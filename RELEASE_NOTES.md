# Release Notes

## Unreleased

Current focus:

- Local industrial labeling workflow for object detection and segmentation.
- YOLOv8 segmentation training/inference/model-comparison operating quality.
- Clear separation between saved labels, AI candidates, trained model candidates, and the current inspection model.
- Compact WPF workflow layout for dataset, labeling, candidate review, training, and model center screens.

Recent verified areas:

- Segmentation brush/polygon save, reopen, and training-export paths.
- YOLOv8 segmentation local runtime plumbing and model-comparison safeguards.
- Image queue usability and save-before-navigation protection.
- Candidate Review wording and rejected-model adoption guard.
- README, release-note, CI, and known-limitations documentation skeleton.
- `Library-Noah` source-project dependency removed from the app/test build path in favor of checked-in DLL references.

Not a release claim:

- Production YOLOv8 segmentation accuracy still requires held-out evaluation on real labeled datasets.
- Anomaly detection remains an active workflow area, not a completed product mode.
- Updating the checked-in `Lib.*` DLLs still needs an intentional binary refresh and build verification.
