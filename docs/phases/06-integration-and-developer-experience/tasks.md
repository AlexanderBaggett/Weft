# Phase 06: Whole-language integration and developer experience

**Status:** Not started.

**Outcome:** Make the full language usable through complete reference applications, diagnostics, editing/debugging tools, packaging, and verified feature interactions.

**Prerequisites:** Work starts with stable interfaces from [Phase 01](../01-design-and-compiler-foundation/tasks.md) and advances alongside [Phases 02–05](../README.md). Final phase completion requires the full feature implementations and real adapters.

**Feature coverage:** W004, W018, W032, W033, W034, W035. See the [feature register](../../feature-status.md) for scope and the [phase index](../README.md) for primary ownership.

All tasks contribute to the first full release on both backends. Follow the [tracking rules](../README.md#tracking-rules). Resolve design choices with the designer while progressing independent engineering work.

## Project and command-line tooling

- [ ] P06-001 — Complete weft.toml project/source discovery, rulesets, dependency/exposes bindings, adapters, configuration, and test profiles. (W032)
- [ ] P06-002 — Complete check/build/run/emit commands, backend selection, generated artifacts, incremental build behavior, and actionable toolchain errors. (W004, W032)
- [ ] P06-003 — Implement compiler/runtime and separate-project dependency resolution, public Weft contract metadata paired with matching DLL/jar artifacts, version compatibility checks, reproducible build inputs, and installation/update packaging. (W032)
- [ ] P06-004 — Complete source/clock/random/switch substitutions and test profiles with deterministic, isolated behavior. (W031, W032, W034)
- [ ] P06-005 — Verify installation and clean builds/runs for both targets from the documented supported environments. (W032, W035)

## Inspection, editing, and debugging

- [ ] P06-006 — Complete diagnostic presentation with source spans, contextual explanations, related declarations, and machine-readable output. (W003, W033)
- [ ] P06-007 — Complete rule/trigger inspection, matched-site and inserted-effect explanations, runtime residue/cost reporting, and broad-scope diagnostics. (W007, W008, W033)
- [ ] P06-008 — Implement graph visualization/inspection with origin/receiver paths, providers/requirements, dynamic admissible sets, and counterexamples. (W009, W033)
- [ ] P06-009 — Complete flag/kill/canary usage, age, state-contract, metric, and operational reporting supported by the language design. (W014–W016, W033)
- [ ] P06-010 — Implement formatting for all language/rule/manifest forms with stable handling of comments and incomplete source. (W001, W032, W033)
- [ ] P06-011 — Implement language-server syntax/semantic diagnostics, symbols, navigation, and policy-at-site information; keep results current during editing. (W003, W033)
- [ ] P06-012 — Complete .NET source/debug mappings and verify stepping, breakpoints, exceptions, and generated effects against Weft source. (W004, W033)
- [ ] P06-013 — Complete JVM source/debug mappings and verify the same source-level debugging scenarios. (W004, W033)

## Reference applications and feature interactions

- [ ] P06-014 — Turn the orders example into a complete executable reference service using services, middleware, provenance/rules/triggers, persistence, messaging, caches, switches, and effect scopes. (W001–W035)
- [ ] P06-015 — Add executable examples for custom origins, streaming, connection lifetimes, channels/timers, signals/watchers, gRPC, and raw network protocols. (W019–W030, W035)
- [ ] P06-016 — Add focused applications for concurrency modes, structured task lifetime, transaction/outbox recovery, retries, and sagas/compensation. (W005, W011, W017, W035)
- [ ] P06-017 — Verify provenance/policies through services, triggers, caches, serialization, helpers, and generated calls with both accepted and rejected programs. (W006–W008, W013, W018)
- [ ] P06-018 — Verify scoped values/ambients across tasks, channels, streams, connections, and process shutdown. (W005, W010–W012, W019–W030)
- [ ] P06-019 — Verify provider guarantees under interacting flags, canaries, kills, tables, next any, and bounded reentrancy. (W009, W014–W016)
- [ ] P06-020 — Verify guard/after behavior for rejection, exceptions, cancellation/deadlines, committed responses, and stream/connection completion. (W005, W009, W017, W020–W022)
- [ ] P06-021 — Verify cache consistency around commit/rollback, outbox delivery, broker redelivery, and external invalidation. (W013, W017, W023, W024, W031)
- [ ] P06-022 — Verify shadow isolation, rollout/rollback, retry eligibility, external effects, and compensation under failures and configuration changes. (W014–W017)
- [ ] P06-023 — Audit the acceptance corpus against every feature and documented subfeature, filling gaps in positive, negative, failure, and interaction coverage. (W034)

## Performance, documentation, and completion

- [ ] P06-024 — Measure reproducible compile-time/runtime workloads on both platforms, including analysis scaling, generated code size, allocation, throughput, and latency. (W004, W005, W034)
- [ ] P06-025 — Run sustained load, resource-leak, cancellation, overload, shutdown, and recovery checks across the real adapter stacks. (W005, W019–W031, W034)
- [ ] P06-026 — Improve diagnosed compiler/runtime bottlenecks while maintaining conformance and rerun affected workloads with recorded evidence. (W003–W005, W034)
- [ ] P06-027 — Complete language reference, tutorials, operational/deployment guidance, supported platform/toolchain matrix, and examples for every feature. (W001–W035)
- [ ] P06-028 — Verify an independent clean environment can install, build either target, run/debug examples, inspect policy failures, and deploy the full reference applications. (W004, W032–W035)
- [ ] P06-029 — Update feature/task records with revision-specific conformance/integration evidence and close remaining cross-phase interactions before release preparation. (W001–W035)
- [ ] P06-030 — Introduce hosted CI when integration work is ready, reusing meaningful local build/run/conformance checks on supported toolchains; verify actual hosted execution before claiming success. Add release gates only for demonstrated needs. Hosted automation was moved here from P01-021 by the designer on 2026-09-06. (W004, W034)

## Decisions and blockers

**P06-030:** hosted CI is deliberately deferred until this phase. The designer wants
early effort spent implementing working language behavior, not maintaining brittle
CI/release infrastructure. The unrun Phase 1 workflow was removed; `eng/verify.sh`
remains a local entrypoint. This scheduling change does not remove a first-release
language feature or block Phase 1.

## Evidence and next action

Implementation has not started. For completed work, record the task ID, revision or worktree state, relevant code/test paths, commands and results, and documentation changes. Keep partial backend progress explicit and record the next ready task for the following AI session.

The initial entry point is P06-001, subject to the prerequisite contracts above. Append newly accepted work with unused task IDs; preserve existing IDs and reopen tasks whose accepted contracts change.
