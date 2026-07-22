# External native segmentation: U-Net / YOLOv8-seg / YOLO11-seg comparison (2026-07-22)

Status: Complete

## Scope

Complete the missing YOLO11 segmentation leg of the existing controlled
U-Net/YOLOv8 benchmark. The comparison reuses the same native five-class
EasyMatch Die Array packet, its fixed `360/80/60` train/valid/test split, image
size `320`, batch `4`, and 30-epoch training budget. YOLO predictions use the
previously selected `0.25` confidence; the 60-image test split is not used to
tune that value.

This scope does not change the recipe, labels, split, model-adoption state, or
normal UI defaults. It does not compare training or inference time because the
recorded training environments are not hardware-equivalent.

## Fixed evidence identity

| Item | Value |
| --- | --- |
| Native source tree | 2,004 files |
| Native source tree SHA-256 before/after YOLO11 training | `5819E2ED72E402D3F06C32CF4F1FB3481A2DF1D70BD8CB8C00B97CE9E28199C2` / identical |
| Native source fingerprint before/after comparison | `C0FDB11C644F1D705EC3B033FDFB5205E0DE72129559624699C85D5B64CCEF53` / identical |
| Canonical dataset fingerprint | `f29c6b16d401d5fb18356220c0952928f5477c318188ccff76548d903ea63cab` |
| Class-contract SHA-256 | `68A393C83A39C103E293D522D70C10C0EC62791E46CE8D3FEF94EE4FAEDAAE76` |
| Test images | 60 |
| U-Net checkpoint SHA-256 | `487EF0EE70FD3A37260F4D9CB17C12994FC747575D1984EC8C64BD65967C6F72` |
| YOLOv8-seg checkpoint SHA-256 | `0AF2A2C937C349C11B2021491ADA586B48DAF7DC5E2AE504D8073A0E112B7CBF` |
| YOLO11-seg seed SHA-256 | `55ED65C56C91713D23E8402371C6C49A6FD84F257F7DCE452E8D70E41DCBE152` |
| YOLO11-seg checkpoint SHA-256 | `4A09B5F668B8F2AA2DAF9FEDB9ADDA4954A607D61CB08C96379AE8CA82462ECA` |
| YOLO11 runtime | Ultralytics `8.4.101`, PyTorch `2.12.1+cpu`, CPU |

YOLO11 trained through the application's real TCP training workflow. Its
profile records `model=yolo11`, `task=segment`, the selected native
`data.yaml`, a separate app-owned runtime `data.yaml`, the seed/checkpoint
hashes, and the bundled worker hash. The source contained zero cache files and
zero temporary directories before and after training.

## Common raster-mask result

All three checkpoints are scored against the same canonical test masks. Dice
and IoU are comparable here; YOLO mAP is deliberately excluded.

| Engine | Mean Dice | Mean IoU |
| --- | ---: | ---: |
| U-Net | 0.243091 | 0.156165 |
| YOLOv8-seg, confidence 0.25 | 0.721702 | 0.570198 |
| YOLO11-seg, confidence 0.25 | **0.773711** | **0.636553** |

| Class | U-Net Dice / IoU | YOLOv8 Dice / IoU | YOLO11 Dice / IoU |
| --- | ---: | ---: | ---: |
| contamination_spot | 0.000000 / 0.000000 | **0.742660 / 0.590660** | 0.732630 / 0.578072 |
| scratch_crack | 0.559132 / 0.388053 | 0.695835 / 0.533549 | **0.740663 / 0.588137** |
| missing_material | 0.343264 / 0.207193 | 0.855801 / 0.747948 | **0.909458 / 0.833950** |
| foreign_particle | 0.000000 / 0.000000 | 0.646248 / 0.477375 | **0.761197 / 0.614461** |
| extra_material_bridge | 0.313061 / 0.185579 | 0.667964 / 0.501461 | **0.724606 / 0.568143** |

Under this fixed synthetic same-source contract, YOLO11 has the highest mean
Dice/IoU. This is an engine benchmark only. No model is registered, adopted, or
made the inspection default by this result.

## Windows path-length regression closed

The first long evidence path reached the legacy Windows path boundary and made
the U-Net exporter fail before model comparison. The final implementation keeps
the full dataset and image SHA-256 values in manifests, while using a 24-character
dataset fingerprint folder and a collision-checked 32-character prediction-mask
name on disk. A deliberately longer real comparison then passed with a
221-character canonical image path and a 238-character prediction path. This
changes artifact path names only; evidence identity and comparison semantics are
unchanged.

## Verification and evidence

- Required isolated test build: 0 warnings, 0 errors.
- `openvisionlab_segmentation_prediction_export.py`: `py_compile` and
  `--self-test` passed.
- `--external-yolo-segmentation-canonical-export`: passed.
- `--wpf-segmentation-adapter-comparison`: passed.
- `--external-yolo-dataset-intake`: passed.
- `--segmentation-mask-comparison`: passed.
- `--priority-workflow-docs`: passed.
- Real YOLO11 30-epoch training:
  `artifacts\benchmark-external-yolo11-die-array-e30-20260722\summary.txt`.
- Final long-path common-mask comparison:
  `artifacts\benchmark-external-seg-adapter-compare-yolo11-e30-confidence025-test-pathfix2-20260722\summary.txt`.
- Prior fixed YOLOv8 comparison:
  `artifacts\benchmark-external-seg-adapter-compare-e30-confidence025-test-20260722\summary.txt`.

## Boundary / next dependency

The supplied packet is procedurally synthesized. This result proves local
YOLO11 segmentation training, immutable external-source handling, normalized
three-model comparison, and the disclosed same-source ranking. It does not
prove production-camera generalization, comparable latency, or a deployment
choice. The subsequent U-Net unweighted cross-entropy plus foreground soft-Dice
valid-only hypothesis completed and was rejected: macro Dice/IoU fell from the
selector baseline `0.204437/0.127053` to `0.189220/0.111142`, and one class
remained zero-overlap. Its held-out test stayed closed. Independent
camera/session masks remain required for any adoption decision.
