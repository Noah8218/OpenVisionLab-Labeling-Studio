"""Worker-side transports for the inspection request/result contract.

Both transports carry the same blocks defined in ovl_edge.modbus_map:
  modbus : holding registers (FC3 read, FC16 write), worker is the Modbus TCP client
  opcua  : one ByteString node per block (big-endian u16 words in the same order),
           worker is the OPC UA client. A single node write is atomic, which gives
           the same "no half-written block" property as one FC16 request.
OPC UA runs without security (anonymous, SecurityPolicy None) for local tests;
a site deployment needs a certificate-based policy agreed with Machine Studio.
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


class ModbusTransport:
    name = "modbus"

    def __init__(self, host: str, port: int, device_id: int = 1, timeout: float = 1.0):
        from pymodbus.client import ModbusTcpClient

        self.client = ModbusTcpClient(host, port=port, timeout=timeout)
        self.device_id = device_id

    def connect(self) -> bool:
        return self.client.connected or bool(self.client.connect())

    def read_request(self) -> Request | None:
        response = self.client.read_holding_registers(REQUEST_BASE, count=REQUEST_SIZE, device_id=self.device_id)
        if response.isError():
            return None
        return Request.decode(list(response.registers))

    def write_result(self, result: Result) -> bool:
        return not self.client.write_registers(RESULT_BASE, result.encode(), device_id=self.device_id).isError()


class OpcUaTransport:
    name = "opcua"

    def __init__(self, url: str, timeout: float = 2.0):
        from asyncua.sync import Client

        self.url = url
        self.client = Client(url, timeout=timeout)
        self.connected = False
        self.request_node = self.result_node = None

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
        except Exception:
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
