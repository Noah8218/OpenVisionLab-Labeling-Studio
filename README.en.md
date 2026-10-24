# OpenVisionLab Labeling Studio

[English](README.en.md) | [한국어](README.md)

A local Windows desktop workbench for preparing industrial image datasets, labeling, training, comparing models, and running inference. The application uses .NET 8 and WPF and is intended for a single operator.

![Smart Mask candidate generation, explicit application and label saving](docs/tutorial/images/industrial-workflow/labeling-smart-mask.gif)

Regenerate a candidate from an existing screw mask, review the popup, apply it, then save the label. Long review pauses are trimmed; calculation and actions keep their recorded timing. [Watch the MP4](docs/tutorial/images/industrial-workflow/labeling-smart-mask.mp4) · [View the candidate popup](docs/tutorial/images/industrial-workflow/labeling-smart-mask-poster.png)

## Quick overview

Keep images, class order, saved ground truth, dataset splits, and model history in one recipe. Model adapters convert the same ground truth into each supported training format.

| Task | Ground truth and tools | Connected model families |
| --- | --- | --- |
| Object detection | Axis-aligned boxes; two-point drag or four extreme points | YOLOv5, YOLOv8, YOLO11 |
| Segmentation | Polygons and pixel masks; polygon, brush, eraser, and Smart Mask tools | YOLOv8-seg, YOLO11-seg, U-Net |
| Anomaly detection | Reviewed image-level OK/NG decisions; localization is optional | YOLOv8/YOLO11 classification, PatchCore pilot |

**Saved labels, AI candidates, trained models, and the inspection model are separate states.** Generating a result or finishing training does not save ground truth or replace the inspection model. Local functional evidence does not establish production accuracy, long-run reliability, or takt time.

## Quick start

1. Create a recipe and select object detection, segmentation, or anomaly detection.
2. Select the source image folder and a separate recipe output folder. Check the class names and order.
3. Open Labeling, annotate or review images, and save the labels or OK/NG decisions.
4. Open Training, select the model runtime, run Dataset Health, and check the training settings before starting.
5. Compare trained results under the same evaluation conditions. Explicitly set the inspection model and save the settings.
6. Open Inference, check the model and images, then run inference. Review AI candidates; confirming a candidate and saving labels are separate actions.

See the [step-by-step manual](docs/tutorial/README.md) for the full workflow.

## Labeling workflows

### Object detection

Draw a box around the actual object or defect, check its class and bounds, then select **Save labels**. For images without a target, explicitly mark **No object**. Four-point input uses top → bottom → left → right to create an ordinary axis-aligned box.

![A bent pin annotated with an object-detection box](docs/tutorial/images/industrial-workflow/object-detection.png)

### Segmentation and Smart Mask

Use polygons, the brush, or the eraser to describe the boundary. In a segmentation recipe, enable **Automatic contour** in **Labeling options** once. Drawing a starting box then generates a MobileSAM candidate.

If correction is needed, add inclusion or exclusion points one at a time, select **Regenerate candidate**, and compare **Previous candidate** with **Current candidate**. Select **Confirm** for the displayed candidate or **Skip** to discard it. Confirmation updates the working annotation; **Save labels** writes it to ground-truth files. Candidate generation and switching do not save files.

The recipe restores the option when reopened. Restoring it does not run inference, confirm candidates, or save labels.

![Saved boundary mask for the screw body and head](docs/tutorial/images/industrial-workflow/segmentation.png)

### Anomaly detection

Use **Normal (OK) → Next** or **Anomaly (NG) → Next** to save the image decision and advance to the next unreviewed image. Boxes and masks are optional.

![Manual OK/NG decisions for straight and bent pin images](docs/tutorial/images/industrial-workflow/anomaly-ok-ng-review.png)

YOLOv8/YOLO11 classification uses both OK and NG examples for supervised two-class learning. The bounded PatchCore pilot trains on reviewed-normal images and returns an image decision, score/threshold, unconfirmed location candidates, and a heatmap. Select **View heatmap** to open read-only review; this does not change labels, candidates, or the model. Evaluate production quality separately on held-out field data that was not used for training or validation.

## One recipe, multiple models

A recipe shares source images, canonical class order, ground truth, train/valid/test membership, and data/configuration/model provenance with SHA-256 identities. Adapters reuse this information instead of requiring a separately edited label set for each model.

Saving a recipe or starting training records a content-based `dsv2-...` dataset version from image and label contents, class order, and split membership. Identical content reuses its version; changed labels or splits create a new version. The Dataset view shows the recorded version, and trained-model history records the version used. This is not a full copy of the source dataset.

Compare models only when dataset identity (SHA-256), classes, splits, and evaluation settings agree. Different conditions are reported as differences rather than proof that one model is better.

In **Training → Runtime**, selecting a registered model switches the Python executable, runtime folder, and communication script together. Select the runtime folder if initial discovery fails. A model is supported through its registered adapter and compatible class/split/output mapping; an arbitrary GitHub model is not supported merely by its name.

## Installation

- Windows 10/11 x64.
- .NET SDK **8.0.421**, pinned by [global.json](global.json), for source builds; PowerShell for the provided scripts.
- A graphics driver that supports the framebuffer functions required by the SharpGL viewer. Basic or virtualized displays can be incompatible.
- Model-specific local Python environments and weights for training, inference, and MobileSAM assistance. These assets may require separate preparation and are not downloaded without approval.

Keep the repository's `dll`, local `OpenVisionLab` libraries, and `third_party` package assets when building. The project consumes the checked-in OpenVisionLab Core/Vision2D SDK assemblies and its aligned OpenCvSharp managed/native runtime; do not replace them with unrelated binaries. See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Keep personal runtime paths in `config/labeling-runtime.local.json`, outside committed configuration. A self-contained published bundle includes the .NET runtime; it still needs compatible graphics and any selected model runtime.

## Build Command

Open `OpenVisionLab.LabelingStudio.sln` in Visual Studio, select `OpenVisionLab.LabelingStudio` as the startup project, and build for x64. The other solution projects contain shared libraries and the test runner.

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.csproj -c Release -p:Platform=x64
```

## Running

For a Debug build:

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.sln -c Debug -p:Platform=x64
.\scripts\start-labeling-workbench.ps1 -AppMode Debug
```

To publish and run a local Release bundle:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 -Configuration Release
.\scripts\start-labeling-workbench.ps1 -AppMode Publish
```

The versioned self-contained folder is `artifacts\publish\Release\win-x64\0.3.4`. The portable ZIP contains this verified folder; it is not an installer or a signed package. Extract it into a new folder, verify the manifests, then run `OpenVisionLab.LabelingStudio.exe`. The release and publish manifests record source/build identity and the full payload's SHA-256 hashes.

## Smoke Command

After publishing, verify the existing package without changing it:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 -Configuration Release -ReleaseVersion 0.3.4 -VerifyOnly
```

Then run the read-only environment check:

```text
OpenVisionLab.LabelingStudio.exe --environment-self-test --json
```

## Headless Environment Self-Test

The environment check runs before WPF startup and emits one JSON document. It does not save labels, recipes, models, or a support bundle. It cannot validate the Main Viewer's graphics context without opening that viewer.

Because the product is a Windows GUI executable, PowerShell automation should use `Start-Process -Wait` with redirected standard output. Exit codes are `0` (ready), `2` (needs attention), `64` (invalid arguments), and `70` (check failed). This product CLI does not provide training or inference commands.

## Sample data

| Location | Purpose |
| --- | --- |
| `datasets/object-detection/coco128/coco128/README.txt` | Object-detection sample information |
| [samples/python_protocol/README.md](samples/python_protocol/README.md) | Python TCP protocol and mock-client examples |
| `docs/tutorial/images` | README and tutorial media |

Keep source images separate from recipe outputs. Reuse the same saved ground truth and splits when experimenting with different models.

## CI

[The Windows CI workflow](.github/workflows/ci.yml) defines trigger filters, README and documentation checks, a fixed-source test build, regression evidence, and whitespace checks. A newer run cancels an older run for the same branch.

The manual `workflow_dispatch` option `full_regression=true` additionally runs the full regression suite and versioned self-contained publish/package checks, then uploads the package artifact. GPU checks require suitable runtime, weights, and data; unavailable prerequisites are recorded as skipped. The workflow definition alone is not evidence of a successful hosted run.

## Documentation

| Document | Contents |
| --- | --- |
| [Step-by-step manual](docs/tutorial/README.md) | Dataset setup, labeling, review, training, and recovery procedures |
| [Visual guide](docs/tutorial/labeling-workbench-tutorial.html) | A walkthrough with navigation, screen references, and troubleshooting |
| [Single-file guide](docs/tutorial/labeling-workbench-tutorial-standalone.html) | A portable HTML manual with embedded images |

The manuals identify their historical screen dates; those images do not prove the appearance of the current build.

## Version management

This project uses explicit versions. The canonical source is `VersionPrefix` in [Directory.Build.props](Directory.Build.props); package and display versions derive from it. Current source version: **0.3.6** (2026-10-24). Previous Public version: **0.3.5**.

| Version | Date | Changes |
| --- | --- | --- |
| 0.3.6 | 2026-10-24 | Preserved Smart Mask selection during candidate regeneration, fixed WPF tab keyboard handling, and refreshed actual industrial workflow media while retaining file formats and explicit apply/save boundaries. |
| 0.3.5 | 2026-09-16 | Hardened annotation load/save/recovery, Object Review, detection/training transitions, dataset quality and model provenance, and Python failure handling while preserving public file formats. |
| 0.3.4 | 2026-09-15 | Moved manually distributed WPF Shell responsibilities into feature-specific View, ViewModel, adapter, and lifecycle owners while preserving save, review, and training workflows and public annotation/recipe/dataset-version/model-state formats. |
| 0.3.3 | 2026-09-08 | Consolidated WPF Shell, Canvas, Queue, and Model responsibilities into concrete owners; recorded call paths, state ownership, and documentation boundaries while preserving storage formats and model states. |
| 0.3.2 | 2026-09-07 | Consolidated Shell partial and internal WPF responsibilities into concrete owners and strengthened asynchronous shutdown/lifetime boundaries; preserved storage formats and model states. |
| 0.3.1 | 2026-09-04 | Moved Shell partial responsibilities to concrete owners and strengthened command/callback shutdown boundaries; preserved storage formats and model states. |
| 0.3.0 | 2026-08-31 | Organized the integrated Studio workflow and WPF review owners while preserving save and review behavior. |
| 0.2.1 | 2026-08-25 | Prepared the minimal WPF workspace and existing annotation behavior as a compatible PATCH candidate. |
| 0.2.0 | 2026-08-24 | Brought bounded WPF presentation and localization candidates into Public source. |

Ordinary development commits do not require a version increase. Each reviewed original/main promotion uses a new product version. Compatible fixes use PATCH, compatible features use MINOR, and breaking changes use MAJOR. Published versions remain immutable; source pushes, tags, and release publication have separate verification and approval boundaries.

## Release Notes

See [RELEASE_NOTES.md](RELEASE_NOTES.md) for user-facing changes.

## Roadmap

Remaining deployment work depends on an approved clean Windows target and installer/signing decisions. Field adoption requires separate accuracy, long-run reliability, and takt-time evaluation using approved production data and target hardware.

## Known Limitations

- There is no installer, code-signing package, or completed clean-PC install/upgrade/uninstall validation. Local package checks do not establish that lifecycle.
- Diagnostics use local user storage and explicit support-bundle export; there is no telemetry, cloud support, or automatic upload.
- Recipe archive export includes the last saved recipe and full dataset with SHA-256 verification. Unsaved labels, unconfirmed candidates, and active jobs block export. Import uses a new location without overwriting existing data or automatically applying the recipe.
- Crash recovery covers one current image's unsaved boxes, segmentation, and review metadata. Restore/discard is explicit; candidates are excluded and labels still require saving. Drafts older than seven days or with mismatched recipe/dataset/image identity are not used.
- An interrupted explicit multi-file save recovers the last completed state on restart. Corrupt or inaccessible recovery records block workspace entry and produce logs. Process-termination checks do not prove resilience to power loss or storage failure.
- Anomaly AI decisions remain unconfirmed candidates. Only manual OK/NG review or explicitly approved folder-review results become training ground truth. Legacy review files cannot recover missing AI/user provenance.
- PatchCore is a bounded normal-only pilot. Synthetic or small-sample functional checks do not establish field quality; evaluate held-out production data separately.
- MobileSAM supports one starting box per object with multiple inclusion/exclusion points. Bounded tests covered exact metadata boxes, 20% expansion, 10% shrinkage, and 10% diagonal shifts; larger errors, arbitrary shapes, automatic multi-object separation, and field accuracy are unverified. Automatic confirmation/save and text/voice-based labeling are unsupported.
- Cloud accounts, team permissions, deployment pipelines, and camera/PLC control are outside the current product scope.
- Full HTML/WPF visual concordance and brightness input/reset remain unverified. Targeted checks and observed workflows do not establish full-regression success.


## License

Distributed under the [MIT License](LICENSE). Preserve the copyright and license notices in [LICENSE](LICENSE), [NOTICE](NOTICE), and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) when copying or distributing the software.

Copyright (c) 2026 최노아 (Noah-Choi)
