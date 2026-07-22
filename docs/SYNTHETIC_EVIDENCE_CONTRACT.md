# Synthetic-First Evidence Contract

## Purpose

OpenVisionLab Labeling Studio may complete and verify product features with
synthetic, procedurally generated, or augmentation-derived industrial images.
An unavailable production-camera dataset must not block labeling UX, adapter,
training connectivity, inference, comparison, provenance, or regression work.

This contract separates two questions:

1. **Does the product feature work reproducibly?** Synthetic evidence may prove
   this when every gate below passes.
2. **Will the trained model meet a factory acceptance target?** Synthetic
   evidence cannot prove this. Until an independent field packet is evaluated,
   record `Field validation: Deferred` or `Field validation: Not evaluated`.

Production accuracy is not claimed from synthetic evidence.

## Feature Completion Gates

A synthetic-data feature slice is `Complete` only when all applicable gates
pass:

- **Declared origin:** record generator/source identity, generation or export
  version, task, classes, image/annotation counts, and whether samples share a
  common parent image.
- **Locked evaluation:** record the split names, split manifest or content
  fingerprints, seed, and zero SHA-256 content overlap between the evaluated
  held-out split and train/validation where the workflow owns those splits.
- **Original immutability:** run on recipe-owned/runtime copies when a model may
  create cache files; record the source tree SHA-256 before and after.
- **Runtime provenance:** record engine/profile, executable or Python runtime,
  repository/package version, checkpoint path and SHA-256, task, image size,
  batch, epoch, confidence/threshold, and relevant device information.
- **Comparable result:** use the same canonical labels/masks and evaluator for
  compared models. Record per-class failures and timing conditions, not only a
  single aggregate score.
- **Non-adopting decision:** a cross-engine synthetic result is
  `comparisonKind=engine-benchmark`. It may recommend what to test next but must
  not automatically register, promote, or replace the inspection model.
- **Durable evidence:** save the command, summary/report path, relevant hashes,
  and pass/fail result in project documentation or an evidence artifact.

Validation-only tuning, duplicate-rich data, an unlocked test split, missing
provenance, or an altered source tree fails the applicable gate. It is not
repaired by describing the result more confidently.

## Optional Field Validation Gate

Field validation is a separate, optional adoption gate. Run it only when an
operator supplies and approves an independently acquired camera/session packet
with:

- part/product, camera/lens, lighting, date/session, and relationship to the
  training source;
- trustworthy task labels: boxes, masks, or image-level OK/NG as appropriate;
- a fixed held-out split with zero SHA-256 content overlap with training and
  validation;
- an agreed false-positive/false-negative, class, latency, and threshold
  acceptance rule.

Absence of this packet does not reopen or block a feature whose synthetic
completion gates passed. It only keeps factory generalization, threshold
calibration, and production adoption `Not evaluated`.

## Reusable Completion Record

```text
Status: Complete | Incomplete | Blocked
Scope: <feature behavior proved by this slice>
Evidence origin: synthetic | procedural | augmented | field
Acceptance criteria: <gate -> pass/fail evidence>
Verification: <commands/tests actually run and result>
Evidence: <manifest/report/log/artifact path and hashes>
Field validation: Deferred | Not evaluated | Passed | Failed
Boundary / next dependency: <claims this evidence does not make>
```

## Example Decision

```text
Status: Complete
Scope: YOLOv5/YOLOv8 adapter comparison on one canonical detection recipe
Evidence origin: synthetic
Acceptance criteria: fixed test manifest -> pass; no content overlap -> pass;
  source tree hash unchanged -> pass; normalized metrics/timing -> pass
Verification: focused adapter test, real one-epoch connectivity, comparison run
Evidence: comparison-summary.json plus runtime/checkpoint/source SHA-256
Field validation: Not evaluated
Boundary / next dependency: engine-benchmark only; no production accuracy or
  automatic model adoption claim
```
