# Build and runtime guide

[English README](../../README.md) · [한국어 README](../../README.ko.md) · [Release notes](../../RELEASE_NOTES.md)

Build and run the app, verify a package, or check its environment with the commands below. Run them from the repository root. The versioned examples below use **0.3.8**; [Directory.Build.props](../../Directory.Build.props) owns the product version and [global.json](../../global.json) pins the SDK.

## Build Command

Open `OpenVisionLab.LabelingStudio.sln` in Visual Studio, select `OpenVisionLab.LabelingStudio` as the startup project, and build for x64. The other solution projects contain shared libraries.

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.csproj -c Release -p:Platform=x64
```

## Running

Debug build and launch:

```powershell
dotnet build .\OpenVisionLab.LabelingStudio.sln -c Debug -p:Platform=x64
.\scripts\start-labeling-workbench.ps1 -AppMode Debug
```

Publish and run a local Release bundle:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 -Configuration Release
.\scripts\start-labeling-workbench.ps1 -AppMode Publish
```

The versioned self-contained folder is `artifacts\publish\Release\win-x64\0.3.8`. Extract the portable ZIP into a new folder, verify the manifests, then run `OpenVisionLab.LabelingStudio.exe`. The release and publish manifests record source/build identity and the full payload's SHA-256 hashes.

## Package verification

Verify the existing package without changing it:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 -Configuration Release -ReleaseVersion 0.3.8 -VerifyOnly
```

## Environment check CLI

```text
OpenVisionLab.LabelingStudio.exe --environment-self-test --json
```

The read-only check runs before WPF startup and emits one JSON document. For PowerShell automation, use `Start-Process -Wait` with redirected standard output. Exit codes are `0` (ready), `2` (needs attention), `64` (invalid arguments), and `70` (check failed).

## CI

[Windows CI](../../.github/workflows/ci.yml) checks the README, builds the Windows application, and checks whitespace. See the workflow for the checks and artifacts included in each run. GPU-dependent validation requires the selected runtime, weights, and data.

Sample information: `datasets/object-detection/coco128/coco128/README.txt`; [Python TCP protocol and mock-client examples](../../samples/python_protocol/README.md).


## Launcher configuration

The launcher reads `config/labeling-runtime.local.json` when present and otherwise uses [config/labeling-runtime.example.json](../../config/labeling-runtime.example.json). Set personal paths in the uncommitted local file.

The example's `labelingApp.publishExecutable` still names a `0.1.0` folder. Before using `-AppMode Publish`, point the local setting to the versioned bundle you actually built. The Debug example points to `artifacts/run/Debug/OpenVisionLab.LabelingStudio.exe`. The `-StartYolo` and `-StartPythonUi` switches start the YOLO runtime and Python UI, respectively. `-CheckYolo` runs the configured Python client with `--smoke-test` before launching the app; missing prerequisites or a failed check stop the launch.

## Limitations

- There is no installer, code-signing package, or completed clean-PC install/upgrade/uninstall validation. A self-contained bundle includes .NET, but still needs compatible graphics and the selected model runtime. Basic or virtualized displays can be incompatible.
- Model runtimes and weights may require separate preparation and are not downloaded without approval. Support requires a registered adapter with compatible class/split/output mapping; an arbitrary GitHub model is not supported merely by its name.
- Local functional evidence does not establish production accuracy, long-run reliability, or takt time. A CI definition alone does not establish a successful hosted run. The manuals identify historical screen dates, not the appearance of the current build.
- Diagnostics use local user storage and explicit support-bundle export; there is no telemetry, cloud support, or automatic upload. The environment CLI cannot validate the Main Viewer's graphics context without opening that viewer and provides no training or inference commands.
- Recipe archive export includes the last saved recipe and full dataset with SHA-256 verification. Unsaved labels, unconfirmed candidates, and active jobs block export. Import uses a new location without overwriting existing data or automatically applying the recipe. Dataset versioning does not copy the full source dataset.
- Crash recovery covers one current image's unsaved boxes, segmentation, and review metadata. Restore/discard is explicit; candidates are excluded and labels still require saving. Drafts older than seven days or with mismatched recipe/dataset/image identity are not used.
- An interrupted explicit multi-file save recovers the last completed state on restart. Corrupt or inaccessible recovery records block workspace entry and produce logs. Process-termination checks do not prove resilience to power loss or storage failure.
- Anomaly AI decisions remain unconfirmed candidates. Only manual OK/NG review or explicitly approved folder-review results become training ground truth. Legacy review files cannot recover missing AI/user provenance.
- PatchCore is a bounded normal-only pilot. Synthetic or small-sample functional checks do not establish field quality; evaluate held-out production data separately from training and validation data.
- MobileSAM supports one starting box per object with multiple inclusion/exclusion points. Bounded tests covered exact metadata boxes, 20% expansion, 10% shrinkage, and 10% diagonal shifts; larger errors, arbitrary shapes, automatic multi-object separation, and field accuracy are unverified. Automatic confirmation/save and text/voice-based labeling are unsupported. The recipe restores the option when reopened. Restoring it does not run inference, confirm candidates, or save labels.
- Cloud accounts, team permissions, deployment pipelines, and camera/PLC control are outside the current product scope.
