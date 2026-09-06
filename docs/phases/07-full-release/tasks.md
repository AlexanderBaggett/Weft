# Phase 07: Full-release completion and delivery

**Status:** Not started.

**Outcome:** Complete and deliver the entire accepted language design on both backends, with no missing first-release features.

**Prerequisites:** Completion evidence from [Phases 01–06](../README.md) and every addition accepted into the first-release scope. Release publication follows the designer's explicit acceptance.

**Feature coverage:** W001, W002, W003, W004, W005, W006, W007, W008, W009, W010, W011, W012, W013, W014, W015, W016, W017, W018, W019, W020, W021, W022, W023, W024, W025, W026, W027, W028, W029, W030, W031, W032, W033, W034, W035. See the [feature register](../../feature-status.md) for scope and the [phase index](../README.md) for primary ownership.

All tasks contribute to the first full release on both backends. Follow the [tracking rules](../README.md#tracking-rules). Resolve design choices with the designer while progressing independent engineering work.

## Full-scope completion audit

- [ ] P07-001 — Reconcile the current design documents, feature register, phase trackers, and accepted additions; account for every promised behavior and open design question. (W001–W035)
- [ ] P07-002 — Verify each feature's compiler/IR, .NET, JVM, diagnostics, test, and documentation work is complete with revision-specific evidence. (W001–W035)
- [ ] P07-003 — Verify all required intrinsics, production adapters, external bindings, and integrations exist and pass on the supported platform matrix. (W004, W005, W019–W031)
- [ ] P07-004 — Audit skipped/disabled tests, placeholder implementations, backend-only paths, and unsupported diagnostics to ensure none conceal an unfinished release feature. (W034)
- [ ] P07-005 — Classify and resolve known issues that violate language guarantees, feature completeness, or required operation; preserve an honest record of remaining non-blocking issues. (W001–W035)
- [ ] P07-006 — Incorporate any newly accepted design changes into the register and owning phases, reopen affected checks, and complete their implementation before continuing the release gate. (W001–W035)

## Release-candidate verification

- [ ] P07-007 — Produce reproducible release-candidate compiler/runtime/tooling artifacts from a recorded source revision with compatible dependency versions. (W003, W004, W032)
- [ ] P07-008 — Run the complete specified conformance suite on both backends and investigate every disagreement or incorrect shared result. (W034)
- [ ] P07-009 — Run the actual adapter integration suite, including cross-platform wire compatibility and failure/recovery cases. (W019–W031, W034)
- [ ] P07-010 — Run full reference-application load, resource, cancellation, shutdown, restart, and recovery checks. (W005, W034, W035)
- [ ] P07-011 — Verify clean installation, compilation, execution, debugging, inspection, and deployment using the candidate packages and published instructions. (W004, W032, W033, W035)
- [ ] P07-012 — Complete a final design/grammar/example/tooling documentation review against the candidate behavior and record corrections. (W001–W035)

## Designer acceptance and publication

- [ ] P07-013 — Prepare concrete release notes, complete feature coverage, compatibility/toolchain requirements, known issues, artifacts, and verification evidence for designer review. (W035)
- [ ] P07-014 — Obtain the designer's acceptance of the complete language experience and explicit approval to publish the reviewed release. (W035)
- [ ] P07-015 — Publish the approved versioned compiler/runtime/tooling packages, documentation, and reference examples to the agreed destinations. (W032, W035)
- [ ] P07-016 — Install the published artifacts in clean environments and verify both backends and representative complete applications. (W032, W035)
- [ ] P07-017 — Record the released revision, artifact locations, final evidence, and completed task/feature status; retain new ideas and later work without relabeling incomplete first-release work as complete. (W001–W035)

## Decisions and blockers

None recorded yet. Record the affected task IDs, the concrete semantic choice or blocker, the designer's decision when available, and any independent work that can continue.

## Evidence and next action

Implementation has not started. For completed work, record the task ID, revision or worktree state, relevant code/test paths, commands and results, and documentation changes. Keep partial backend progress explicit and record the next ready task for the following AI session.

The initial entry point is P07-001, subject to the prerequisite contracts above. Append newly accepted work with unused task IDs; preserve existing IDs and reopen tasks whose accepted contracts change.
