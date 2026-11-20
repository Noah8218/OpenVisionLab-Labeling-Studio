"""OPC UA application certificates, trust lists and the secure settings both sides use.

Model (OPC UA Part 6 application instance certificates, self-signed):
  - each application (edge worker = client, requester = server) has its own key
    pair and X.509 certificate whose subjectAltName carries its application URI;
  - each side keeps a trust list folder with the certificates of the peers it
    accepts; the server rejects a client whose certificate is not trusted, is
    outside its validity period or does not match its application URI;
  - messages are signed and encrypted (SecurityPolicy Basic256Sha256,
    SignAndEncrypt); the server offers no unsecured endpoint.

PKI folder layout used by the CLI (ovl_edge.cli.opcua_certs) and the services:
    <pki>/own/<name>.der, <pki>/own/<name>.key.pem   (key files: owner-only permissions)
    <pki>/trusted/<peer>.der

Certificates expire; ovl_certificate_expiry_days is published for the alert rule
CertificateExpiringSoon. Renewal = issue a new certificate, add it to the peer's
trust list, restart, then remove the old one from the trust list.
"""

from __future__ import annotations

import datetime as dt
import ipaddress
import os
import shutil
from dataclasses import dataclass
from pathlib import Path

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID

from prometheus_client import Gauge

WORKER_URI = "urn:openvisionlab:edge-worker"
REQUESTER_URI = "urn:openvisionlab:requester"

CERT_EXPIRY_DAYS = Gauge("ovl_certificate_expiry_days", "Days until a certificate in use expires (negative = expired)", ["certificate"])
OPCUA_SECURITY = Gauge("ovl_opcua_security_info", "OPC UA security in use by this process (value 1)", ["policy", "mode"])


@dataclass(frozen=True)
class SecureSettings:
    certificate: Path  # own certificate (DER)
    private_key: Path  # own private key (PEM)
    app_uri: str
    trusted_dir: Path  # peer certificates accepted
    peer_certificate: Path | None = None  # client side: the server certificate to encrypt for

    @staticmethod
    def worker(pki: Path) -> "SecureSettings":
        return SecureSettings.from_pki(Path(pki) / "edge-worker", "edge-worker", WORKER_URI, peer="requester")

    @staticmethod
    def requester(pki: Path) -> "SecureSettings":
        return SecureSettings.from_pki(Path(pki) / "requester", "requester", REQUESTER_URI)

    @staticmethod
    def from_pki(pki: Path, name: str, app_uri: str, peer: str | None = None) -> "SecureSettings":
        pki = Path(pki)
        return SecureSettings(pki / "own" / f"{name}.der", pki / "own" / f"{name}.key.pem", app_uri, pki / "trusted",
                              pki / "trusted" / f"{peer}.der" if peer else None)


def issue_certificate(out_dir: Path, name: str, app_uri: str, hostnames: list[str], role: str, days: int = 365,
                      not_before: dt.datetime | None = None) -> tuple[Path, Path]:
    """Self-signed OPC UA application certificate. role: 'client' or 'server'."""
    if role not in ("client", "server"):
        raise ValueError("role must be client or server")
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    start = not_before or dt.datetime.now(dt.timezone.utc) - dt.timedelta(minutes=5)
    subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, name), x509.NameAttribute(NameOID.ORGANIZATION_NAME, "OpenVisionLab")])
    alt_names: list[x509.GeneralName] = [x509.UniformResourceIdentifier(app_uri)]
    for host in hostnames:
        try:
            alt_names.append(x509.IPAddress(ipaddress.ip_address(host)))
        except ValueError:
            alt_names.append(x509.DNSName(host))
    usage = ExtendedKeyUsageOID.CLIENT_AUTH if role == "client" else ExtendedKeyUsageOID.SERVER_AUTH
    cert = (
        x509.CertificateBuilder()
        .subject_name(subject)
        .issuer_name(subject)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(start)
        .not_valid_after(start + dt.timedelta(days=days))
        .add_extension(x509.SubjectAlternativeName(alt_names), critical=False)
        .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
        .add_extension(x509.KeyUsage(digital_signature=True, content_commitment=True, key_encipherment=True, data_encipherment=True,
                                     key_agreement=False, key_cert_sign=True, crl_sign=False, encipher_only=False, decipher_only=False), critical=True)
        .add_extension(x509.ExtendedKeyUsage([usage]), critical=False)
        .add_extension(x509.SubjectKeyIdentifier.from_public_key(key.public_key()), critical=False)
        .sign(key, hashes.SHA256())
    )
    out = Path(out_dir) / "own"
    out.mkdir(parents=True, exist_ok=True)
    cert_path, key_path = out / f"{name}.der", out / f"{name}.key.pem"
    cert_path.write_bytes(cert.public_bytes(serialization.Encoding.DER))
    key_path.write_bytes(key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.TraditionalOpenSSL, serialization.NoEncryption()))
    try:
        os.chmod(key_path, 0o600)
    except OSError:  # file systems without POSIX modes (e.g. some Windows mounts)
        pass
    return cert_path, key_path


def trust(certificate: Path, trusted_dir: Path, name: str | None = None) -> Path:
    trusted_dir = Path(trusted_dir)
    trusted_dir.mkdir(parents=True, exist_ok=True)
    target = trusted_dir / (f"{name}.der" if name else Path(certificate).name)
    shutil.copyfile(certificate, target)
    return target


def load_certificate(path: Path) -> x509.Certificate:
    data = Path(path).read_bytes()
    return x509.load_pem_x509_certificate(data) if data.startswith(b"-----") else x509.load_der_x509_certificate(data)


def expiry_days(path: Path, now: dt.datetime | None = None) -> float:
    cert = load_certificate(path)
    now = now or dt.datetime.now(dt.timezone.utc)
    return (cert.not_valid_after_utc - now).total_seconds() / 86400.0


_WATCHED: dict[str, Path] = {}


def publish_expiry(label: str, path: Path) -> float:
    """Publish now and keep refreshing it (refresh_expiry) for the life of the process."""
    _WATCHED[label] = Path(path)
    days = expiry_days(path)
    CERT_EXPIRY_DAYS.labels(label).set(round(days, 2))
    return days


def refresh_expiry() -> None:
    for label, path in list(_WATCHED.items()):
        try:
            CERT_EXPIRY_DAYS.labels(label).set(round(expiry_days(path), 2))
        except OSError:
            CERT_EXPIRY_DAYS.labels(label).set(-1)  # certificate file gone: treat as expired


def certificate_summary(path: Path) -> dict:
    cert = load_certificate(path)
    san = cert.extensions.get_extension_for_class(x509.SubjectAlternativeName).value
    return {"subject": cert.subject.rfc4514_string(), "app_uri": san.get_values_for_type(x509.UniformResourceIdentifier),
            "not_after": cert.not_valid_after_utc.isoformat(), "days_left": round(expiry_days(path), 1),
            "sha1_thumbprint": cert.fingerprint(hashes.SHA1()).hex()}
