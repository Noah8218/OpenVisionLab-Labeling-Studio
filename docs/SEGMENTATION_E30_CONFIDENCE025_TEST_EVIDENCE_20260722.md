# External native segmentation: YOLO confidence 0.25 held-out evidence (2026-07-22)

Status: Complete

## Scope

Make the opt-in, reproducible paired-comparison runner expose its YOLOv8-seg
confidence instead of silently forcing `0.00`, then execute the already chosen
`0.25` threshold exactly once on the preserved 60-image `test` split.  The
threshold was selected previously on `valid` only.  This scope excludes model
retraining, source changes, U-Net calibration, UI changes, automatic adoption,
and any production-quality claim.

## Acceptance criteria and evidence

1. `--yolo-confidence` accepts only an invariant-culture number in `[0, 1]`,
   defaults to `0.25`, and is written to the run summary: passed by
   `tests\LabelingApplication.Tests\Program.RealExternalSegmentationAdapterComparison.cs`.
2. The exact requested `0.25` reaches the app-owned YOLO prediction manifest:
   passed. The first record declares `split=test`, `imageSize=320`,
   `confidence=0.25`, the fixed checkpoint SHA-256
   `0AF2A2C937C349C11B2021491ADA586B48DAF7DC5E2AE504D8073A0E112B7CBF`,
   and the unchanged canonical dataset/class contracts.
3. One final test replay completed over all 60 held-out images without report
   errors: passed. Its source fingerprint before and after is identical:
   `C0FDB11C644F1D705EC3B033FDFB5205E0DE72129559624699C85D5B64CCEF53`.
4. The common-mask evaluator reports the following results:

   | Metric | U-Net | YOLOv8-seg at 0.25 |
   | --- | ---: | ---: |
   | Mean Dice | 0.243091 | 0.721702 |
   | Mean IoU | 0.156165 | 0.570198 |

   | Class | YOLO Dice | YOLO IoU | component TP / FP / FN |
   | --- | ---: | ---: | --- |
   | contamination_spot | 0.742660 | 0.590660 | 4 / 0 / 2 |
   | scratch_crack | 0.695835 | 0.533549 | 7 / 1 / 1 |
   | missing_material | 0.855801 | 0.747948 | 9 / 2 / 1 |
   | foreign_particle | 0.646248 | 0.477375 | 4 / 1 / 1 |
   | extra_material_bridge | 0.667964 | 0.501461 | 6 / 0 / 3 |

## Decision

For this fixed 30-epoch checkpoint and this same-source test packet, the
YOLOv8-seg `0.25` profile is the valid common-mask comparison candidate.  The
earlier `0.00` score was a raw-export evidence setting, not a fair profile
comparison.  No model was automatically selected or adopted.

## Artifacts and commands

- Run summary:
  `artifacts\benchmark-external-seg-adapter-compare-e30-confidence025-test-20260722\summary.txt`.
- Full report:
  `artifacts\benchmark-external-seg-adapter-compare-e30-confidence025-test-20260722\model-center-run\run-f29c6b16d401d5fb-20260721-221513\comparison\comparison-summary.json`.
- YOLO manifest:
  `artifacts\benchmark-external-seg-adapter-compare-e30-confidence025-test-20260722\model-center-run\run-f29c6b16d401d5fb-20260721-221513\yolo-seg\prediction-manifest.jsonl`.
- Actual command:

```powershell
dotnet .\tests\LabelingApplication.Tests\artifacts\isolated-out\LabelingApplication.Tests.dll `
  --real-external-segmentation-adapter-comparison `
  --external-data-yaml <fixed native data.yaml> `
  --unet-weights <fixed U-Net best.pt> `
  --yolo-weights <fixed YOLOv8-seg best.pt> `
  --image-size 320 --yolo-confidence 0.25 `
  --artifact-root artifacts\benchmark-external-seg-adapter-compare-e30-confidence025-test-20260722
```

## Boundary / next dependency

This proves one reproducible same-source comparison after validation-only
threshold selection. It does not prove camera/session generalization,
production readiness, comparable CUDA/CPU latency, or that the U-Net
class-confusion issue is solved. An independent acquired segmentation packet
with a leakage-guarded holdout is required before a production decision.
