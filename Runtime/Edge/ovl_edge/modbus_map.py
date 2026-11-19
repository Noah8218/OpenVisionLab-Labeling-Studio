"""Modbus TCP holding-register map between the requesting side (server) and the worker (client).

Draft for the first protocol (design E7/M4). The Machine Studio side of this
contract is not verified on this workstation; the simulator in
ovl_edge.cli.machine_sim implements the server role for local tests only.

Every block is written with one FC16 (write multiple registers) request so a
reader never observes a half-written block. 32-bit values are big-endian word
order (high word first).

Request block (server writes)      @ REQUEST_BASE, REQUEST_SIZE registers
  0-1  session id (u32)            2-3  request seq (u32)
  4    camera id (u16)             5    flags: bit0 = request pending
  6-9  frame SHA-256, first 8 bytes
Result block (worker writes)       @ RESULT_BASE, RESULT_SIZE registers
  0-1  session id                  2-3  request seq
  4    ResultCode                  5    detection count
  6    qualifying (NG) count       7    top class (0xFFFF = none)
  8    top score x 10000           9    commit counter (changes on every new result)
  10-13 model spec SHA-256 prefix  14-17 rule SHA-256 prefix
  18-21 frame SHA-256 prefix echo  22-23 worker service time (us, u32)
Applied block (server writes)      @ APPLIED_BASE, APPLIED_SIZE registers
  0-1  session id                  2-3  request seq
Frame bytes are read from the shared read-only input folder at
<input_root>/<session:08x>/<seq:010d>.<ext> (design E6); ext is png (default) or bmp.
"""

from __future__ import annotations

from dataclasses import dataclass

REQUEST_BASE, REQUEST_SIZE = 0, 10
RESULT_BASE, RESULT_SIZE = 100, 24
APPLIED_BASE, APPLIED_SIZE = 200, 4
FLAG_PENDING = 0x0001
NO_CLASS = 0xFFFF


def u32_to_regs(value: int) -> list[int]:
    value &= 0xFFFFFFFF
    return [(value >> 16) & 0xFFFF, value & 0xFFFF]


def regs_to_u32(regs: list[int]) -> int:
    return ((regs[0] & 0xFFFF) << 16) | (regs[1] & 0xFFFF)


def bytes_to_regs(data: bytes) -> list[int]:
    if len(data) % 2:
        data += b"\x00"
    return [(data[i] << 8) | data[i + 1] for i in range(0, len(data), 2)]


def regs_to_bytes(regs: list[int]) -> bytes:
    return b"".join(bytes([(r >> 8) & 0xFF, r & 0xFF]) for r in regs)


FRAME_EXTENSIONS = ("png", "bmp")


def frame_relative_path(session: int, seq: int, ext: str = "png") -> str:
    # Both sides must use the same extension (deployment setting OVL_FRAME_EXT).
    if ext not in FRAME_EXTENSIONS:
        raise ValueError(f"Unsupported frame extension: {ext}")
    return f"{session:08x}/{seq:010d}.{ext}"


@dataclass(frozen=True)
class Request:
    session: int
    seq: int
    camera: int
    pending: bool
    frame_sha_prefix: bytes

    def encode(self) -> list[int]:
        regs = u32_to_regs(self.session) + u32_to_regs(self.seq) + [self.camera & 0xFFFF, FLAG_PENDING if self.pending else 0]
        regs += bytes_to_regs(self.frame_sha_prefix[:8].ljust(8, b"\x00"))
        assert len(regs) == REQUEST_SIZE
        return regs

    @staticmethod
    def decode(regs: list[int]) -> "Request":
        return Request(regs_to_u32(regs[0:2]), regs_to_u32(regs[2:4]), regs[4], bool(regs[5] & FLAG_PENDING), regs_to_bytes(regs[6:10]))


@dataclass(frozen=True)
class Result:
    session: int
    seq: int
    code: int
    detections: int
    qualifying: int
    top_class: int
    top_score: float
    commit: int
    model_sha_prefix: bytes
    rule_sha_prefix: bytes
    frame_sha_prefix: bytes
    service_us: int

    def encode(self) -> list[int]:
        regs = u32_to_regs(self.session) + u32_to_regs(self.seq)
        regs += [
            self.code & 0xFFFF,
            min(self.detections, 0xFFFF),
            min(self.qualifying, 0xFFFF),
            NO_CLASS if self.top_class < 0 else self.top_class & 0xFFFF,
            int(round(max(0.0, min(1.0, self.top_score)) * 10000)),
            self.commit & 0xFFFF,
        ]
        for prefix in (self.model_sha_prefix, self.rule_sha_prefix, self.frame_sha_prefix):
            regs += bytes_to_regs(prefix[:8].ljust(8, b"\x00"))
        regs += u32_to_regs(self.service_us)
        assert len(regs) == RESULT_SIZE
        return regs

    @staticmethod
    def decode(regs: list[int]) -> "Result":
        top_class = regs[7]
        return Result(
            session=regs_to_u32(regs[0:2]),
            seq=regs_to_u32(regs[2:4]),
            code=regs[4],
            detections=regs[5],
            qualifying=regs[6],
            top_class=-1 if top_class == NO_CLASS else top_class,
            top_score=regs[8] / 10000,
            commit=regs[9],
            model_sha_prefix=regs_to_bytes(regs[10:14]),
            rule_sha_prefix=regs_to_bytes(regs[14:18]),
            frame_sha_prefix=regs_to_bytes(regs[18:22]),
            service_us=regs_to_u32(regs[22:24]),
        )
