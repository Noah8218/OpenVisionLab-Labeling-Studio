# Release Notes

## Unreleased

Current focus:

- Local industrial labeling workflow for object detection and segmentation.
- Independent object-detection test evidence for YOLOv5/YOLOv8 accuracy and model-Takt comparison.
- Independent production/cross-session anomaly-classification runtime evidence.
- YOLOv8 segmentation data/model operating quality.
- Clear separation between saved labels, AI candidates, trained model candidates, and the current inspection model.
- Compact WPF workflow layout for dataset, labeling, candidate review, training, and model center screens.

Recent verified areas:

- Local YOLOv5/YOLOv8 Detect comparison with separate runtimes, test-preferred/validation-reference split handling, and Candidate Review metrics/Takt presentation.
- Dataset-purpose-aware YOLOv8 Detect/SEG/Classification weight selection when connecting a local runtime folder.
- Segmentation brush/polygon save, reopen, and training-export paths.
- YOLOv8 segmentation local runtime plumbing and model-comparison safeguards.
- Image queue usability and save-before-navigation protection.
- Candidate Review wording and rejected-model adoption guard.
- README, release-note, CI, and known-limitations documentation skeleton.
- `Library-Noah` source-project dependency removed from the app/test build path in favor of checked-in DLL references.

Not a release claim:

- The current object-detection comparison uses validation with one NG object because the test split is empty; it is not model-adoption evidence.
- Production YOLOv8 segmentation accuracy still requires held-out evaluation on real labeled datasets.
- Anomaly detection remains an active workflow area, not a completed product mode.
- Updating the checked-in `Lib.*` DLLs still needs an intentional binary refresh and build verification.
