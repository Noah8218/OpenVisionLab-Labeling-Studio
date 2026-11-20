"""Worker-side transports for the inspection request/result contract.

Both transports carry the same blocks defined in ovl_edge.modbus_map:
  modbus : holding registers (FC3 read, FC16 write), worker is the Modbus TCP client
  opcua  : one ByteString node per block (big-endian u16 words in the same order),
           worker is the OPC UA client. A single node write is atomic, which gives
           the same "no half-written block" property as one FC16 request.
OPC UA security: without `secure` the client connects with SecurityPolicy None
(anonymous, local tests only). With `secure` (ovl_edge.opcua_security) it uses
Basic256Sha256 SignAndEncrypt with its own application certificate and pins the
server certificate it was given; the server must trust the worker's certificate.
"""

from __future__ import annotations

import struct

from ovl_edge.modbus_map import APPLIED_SIZE, REQUEST_BASE, REQUEST_SIZE, RESULT_BASE, RESULT_SIZE, Request, Result

OPCUA_NAMESPACE_URI = "urn:openvisionlab:edge-inspection"
OPCUA_OBJECT = "OvlInspection"
OPCUA_NODES = {"request": ("RequestBlock", REQUEST_SIZE), "result": ("ResultBlock", RESULT_SIZE), "applied": ("AppliedBlock", APPLIED_SIZE)}


def regs_to_block(regs: list[int]) -> bytes:
    return struct.pack(f">{len(regs)}H", *[r & 0xFFFF for r in regs])


def block_to_regs(block: bytes) -> list[int]:
    return list(struct.unpack(f">{len(block) // 2}H", block))


def _modbus_link_errors() -> tuple[type[BaseException], ...]:
    """Errors that mean the requester link is gone (socket reset, server stopped), not a bug."""
    try:
        from pymodbus.exceptions import ModbusException
    except ImportError:  # unit tests without pymodbus
        return (OSError,)
    return (OSError, ModbusException)


class ModbusTransport:
    """A lost requester link is reported as a failed read/write, never raised: the worker
    loop then reconnects (Worker.step calls connect again) instead of the process exiting.
    Found in the 2026-11-20 soak: stopping the requester raised ConnectionResetError out of
    read_holding_registers and the worker only came back through the container restart policy."""

    name = "modbus"

    def __init__(self, host: str, port: int, device_id: int = 1, timeout: float = 1.0):
        from pymodbus.client import ModbusTcpClient

        self.client = ModbusTcpClient(host, port=port, timeout=timeout)
        self.device_id = device_id
        self.last_error = ""

    def _drop(self, exc: BaseException) -> None:
        self.last_error = f"{type(exc).__name__}: {exc}"
        try:
            self.client.close()
        except Exception:
            pass

    def connect(self) -> bool:
        try:
            return self.client.connected or bool(self.client.connect())
        except _modbus_link_errors() as exc:
            self._drop(exc)
            return False

    def read_request(self) -> Request | None:
        try:
            response = self.client.read_holding_registers(REQUEST_BASE, count=REQUEST_SIZE, device_id=self.device_id)
        except _modbus_link_errors() as exc:
            self._drop(exc)
            return None
        if response.isError():
            return None
        return Request.decode(list(response.registers))

    def write_result(self, result: Result) -> bool:
        try:
            return not self.client.write_registers(RESULT_BASE, result.encode(), device_id=self.device_id).isError()
        except _modbus_link_errors() as exc:
            self._drop(exc)
            return False


class OpcUaTransport:
    name = "opcua"

    def __init__(self, url: str, timeout: float = 2.0, secure=None):
        from asyncua.sync import Client

        self.url = url
        self.client = Client(url, timeout=timeout)
        self.connected = False
        self.request_node = self.result_node = None
        self.last_error = ""
        if secure is not None:
            from asyncua import ua
            from asyncua.crypto.security_policies import SecurityPolicyBasic256Sha256

            from ovl_edge.opcua_security import OPCUA_SECURITY, publish_expiry

            # The client's application URI must match the URI in its certificate.
            self.client.aio_obj.application_uri = secure.app_uri
            self.client.set_security(SecurityPolicyBasic256Sha256, certificate=str(secure.certificate), private_key=str(secure.private_key),
                                     server_certificate=str(secure.peer_certificate), mode=ua.MessageSecurityMode.SignAndEncrypt)
            OPCUA_SECURITY.labels("Basic256Sha256", "SignAndEncrypt").set(1)
            publish_expiry("opcua-client", secure.certificate)
            publish_expiry("opcua-server-pinned", secure.peer_certificate)

    def connect(self) -> bool:
        if self.connected:
            return True
        try:
            self.client.connect()
            ns = self.client.get_namespace_index(OPCUA_NAMESPACE_URI)
            root = self.client.nodes.objects
            self.request_node = root.get_child([f"{ns}:{OPCUA_OBJECT}", f"{ns}:{OPCUA_NODES['request'][0]}"])
            self.result_node = root.get_child([f"{ns}:{OPCUA_OBJECT}", f"{ns}:{OPCUA_NODES['result'][0]}"])
            self.connected = True
        except Exception as exc:  # refused certificate, wrong policy, network: reported as a connect error
            self.last_error = f"{type(exc).__name__}: {exc}"
            self.connected = False
        return self.connected

    def read_request(self) -> Request | None:
        try:
            block = self.request_node.read_value()
        except Exception:
            self.connected = False
            return None
        regs = block_to_regs(block)
        return Request.decode(regs) if len(regs) == REQUEST_SIZE else None

    def write_result(self, result: Result) -> bool:
        from asyncua import ua

        try:
            self.result_node.write_value(ua.Variant(regs_to_block(result.encode()), ua.VariantType.ByteString))
            return True
        except Exception:
            self.connected = False
            return False
