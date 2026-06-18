#!/usr/bin/env python3
"""Mock Python YOLO client for the C# labeling application.

The C# app listens as a TCP server. This script connects as the Python model
side, prints training/inference packets, and returns deterministic detection
results for StartDefect packets.
"""

from __future__ import annotations

import argparse
import json
import socket
import sys
import time
from dataclasses import dataclass
from typing import Iterable


PACKET_SEPARATOR = b"\n\n"
PNG_IEND_MARKER = b"IEND"


@dataclass
class Packet:
    command: str
    payload: bytes


def build_detection_result(result_format: str) -> bytes:
    items = [
        {
            "ClassName": "NG",
            "Confidence": 0.98,
            "X": 24,
            "Y": 18,
            "Width": 120,
            "Height": 80,
        }
    ]

    if result_format == "v1":
        envelope = {
            "type": "ResultDefect",
            "version": 1,
            "imageId": "mock-image",
            "items": [
                {
                    "className": item["ClassName"],
                    "confidence": item["Confidence"],
                    "x": item["X"],
                    "y": item["Y"],
                    "width": item["Width"],
                    "height": item["Height"],
                }
                for item in items
            ],
        }
        return json.dumps(envelope, separators=(",", ":")).encode("utf-8")

    return ("ResultDefect " + json.dumps(items, separators=(",", ":"))).encode("utf-8")


def send_response(sock: socket.socket, payload: bytes, split_response: bool) -> None:
    if not split_response or len(payload) < 8:
        sock.sendall(payload)
        return

    split_at = max(1, len(payload) // 2)
    sock.sendall(payload[:split_at])
    time.sleep(0.05)
    sock.sendall(payload[split_at:])


def parse_packets(buffer: bytearray) -> Iterable[Packet]:
    while True:
        separator_index = buffer.find(PACKET_SEPARATOR)
        if separator_index < 0:
            return

        command = buffer[:separator_index].decode("ascii", errors="replace").strip()
        payload_start = separator_index + len(PACKET_SEPARATOR)

        if command == "StartTraining":
            packet_length = find_json_packet_end(buffer, payload_start)
        elif command == "StartDefect":
            packet_length = find_png_packet_end(buffer, payload_start)
        elif command in {"StopTraining", "StopDefect"}:
            packet_length = payload_start
        else:
            line_break = buffer.find(b"\n")
            if line_break < 0:
                return
            packet_length = line_break + 1

        if packet_length < 0:
            return

        payload = bytes(buffer[payload_start:packet_length])
        del buffer[:packet_length]
        yield Packet(command=command, payload=payload)


def find_json_packet_end(buffer: bytearray, start: int) -> int:
    depth = 0
    in_string = False
    escaped = False

    for index in range(start, len(buffer)):
        char = chr(buffer[index])
        if in_string:
            if escaped:
                escaped = False
                continue
            if char == "\\":
                escaped = True
                continue
            if char == '"':
                in_string = False
            continue

        if char == '"':
            in_string = True
            continue
        if char == "{":
            depth += 1
            continue
        if char == "}":
            depth -= 1
            if depth == 0:
                return index + 1

    return -1


def find_png_packet_end(buffer: bytearray, start: int) -> int:
    marker_index = buffer.find(PNG_IEND_MARKER, start)
    if marker_index < 0:
        return -1

    return marker_index + len(PNG_IEND_MARKER) + 4


def handle_packet(sock: socket.socket, packet: Packet, result_format: str, split_response: bool) -> None:
    if packet.command == "StartTraining":
        try:
            request = json.loads(packet.payload.decode("utf-8"))
        except json.JSONDecodeError as exc:
            print(f"invalid training payload: {exc}", flush=True)
            return

        print(f"training request: {json.dumps(request, ensure_ascii=False)}", flush=True)
        return

    if packet.command == "StartDefect":
        print(f"inference image bytes: {len(packet.payload)}", flush=True)
        send_response(sock, build_detection_result(result_format), split_response)
        return

    print(f"command: {packet.command}", flush=True)


def run_client(args: argparse.Namespace) -> int:
    buffer = bytearray()
    packets_handled = 0

    with socket.create_connection((args.host, args.port), timeout=args.timeout) as sock:
        print(f"connected to {args.host}:{args.port}", flush=True)
        sock.settimeout(args.timeout)

        while True:
            chunk = sock.recv(65536)
            if not chunk:
                print("server closed connection", flush=True)
                return 0

            buffer.extend(chunk)
            for packet in parse_packets(buffer):
                packets_handled += 1
                handle_packet(sock, packet, args.result_format, args.split_response)
                if args.once and packets_handled >= 1:
                    return 0


def run_self_test() -> int:
    training = b'StartTraining\n\n{"imgSize":"640","batch":"8","epoch":"100"}'
    image = b"StartDefect\n\n\x89PNG\r\n\x1a\nmock-data" + PNG_IEND_MARKER + b"\x00\x00\x00\x00"
    buffer = bytearray(training + image)
    packets = list(parse_packets(buffer))

    assert len(packets) == 2, f"expected 2 packets, got {len(packets)}"
    assert packets[0].command == "StartTraining"
    assert packets[1].command == "StartDefect"
    assert not buffer
    assert build_detection_result("legacy").startswith(b"ResultDefect ")
    assert json.loads(build_detection_result("v1").decode("utf-8"))["type"] == "ResultDefect"
    print("self-test passed", flush=True)
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Mock YOLO client for the C# labeling app.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--timeout", type=float, default=10)
    parser.add_argument("--result-format", choices=("legacy", "v1"), default="v1")
    parser.add_argument("--split-response", action="store_true")
    parser.add_argument("--once", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()

    return run_client(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
