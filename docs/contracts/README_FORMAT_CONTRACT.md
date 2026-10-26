# README Format Contract

Status: `CONTRACT`. Scope: this public Labeling Studio repository's README pair.
Product facts and history baseline: [94031ee](https://github.com/Noah8218/OpenVisionLab-Labeling-Studio/commit/94031eee06c67b665a1a3d55a9e1a39c8587fd0c),
with product version **0.3.7**. The current format uses the reviewed common
README structure and development-document split, applied on 2026-10-26 while
preserving this repository's own facts, media, and real version history.
This documentation change does not announce another product release.

Read [AGENTS.md](../../AGENTS.md), [English README](../../README.en.md), and
[한국어 README](../../README.md) before editing. The actual README pair remains
the example of the reviewed format. Obtain the user's confirmation of an exact
proposal before changing the format, language routes, media roles, or detail split.

## Existing order and placement

| Area | English heading or content | Korean heading or content |
| --- | --- | --- |
| Opening | Product title, one-sentence introduction, Version / Windows / .NET / MIT / CI badges | Same roles and order |
| Language navigation | `[English](README.en.md)` then `[한국어](README.md)` | Same first-English link order |
| Hero | Smart Mask GIF, workflow caption, matching MP4 link, idle-time note, three-step trial link | Corresponding Korean capture and caption |
| Task overview | `## Supported tasks`: detection / segmentation / anomaly, three columns with linked thumbnails, ground-truth and model rows | `## 지원 작업` |
| Workflow | `## Workflow`: Dataset → Labeling → Training → Model comparison → Inference | `## 작업 흐름` |
| AI boundary | `## AI proposes candidates`: three lines separating candidates, confirmation/save, and inspection-model selection | `## AI는 후보만 만든다` |
| Task details | `## Task details`: Object detection → Segmentation and Smart Mask → Anomaly detection; screenshot before each explanation | `## 작업별 상세`: 객체 탐지 → 세그멘테이션과 Smart Mask → 이상 탐지 |
| First trial | `## Try it in 3 steps`: three steps and manual-labeling runtime distinction | `## 3단계로 시작하기` |
| Start | `## Quick start`: existing six steps, followed by manual/visual/single-file guide links | `## 빠른 시작` |
| Requirements | `## Installation and requirements` | `## 설치·요구 사항` |
| Recipe/model context | `## One recipe, multiple models`: compact five-line explanation | `## 하나의 레시피, 여러 모델` |
| Build entry | `## Build from source`: prerequisites, two-command build/launch, Development link | `## 소스에서 빌드` |
| Developer details | `## For developers`: existing `<details>`/`<summary>` block | `## 개발자용` |
| Documentation entry | `## Documentation`: user guides, development guide, release notes | `## 문서` |
| Limitations | `## Known Limitations`: short list and Development limitations link | `## 알려진 한계` |
| Version | `## Version`: current version, version-source/release-notes links, explicit versioning statement, recent ten real entries | `## 버전`, `### 최근 버전 이력` |
| Project family | `## Part of OpenVisionLab`: existing five-project table | `## OpenVisionLab 프로젝트` |
| License | `## License`: MIT, LICENSE/NOTICE/third-party links, attribution | `## 라이선스` |

Keep `### Object detection`, `### Segmentation and Smart Mask`, and
`### Anomaly detection` inside Task details. The developer block retains
`### Build Command`, `### Running`, `### Package verification`,
`### Environment check CLI`, and `### CI`. Keep `### Recent version history`
and the current dated entries inside Version. Preserve their spelling and case,
the Korean equivalents, and all headings checked by
[CI](../../.github/workflows/ci.yml); renaming a heading also changes its anchor.

## Language, facts, and detail ownership

- Write natural English first and maintain the visible Korean link and equivalent
  Korean content. English lives in `README.en.md`; the established root
  `README.md` is Korean. Preserve this routing and first-English link order.
  Use established terms, direct instructions, and concise factual prose without
  promotional filler, literal translation, or repeated explanations.
- Make only the edits needed for the feature or correction in its existing area.
  Preserve the common structure and this product's differences. A generic
  template or another repository's newer layout does not authorize restructuring.
- Keep build, launch, package, environment CLI, and extended limitations in
  [Development](../DEVELOPMENT.md). Preserve the short build entry and developer
  disclosure with their CI-required headings. Use the same common structure as
  the reviewed README; differences preserve product facts and real history,
  rather than the older long layout. Keep the full operator workflow in
  [the manual](../tutorial/README.md),
  [visual guide](../tutorial/labeling-workbench-tutorial.html), and
  [single-file guide](../tutorial/labeling-workbench-tutorial-standalone.html).
  Keep protocol examples in their [existing guide](../../samples/python_protocol/README.md).
  Do not introduce links to documents that exist only in a development checkout.
- Preserve the current-version links to [Directory.Build.props](../../Directory.Build.props)
  and [RELEASE_NOTES.md](../../RELEASE_NOTES.md), the explicit versioning statement,
  and the ten real recent entries from 0.3.7 through 0.2.0 in both languages.
  Future versioned updates follow the release policy while preserving older
  traceability. This format-rule maintenance changes no version or release note.
- Confirm changed claims against this checkout's source, commands, and evidence.
  Preserve its newer public features and limitations independently of the
  development repository. Do not downgrade this version, copy development URLs
  or unreleased behavior, or claim unsupported models or successful tests.
  AI candidates, operator confirmation, saving ground truth, and selecting an
  inspection model remain separate steps. Preserve all runtime and pilot caveats.

## Media contract

Keep the hero above Supported tasks. The three task PNGs are linked thumbnails
in the overview and full screenshots before their task explanations. Retain the
language-specific files under `docs/tutorial/images/readme/en/` and `ko/`:
`hero-smart-mask.gif`, its MP4, `detection-washer.png`,
`segmentation-bubble.png`, and `anomaly-grid-disk.png`.

For new or replaced media:

- Capture the latest verified actual EXE for this checkout with its window
  maximized; record build identity, language, resolution, and display scaling.
  Keep full-resolution PNG originals and matching English/Korean scenes with a
  useful image queue and some completed work. This future-capture rule does not
  recertify existing media or change separate UI-comparison conditions.
- Keep the approved washer, bubble, and procedural hexagon scenes. Distinguish
  manual ground truth from AI candidates. Do not present manual edits as AI
  output, a prototype as the actual app, or a historical screen as a new capture.
- Exclude personal paths, cursor/click effects, keystroke/operation overlays,
  composited controls, fake UI, and fabricated results.
- Make GIFs quick and readable: omit idle time and disclose that in the caption,
  preserve candidate → confirmation → explicit save, and keep the approved
  8–12 seconds, 10–12 fps, 1000–1100 pixel width, and at-most-5 MB bounds.
  Retain the matching MP4 and original captures. A different format requires
  the user's confirmation.
- Inspect stills and the entire loop in the rendered README for readable UI,
  framing, clipping, language, genuine workflow, loading, and pacing. This
  rules-only edit is not fresh actual-application or all-view verification.

## Verification before commit and push

1. Reopen the saved AGENTS rules and this contract. Compare both READMEs to this
   checkout's baseline, including recent history. Preserve unrelated user edits.
2. Verify relative paths from each referring file, case, URL encoding, reciprocal
   language links, heading anchors, and every media target. Confirm linked files
   are tracked and that GIF and MP4 playback works.
3. Inspect both rendered languages at normal desktop and narrow widths, including
   the hero, task table, expanded developer block, guides, limitations, recent
   history, and footer. For media edits, also check the actual current app and
   the complete animation. Record unavailable checks as unverified; build success
   or identical numeric settings do not establish visual correctness.
4. Run the required-heading checks from the existing CI and `git diff --check`.
   Keep workflow settings, documentation links, and any existing documentation
   classification intact. Do not copy development-only verifiers or indexes here.
5. Check the staged and committed file lists and full diff. The explicit allowlist
   for this maintenance is `README.md`, `README.en.md`, `docs/DEVELOPMENT.md`,
   `AGENTS.md`, and this contract. Source, settings, versions, release notes,
   tags, releases, and unrelated changes stay outside it. The existing development
   public-surface verifier predates the three authorized ancillary Markdown
   documents (AGENTS, Development, and this contract): report its result and this
   narrow exception without changing the verifier or publication settings.
6. Verify the actual local repository, remote identity, branch, and remote base
   SHA. Use only the authorized non-force push, then verify the resulting remote
   SHA, exact diff, and CI for that SHA. Report absent, pending, skipped, or failed
   CI accurately. Billing-limit failures are separate blockers; do not change
   settings or rerun them. This contract grants no future publication authority.
