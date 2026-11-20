"""Create and inspect OPC UA application certificates (ovl_edge.opcua_security).

    # local stack: requester (server) and edge worker (client), each trusting the other
    python -m ovl_edge.cli.opcua_certs --pki /pki setup --days 365
    # renew one side, then add the new certificate to the peer's trust list
    python -m ovl_edge.cli.opcua_certs --pki /pki issue --side edge-worker --days 365
    python -m ovl_edge.cli.opcua_certs --pki /pki trust --cert /pki/edge-worker/own/edge-worker.der --side requester --name edge-worker
    python -m ovl_edge.cli.opcua_certs --pki /pki inspect

Layout: <pki>/requester/{own,trusted}, <pki>/edge-worker/{own,trusted}.
Private keys are written with owner-only permissions where the file system allows it.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from ovl_edge import opcua_security as sec

SIDES = {
    "requester": ("server", sec.REQUESTER_URI, ["machine-sim", "localhost", "127.0.0.1"]),
    "edge-worker": ("client", sec.WORKER_URI, ["edge-worker", "localhost"]),
}


def issue(pki: Path, side: str, days: int, hosts: list[str] | None = None) -> Path:
    role, uri, default_hosts = SIDES[side]
    cert, _ = sec.issue_certificate(pki / side, side, uri, hosts or default_hosts, role, days)
    return cert


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--pki", required=True)
    sub = parser.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("setup")
    p.add_argument("--days", type=int, default=365)
    p = sub.add_parser("issue")
    p.add_argument("--side", required=True, choices=sorted(SIDES))
    p.add_argument("--days", type=int, default=365)
    p.add_argument("--hosts", nargs="*", default=None)
    p = sub.add_parser("trust")
    p.add_argument("--cert", required=True)
    p.add_argument("--side", required=True, choices=sorted(SIDES), help="whose trust list receives the certificate")
    p.add_argument("--name", required=True)
    sub.add_parser("inspect")
    args = parser.parse_args(argv)
    pki = Path(args.pki)

    if args.cmd == "setup":
        server = issue(pki, "requester", args.days)
        client = issue(pki, "edge-worker", args.days)
        sec.trust(client, pki / "requester" / "trusted", "edge-worker")
        sec.trust(server, pki / "edge-worker" / "trusted", "requester")
    elif args.cmd == "issue":
        issue(pki, args.side, args.days, args.hosts)
    elif args.cmd == "trust":
        sec.trust(Path(args.cert), pki / args.side / "trusted", args.name)
    report = {str(p.relative_to(pki)): sec.certificate_summary(p) for p in sorted(pki.glob("*/*/*.der"))}
    print(json.dumps(report, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
