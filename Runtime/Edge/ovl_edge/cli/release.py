"""Operator commands for the release registry (ovl_edge.release).

    python -m ovl_edge.cli.release --registry /registry register --id r3-fp16 --package /model --rule rule.json \
        --runtime trt-fp16 --fallback ort-cuda rule --rule-params rule-params.json --note "first release"
    python -m ovl_edge.cli.release --registry /registry activate r3-fp16
    python -m ovl_edge.cli.release --registry /registry shadow r3-int8         # candidate judged in parallel, not used
    python -m ovl_edge.cli.release --registry /registry canary r3-int8 --percent 10
    python -m ovl_edge.cli.release --registry /registry promote                 # gate on the shadow report
    python -m ovl_edge.cli.release --registry /registry promote --force --review review.json --note "..."  # recorded override
    python -m ovl_edge.cli.release --registry /registry rollback [--to r3-fp16]
    python -m ovl_edge.cli.release --registry /registry status
    python -m ovl_edge.cli.release --registry /registry verify r3-fp16         # files still match their hashes

Every change rewrites pointer.json atomically and appends to its history; a
running worker installs it between requests without a restart.
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict
from pathlib import Path

from ovl_edge import release as rel


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--registry", required=True)
    sub = parser.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("register")
    p.add_argument("--id", required=True)
    p.add_argument("--package", required=True)
    p.add_argument("--rule", required=True)
    p.add_argument("--runtime", required=True)
    p.add_argument("--fallback", nargs="*", default=[])
    p.add_argument("--rule-params", default="")
    p.add_argument("--note", default="")
    for name in ("activate", "shadow", "canary"):
        p = sub.add_parser(name)
        p.add_argument("id")
        p.add_argument("--note", default="")
        if name == "canary":
            p.add_argument("--percent", type=int, required=True)
    p = sub.add_parser("clear-candidate")
    p.add_argument("--note", default="")
    p = sub.add_parser("promote")
    p.add_argument("--min-compared", type=int, default=rel.GateCriteria.min_compared)
    p.add_argument("--min-agreement", type=float, default=rel.GateCriteria.min_agreement)
    p.add_argument("--max-ng-to-pass", type=int, default=rel.GateCriteria.max_ng_to_pass)
    p.add_argument("--max-candidate-p99-ms", type=float, default=rel.GateCriteria.max_candidate_p99_ms)
    p.add_argument("--force", action="store_true", help="promote despite gate failures (recorded as forced); needs --review")
    p.add_argument("--review", default="", help="JSON review of the shadow disagreements against ground truth")
    p.add_argument("--note", default="")
    p = sub.add_parser("rollback")
    p.add_argument("--to", default=None)
    p.add_argument("--note", default="")
    sub.add_parser("status")
    p = sub.add_parser("verify")
    p.add_argument("id")
    args = parser.parse_args(argv)
    root = Path(args.registry)

    try:
        if args.cmd == "register":
            r = rel.register(root, args.id, Path(args.package), Path(args.rule), args.runtime, args.fallback,
                             Path(args.rule_params) if args.rule_params else None, args.note)
            print(json.dumps({"registered": r.id, "files": len(r.files), "digest": r.digest()[:16]}))
            return 0
        if args.cmd == "verify":
            changed = rel.verify_release(root, rel.load_release(root, args.id))
            print(json.dumps({"release": args.id, "intact": not changed, "changed_files": changed}))
            return 0 if not changed else 4
        if args.cmd == "activate":
            pointer = rel.activate(root, args.id, args.note)
        elif args.cmd in ("shadow", "canary"):
            pointer = rel.set_candidate(root, args.id, args.cmd, getattr(args, "percent", 0), args.note)
        elif args.cmd == "clear-candidate":
            pointer = rel.clear_candidate(root, args.note)
        elif args.cmd == "promote":
            criteria = rel.GateCriteria(args.min_compared, args.min_agreement, args.max_ng_to_pass, args.max_candidate_p99_ms)
            pointer = rel.promote(root, criteria, args.force, args.note, Path(args.review) if args.review else None)
        elif args.cmd == "rollback":
            pointer = rel.rollback(root, args.to, args.note)
        else:
            pointer = rel.read_pointer(root)
            reports = {}
            for path in sorted((root / "reports").glob("shadow-*.json")):
                reports[path.stem] = json.loads(path.read_text(encoding="utf-8"))
            print(json.dumps({"pointer": {k: v for k, v in asdict(pointer).items() if k != "history"},
                              "last_changes": pointer.history[-5:], "shadow_reports": reports}, indent=1))
            return 0
    except PermissionError as exc:  # gate failure
        print(json.dumps({"refused": str(exc)}))
        return 3
    except (ValueError, FileExistsError, FileNotFoundError) as exc:
        print(json.dumps({"error": str(exc)}))
        return 2
    print(json.dumps({"revision": pointer.revision, "active": pointer.active, "candidate": pointer.candidate, "mode": pointer.mode,
                      "canary_percent": pointer.canary_percent, "action": pointer.history[-1]["action"]}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
