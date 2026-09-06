# Phase 03: Cross-cutting compiler machinery

**Status:** Not started.

**Outcome:** Implement the full middleware graph, provenance, rules, triggers, lifetime/ambient proofs, and their combined lowering on both backends.

**Prerequisites:** Relevant semantic decisions and compiler/runtime representations from [Phase 01](../01-design-and-compiler-foundation/tasks.md) and [Phase 02](../02-language-and-message-runtime/tasks.md). Switch-, effect-, and transport-specific checks are completed with Phases 04 and 05.

**Feature coverage:** W003, W006, W007, W008, W009, W010, W011, W012, W019, W033. See the [feature register](../../feature-status.md) for scope and the [phase index](../README.md) for primary ownership.

All tasks contribute to the first full release on both backends. Follow the [tracking rules](../README.md#tracking-rules). Resolve design choices with the designer while progressing independent engineering work.

## Middleware graph and proofs

- [ ] P03-001 — Implement middleware/pipeline binding, node identity, explicit shared/local node connections, exported continuation points, complete-pipeline adoption through a required use file, scope matching, and receiver-selection facts across project DLL/jar boundaries. References alone must never activate middleware. Follow the [project pipeline contract](../../contracts/project-pipelines.md). (W009)
- [ ] P03-002 — Implement from-envelope refinement, transparent scope/filter behavior, guard exhaustiveness/non-mutation, effect execution, and provider assignment checks. (W009)
- [ ] P03-003 — Lower direct destinations, match/code routing, Route, respond, and reject using the specified receiver dispatch and outcome behavior. (W009, W010)
- [ ] P03-004 — Implement response unwinding and after eligibility for normal return, filter bypass, guard rejection, effect termination, exceptions, and cancellation. (W009)
- [ ] P03-005 — Implement declared routing tables, refresh/load validation, missing/invalid entry outcomes, and enumerable destination checks. (W009)
- [ ] P03-006 — Implement next any admissible-set derivation, runtime membership validation, and diagnostics for invalid destinations. (W009)
- [ ] P03-007 — Implement bounded reentrancy, cycle/termination analysis, unreachable-node warnings, envelope access checks, and body refinement. (W009)
- [ ] P03-008 — Implement origin/receiver-aware dataflow for provides/requires across every reachable path and reconstruct explanatory counterexample paths. (W009, W012)
- [ ] P03-009 — Implement outcome exhaustiveness against each reachable origin, including custom maps and runtime-selected routing. (W009, W019)
- [ ] P03-010 — Define and implement analysis budgets and sound behavior when proof resources are exhausted; exercise graphs with substantial branching. (W009)

## Services, lifetimes, and ambient availability

- [ ] P03-011 — Implement dependency lifetime checks for singleton/scoped/transient services and receiver lifetime requirements. (W010–W012)
- [ ] P03-012 — Implement escape checks through fields, statics, closures, indirect calls, channels, and detached tasks using the agreed lifetime contract. (W005, W010–W012)
- [ ] P03-013 — Propagate required ambients through ordinary and service calls, exposed contracts, and generated calls; reject unavailable required reads. (W011, W012)
- [ ] P03-014 — Integrate receiver parameter binding and guards with ambient availability and scoped lifetime checks. (W010, W012)
- [ ] P03-015 — Implement any designer-approved service ambient-provision behavior and its call-graph proof obligations; close the corresponding design question. (W011, W012)

## Provenance and lineage

- [ ] P03-016 — Implement origin/model/service stamps, provenance unions, from/only/wildcard matching, via semantics, and source-specific receiver specialization. (W006)
- [ ] P03-017 — Propagate provenance through assignments, returns, interpolation, fields, containers, aliases, scalar derivations, and captured values according to the chosen contract. (W006)
- [ ] P03-018 — Implement signature inference and exported summaries for ordinary/service calls, contracts/interfaces, generic code, and callable values. (W006)
- [ ] P03-019 — Implement transforms and exact discharge behavior, including union origins, service lineage, explicit transforms, and repeated uses of aliased values. (W006)
- [ ] P03-020 — Implement lineage predicates and compile-time metadata access while preserving the intended runtime erasure. (W006)

## Rules and triggers

- [ ] P03-021 — Implement .rules discovery, file/project scopes, all scope composition, target binding, named rulesets/use/suppression, and rule identity. (W007, W032)
- [ ] P03-022 — Implement type/attribute/field filters, property patterns, predicates, collection any/all/each, lineage/context filters, and named filter composition. (W007)
- [ ] P03-023 — Implement bind/call/glob/after/cross/named-sink points and site predicates, including indirect calls under the agreed contract. (W007)
- [ ] P03-024 — Implement replace/observe functions, argument placeholders, validation/reject/throw, effect blocks, and compile-time forbid actions. (W007)
- [ ] P03-025 — Implement specificity/priority/declaration ordering, conflict diagnostics, transform matching after discharge, and replacement versus original-value behavior. (W007)
- [ ] P03-026 — Implement static-rule residue checks, dead/unreachable rule diagnostics, source-to-sink explanations, and runtime-cost accounting. (W007)
- [ ] P03-027 — Implement all trigger boundary kinds and internal/external modifiers, filters, enter/exit/throw effects, ordering, and point metadata. (W008)
- [ ] P03-028 — Preserve typed per-argument provenance through trigger views, logging/interpolation, results, exceptions, and transform audit points. (W006–W008)
- [ ] P03-029 — Specify and implement analysis/insertion ordering and termination; recheck sinks, effects, and ambient/lifetime requirements introduced by generated code before erasure. (W003, W006–W009)
- [ ] P03-030 — Lower the complete rule/trigger/middleware behavior through shared IR and finish both platform emission paths. (W004, W006–W009)
- [ ] P03-031 — Expose machine-readable and human-readable explanations of matched rules, inserted effects, graph proofs, counterexamples, and runtime residue. (W007–W009, W033)

## Verification and phase completion

- [ ] P03-032 — Verify provider bypass, transparent nodes, custom outcomes, tables, next any, bounded reentrancy, receiver selection, and unwind order on both runtimes. (W009–W012)
- [ ] P03-033 — Verify forbidden flows through helpers, aliases, generic containers, interpolation, triggers, serializers, and transformed/reused values, including valid discharge cases. (W006–W008)
- [ ] P03-034 — Verify interactions between generated effects, async calls, scoped captures, ambient requirements, and service boundaries. (W005–W012)
- [ ] P03-035 — Complete all documented forms, update grammar/design/examples, record conformance evidence, and track operational/transport interactions through Phases 04–06. (W003, W006–W012, W033)

## Decisions and blockers

None recorded yet. Record the affected task IDs, the concrete semantic choice or blocker, the designer's decision when available, and any independent work that can continue.

## Evidence and next action

Implementation has not started. For completed work, record the task ID, revision or worktree state, relevant code/test paths, commands and results, and documentation changes. Keep partial backend progress explicit and record the next ready task for the following AI session.

The initial entry point is P03-001, subject to the prerequisite contracts above. Append newly accepted work with unused task IDs; preserve existing IDs and reopen tasks whose accepted contracts change.
