"""Minimal asyncio Modbus TCP server for the simulator (holding registers only).

Implements FC3 (read holding registers), FC6 (write single register) and FC16
(write multiple registers) on a flat 0-based register array. It exists because
pymodbus 3.16 turned its server datastore into a deprecated shim without direct
server-side register access; the worker side keeps using the pymodbus client,
and tests check this server against that client.
"""

from __future__ import annotations

import asyncio
import struct

ILLEGAL_FUNCTION, ILLEGAL_ADDRESS, ILLEGAL_VALUE = 1, 2, 3


class RegisterServer:
    def __init__(self, size: int = 1000, unit_ids: set[int] | None = None):
        self.registers = [0] * size
        self.unit_ids = unit_ids  # None accepts any unit id
        self._server: asyncio.base_events.Server | None = None

    # In-process access for the requesting side (same event loop, no locking needed).
    def get(self, address: int, count: int) -> list[int]:
        return self.registers[address : address + count]

    def set(self, address: int, values: list[int]) -> None:
        self.registers[address : address + len(values)] = [v & 0xFFFF for v in values]

    def _exception(self, fc: int, code: int) -> bytes:
        return bytes([fc | 0x80, code])

    def _handle_pdu(self, pdu: bytes) -> bytes:
        fc = pdu[0]
        size = len(self.registers)
        if fc == 3:
            if len(pdu) != 5:
                return self._exception(fc, ILLEGAL_VALUE)
            address, count = struct.unpack(">HH", pdu[1:5])
            if not 1 <= count <= 125:
                return self._exception(fc, ILLEGAL_VALUE)
            if address + count > size:
                return self._exception(fc, ILLEGAL_ADDRESS)
            values = self.get(address, count)
            return bytes([fc, 2 * count]) + struct.pack(f">{count}H", *values)
        if fc == 6:
            if len(pdu) != 5:
                return self._exception(fc, ILLEGAL_VALUE)
            address, value = struct.unpack(">HH", pdu[1:5])
            if address >= size:
                return self._exception(fc, ILLEGAL_ADDRESS)
            self.set(address, [value])
            return pdu
        if fc == 16:
            if len(pdu) < 6:
                return self._exception(fc, ILLEGAL_VALUE)
            address, count, byte_count = struct.unpack(">HHB", pdu[1:6])
            if not 1 <= count <= 123 or byte_count != 2 * count or len(pdu) != 6 + byte_count:
                return self._exception(fc, ILLEGAL_VALUE)
            if address + count > size:
                return self._exception(fc, ILLEGAL_ADDRESS)
            # One FC16 request is applied as a whole before the next request is read,
            # so readers never see a partially written block.
            self.set(address, list(struct.unpack(f">{count}H", pdu[6 : 6 + byte_count])))
            return bytes([fc]) + struct.pack(">HH", address, count)
        return self._exception(fc, ILLEGAL_FUNCTION)

    async def _client(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        try:
            while True:
                header = await reader.readexactly(7)
                transaction, protocol, length, unit = struct.unpack(">HHHB", header)
                pdu = await reader.readexactly(length - 1)
                if protocol != 0 or (self.unit_ids is not None and unit not in self.unit_ids):
                    continue  # not for this server: no response (Modbus TCP gateway rule)
                response = self._handle_pdu(pdu)
                writer.write(struct.pack(">HHHB", transaction, 0, len(response) + 1, unit) + response)
                await writer.drain()
        except (asyncio.IncompleteReadError, ConnectionResetError):
            pass
        finally:
            writer.close()

    async def start(self, host: str, port: int) -> None:
        self._server = await asyncio.start_server(self._client, host, port)

    async def stop(self) -> None:
        if self._server:
            self._server.close()
            await self._server.wait_closed()
