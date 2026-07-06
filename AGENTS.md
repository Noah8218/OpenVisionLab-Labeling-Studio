# AGENTS.md

This file defines how Codex should work in this repository.

## Operating Rules

- Start every development run with `git status --short`.
- Do not revert or overwrite existing user/Codex changes unless the user explicitly asks for that exact action.
- Do not run `git push` unless the user explicitly asks for `push`.
- A commit request means a local commit only. Push requires a separate explicit request.
- Keep MVVM boundaries: View code-behind may act as a UI adapter, but command/state/workflow/presentation logic should live in ViewModel or Service classes where feasible.
- Avoid Viewer/OpenGL/ROI/brush/eraser performance paths unless the task explicitly requires them.
- Public README/tutorial docs must not include local private paths, conversation notes, portfolio-only wording, or machine-specific details.

## Think Before Coding

- State the concrete goal before editing.
- List assumptions briefly. If an assumption affects behavior or data safety, verify it by opening files/logs/tests or ask the user.
- If the problem becomes unclear, stop and inspect the relevant file, log, or test instead of guessing.

## No Guessing

- Do not present unverified claims as facts.
- If you do not know, open the file, run the command, or inspect the log that can prove it.
- When explaining a conclusion, cite the file, test, command output, or log that supports it.
- If verification is interrupted or unavailable, mark the work as incomplete.

## Simplicity First

- Make the smallest change that satisfies the request.
- Do not add features, abstractions, or extra error handling unless they directly support the current goal.
- Prefer existing local patterns and services over new architecture.

## Surgical Changes

- Touch only the files needed for the request.
- Keep unrelated refactors out of the patch.
- Do not modify verified hot paths unless the request requires it and focused verification is included.

## Goal-Driven Execution

- Convert broad requests into concrete completion goals.
- Prefer goals like "focused tests pass and wording is service-owned" over vague goals like "improve UX".
- Keep a clear next step in the final response.
- When completing priority-driven work, explicitly state any remaining next-priority work in the final response instead of leaving the next step implicit.

## Reasoning Effort

- Low effort: typo fixes, formatting, simple text edits, one-line test expectation updates.
- Medium effort: single-service refactors, focused WPF binding changes, small documentation updates.
- High effort: workflow redesign, model runtime behavior, dataset persistence, performance work, training/inference execution, or cross-module refactors.
- Increase verification rigor with higher effort.

## Completion Definition

Completion must be proven by commands, not by wording alone.

- C# / WPF default:
  - `dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c Debug /nr:false -m:1 /p:UseSharedCompilation=false /p:OutDir=artifacts\isolated-out\`
  - Run the focused `LabelingApplication.Tests.dll` switches for the changed area.
  - `git diff --check`
- WPF UI visual changes:
  - Run the focused build/tests.
  - Capture or regenerate the relevant 1920x1080 screenshot when layout/visuals changed.
  - Update README/tutorial images only with current UI captures.
- Python worker changes:
  - Run Python compile/self-test commands relevant to the touched worker scripts.
  - Run the matching C# focused tests if the worker is called from WPF.
- Documentation-only changes:
  - Run `git diff --check`.
  - Run `--priority-workflow-docs` when workflow/readme/tutorial policy is touched.
- If the repository later adds other stacks, use their native gates:
  - Node: `pnpm test`, linter, typecheck.
  - Python: `pytest`, formatter/linter if configured.
  - Rust: `cargo test`, `cargo clippy`, `cargo fmt --check`.

Do not claim complete if the required verification did not run or did not pass.

## Current Project Priorities

- Continue improving OpenVisionLab Labeling Studio as a full workflow tool: dataset setup, image queue, class setup, object detection/segmentation/anomaly labeling, template labeling, training, inference, model runtime setup, and model comparison.
- Avoid repeating items already documented in `docs/WORK_TRACKING.md` and `docs/STABLE_VERIFIED_AREAS.md`.
- Keep verified items documented after completion.
