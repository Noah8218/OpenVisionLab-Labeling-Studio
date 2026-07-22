# U-Net 30-epoch class-confusion analysis (2026-07-22)

Status: Complete

## Scope

Read-only diagnosis of the existing 30-epoch U-Net checkpoint on the fixed
EasyMatch Die Array canonical `valid` split. This run creates only a new
app-owned prediction artifact. It does not retrain, change the original native
source, alter the held-out `test` split, select a production model, or treat
the YOLO confidence result as a U-Net control.

## Fixed inputs

- Canonical export: the immutable five-class 360 train / 80 valid / 60 test
  artifact with dataset fingerprint
  `f29c6b16d401d5fb18356220c0952928f5477c318188ccff76548d903ea63cab`.
- Checkpoint: `C:\Git\unet\runs\segment\openvisionlab-unet-external-die-array-e30-20260721-203302\weights\best.pt`
  (`487EF0EE70FD3A37260F4D9CB17C12994FC747575D1984EC8C64BD65967C6F72`).
- Prediction artifact:
  `artifacts\unet-e30-class-confusion-20260722\unet-valid-predictions\prediction-manifest.jsonl`.

## Evidence

The training masks are extremely pixel-imbalanced. Background occupies
94,166,351 of 94,371,840 train pixels (`99.7823%`); each defect class occupies
only `0.0302%` to `0.0559%`.

| True valid class | True pixels | True images | Predicted as same class | Predicted as background | Dominant error |
| --- | ---: | ---: | ---: | ---: | --- |
| contamination_spot | 14,269 | 11 | 0 | 13,939 | background (97.6873%) |
| scratch_crack | 10,562 | 12 | 3,141 | 5,348 | background (50.6343%) |
| missing_material | 12,409 | 11 | 2,084 | 10,053 | background (81.0138%) |
| foreign_particle | 4,702 | 8 | 0 | 1,841 | extra_material_bridge (39.8341%) |
| extra_material_bridge | 4,519 | 7 | 1,168 | 2,251 | background (49.8119%) |

Additional signals:

- The U-Net never predicts `contamination_spot` on any validation image and
  never predicts `foreign_particle` on any validation image.
- `foreign_particle` is also confused with `scratch_crack` (988 pixels) and
  `extra_material_bridge` (1,873 pixels).
- The worker uses unweighted `torch.nn.CrossEntropyLoss()` and selects
  `best.pt` solely by validation cross-entropy loss. The U-Net comparison
  exporter uses raw softmax `argmax`; its recorded confidence is not a pixel
  rejection threshold. Therefore the YOLO `0.25` remediation cannot repair
  this U-Net failure mode.

## One controlled remediation hypothesis

Replace the unweighted U-Net cross-entropy with **train-mask-derived,
class-weighted cross-entropy**. The weights must be calculated from the fixed
train masks only, persisted with the run configuration, and held fixed for the
run. Validation must select the candidate using foreground macro Dice plus the
per-class no-collapse gate (each supported class must receive at least one
prediction) rather than validation loss alone.

This is deliberately one hypothesis: do not mix crop sampling, augmentation,
architecture changes, data relabeling, class merging, or test-split threshold
tuning into that first run.

## Controlled experiment

The temporary worker experiment derived weights after resizing each **train**
mask with nearest-neighbour interpolation to the requested network image size.
With `B` as the background pixel count, `F` as the sum of all foreground
pixels, and `C_i` as one foreground-class count, its fixed weights were:

- `w_background = F / B`
- `w_class_i = median(C_1 ... C_n) / C_i`

Every configured class had to exist in train, and `best.pt` selection used
valid no-collapse, then foreground macro Dice, then weighted validation loss.
The experiment checkpoint recorded the weights/counts and validation state so
the outcome can be reproduced.

## Result and decision

Status: Complete

Scope: run exactly one CUDA 30-epoch class-weighted experiment on the fixed
360 train / 80 valid packet, compare its app-owned valid raster predictions
with the fixed unweighted baseline, and decide whether it may change the
default worker. No held-out test evaluation was run.

Acceptance criteria:

- Train/valid input stayed immutable: passed. Its four train/valid subtrees had
  SHA-256 `D5C48F94E25591BFF53A9AC9EB687D8CC69F017C119D486D397E8483AC8FFD96`
  before and after the run.
- The temporary class-weighted run completed 30 CUDA epochs and produced a
  valid-only prediction artifact: passed. Its selected checkpoint was epoch
  30 with in-worker foreground macro Dice `0.073260` and no-collapse `false`.
- The common 80-image valid raster comparison improved foreground quality and
  cleared all-class no-collapse: failed. Unweighted baseline macro Dice/IoU
  was `0.164849` / `0.097209`; class-weighted macro Dice/IoU was `0.074135` /
  `0.040426` (Dice delta `-0.090714`). Both runs failed no-collapse, and
  `contamination_spot` remained at zero predicted pixels.

Decision: reject this exact class-weight formula and selection policy as a
default product behavior. The worker has been restored to unweighted
cross-entropy and validation-loss checkpoint selection. No held-out test is
authorized by this result.

Evidence: baseline `artifacts\unet-e30-class-confusion-20260722\unet-valid-predictions`; experiment `C:\Git\unet\runs\segment\openvisionlab-unet-classweighted-validonly-e30-20260722-083735`; new valid artifact `artifacts\unet-classweighted-e30-valid-20260722\unet-valid-predictions`.

Boundary / next dependency: do not retry this formula or mix it with crops,
augmentation, architecture changes, relabeling, class merging, or test tuning.
Any new U-Net hypothesis needs a separately declared valid-only acceptance
criterion. Independent camera/session segmentation data remains required for
a production decision.

## Foreground-centered crop experiment

Status: Complete

Scope: test one deterministic train-only foreground-crop policy without
changing labels, native source data, validation inputs, the held-out test
split, or the default U-Net worker behavior.

The plan kept the 180 normal train images as full frames. Of the 180
foreground images, 164 received a 320px crop centered on the full foreground
bounds; 16 whose combined foreground exceeded the crop remained full-frame to
avoid dropping labels. All five classes were represented in the crop manifest.

Acceptance criteria:

- A one-epoch CUDA integration run recorded the crop manifest/policy in its
  output checkpoint and preserved the train/valid input SHA-256: passed.
- The fixed 30-epoch CUDA run preserved the same train/valid SHA-256
  `D5C48F94E25591BFF53A9AC9EB687D8CC69F017C119D486D397E8483AC8FFD96`
  before/after and produced an app-owned 80-image valid prediction artifact:
  passed.
- The selected checkpoint improved valid macro Dice above `0.164849`, had real
  overlap for all five classes, and avoided all-image false-positive flood:
  failed. It predicted background only; valid macro Dice/IoU was `0.0` /
  `0.0`.

Interpretation: this does not prove that crop geometry is harmful. The
unchanged validation cross-entropy selector chose epoch 29 with validation
loss `0.016890`, even though it predicted no foreground pixels. Therefore the
selector, not crop quality alone, is a confounder for this experiment.

Decision: remove the experimental crop path from the worker rather than leave
an unproven hidden option. Do not run held-out test evaluation. Before another
crop experiment, validate a foreground-quality checkpoint selector on the
unchanged full-frame baseline as a separate hypothesis.

Evidence: one-epoch run
`C:\Git\unet\runs\segment\openvisionlab-unet-foregroundcrop-validonly-e1-20260722-095720`; 30-epoch run
`C:\Git\unet\runs\segment\openvisionlab-unet-foregroundcrop-validonly-e30-20260722-095828`; valid prediction artifact
`artifacts\unet-foregroundcrop-e30-valid-20260722\unet-valid-predictions`.

## Foreground-quality checkpoint selection experiment

Status: Incomplete

Scope: keep full-frame images, unweighted cross-entropy, optimizer, seed,
train/valid split, and 30-epoch budget unchanged; change only the opt-in
checkpoint selection rule. The rule prefers all-five-class real overlap,
then higher foreground macro Dice, then lower validation loss.

Completed evidence:

- Python self-test, isolated build, and a one-epoch CUDA integration run passed.
  The checkpoint records per-class Dice, true/predicted/intersection pixel
  counts, predicted-image counts, macro Dice, overlap gate, and selector ID.
- One full-frame/unweighted 30-epoch CUDA run completed and preserved
  train/valid SHA-256
  `D5C48F94E25591BFF53A9AC9EB687D8CC69F017C119D486D397E8483AC8FFD96`
  before/after.
- The selector chose epoch 29. The app-owned 80-image valid raster comparison
  improved macro Dice/IoU from `0.164849` / `0.097209` to `0.204437` /
  `0.127053`; no class was predicted on all 80 images.

Failed criterion:

- All five classes must have real valid overlap: failed. Both
  `contamination_spot` and `foreign_particle` remained at Dice `0.0` and zero
  intersection pixels. Therefore the selector is not eligible as the default
  training policy and the held-out test remains closed.

Boundary / corrective action: retain the flag only as an internal opt-in
validation harness; the normal TCP worker still uses validation-loss selection.
Before another data-sampling experiment, isolate one loss/architecture
hypothesis that can recover the two zero-overlap classes while this harness
measures the unchanged valid split.

Evidence: run
`C:\Git\unet\runs\segment\openvisionlab-unet-foregroundselection-validonly-e30-20260722-102225`; valid artifact
`artifacts\unet-foregroundselection-e30-valid-20260722\unet-valid-predictions`.
