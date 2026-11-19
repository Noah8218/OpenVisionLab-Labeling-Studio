"""OPC UA server side of the inspection contract for the simulator (local tests only).

Exposes the same register blocks as ByteString nodes and offers the async
get/set interface the simulator uses for its Modbus RegisterServer.
"""

from __future__ import annotations

from ovl_edge.modbus_map import APPLIED_BASE, REQUEST_BASE, RESULT_BASE
from ovl_edge.transport import OPCUA_NAMESPACE_URI, OPCUA_NODES, OPCUA_OBJECT, block_to_regs, regs_to_block

_BASES = {REQUEST_BASE: "request", RESULT_BASE: "result", APPLIED_BASE: "applied"}


class OpcUaBlockStore:
    def __init__(self):
        self.server = None
        self.nodes: dict[str, object] = {}

    async def start(self, host: str, port: int) -> None:
        from asyncua import Server, ua

        self.server = Server()
        await self.server.init()
        self.server.set_endpoint(f"opc.tcp://{host}:{port}/ovl/")
        self.server.set_security_policy([ua.SecurityPolicyType.NoSecurity])
        ns = await self.server.register_namespace(OPCUA_NAMESPACE_URI)
        obj = await self.server.nodes.objects.add_object(ns, OPCUA_OBJECT)
        for key, (name, size) in OPCUA_NODES.items():
            node = await obj.add_variable(ns, name, ua.Variant(bytes(2 * size), ua.VariantType.ByteString))
            await node.set_writable()
            self.nodes[key] = node
        await self.server.start()

    def _locate(self, address: int) -> tuple[str, int]:
        for base, key in sorted(_BASES.items(), reverse=True):
            if address >= base:
                return key, address - base
        raise ValueError(address)

    async def get(self, address: int, count: int) -> list[int]:
        key, offset = self._locate(address)
        regs = block_to_regs(await self.nodes[key].read_value())
        return regs[offset : offset + count]

    async def set(self, address: int, values: list[int]) -> None:
        from asyncua import ua

        key, offset = self._locate(address)
        regs = block_to_regs(await self.nodes[key].read_value())
        regs[offset : offset + len(values)] = [v & 0xFFFF for v in values]
        await self.nodes[key].write_value(ua.Variant(regs_to_block(regs), ua.VariantType.ByteString))

    async def stop(self) -> None:
        if self.server:
            await self.server.stop()
