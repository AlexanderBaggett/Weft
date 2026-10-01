# Phase 02: Language foundation and message runtime

**Status:** In progress — ordinary functions/static methods and structured control flow (P02-001/002).

**Outcome:** Implement the ordinary language, standard-library foundation, async/message lifetime machinery, and service/receiver runtime behavior on both platforms.

**Prerequisites:** [Phase 01](../01-design-and-compiler-foundation/tasks.md) compiler paths and the relevant semantic/intrinsic contracts. Compiler proofs and origin-specific completion are integrated with Phases 03 and 05.

**Feature coverage:** W001, W002, W005, W010, W011, W012, W018, W025, W026, W031, W032. See the [feature register](../../feature-status.md) for scope and the [phase index](../README.md) for primary ownership.

All tasks contribute to the first full release on both backends. Follow the [tracking rules](../README.md#tracking-rules). Resolve design choices with the designer while progressing independent engineering work.

## Ordinary language

- [ ] P02-001 — Implement declarations, namespaces/projects, visibility, name resolution, functions/methods, parameters, returns, and the specified public-contract rules. (W001)
- [ ] P02-002 — Implement expressions and statements with the specified evaluation order, assignment/mutation, branching, matching, loops, and control-flow behavior. (W001)
- [ ] P02-003 — Implement models, ordinary classes/records, construction, field/property access, identity/value equality, and the agreed aliasing rules. (W001)
- [ ] P02-004 — Implement generics, contract/interface types, type inference, conversions, and host erasure behavior without exposing forbidden runtime type operations. (W001, W002)
- [ ] P02-005 — Implement callable values, lambdas, captures, indirect calls, and the metadata needed for later lifetime/provenance/effect analysis. (W001)
- [ ] P02-006 — Implement arrays and collections, indexing, iteration, element typing, optional values, and nullability checks. (W001, W018)
- [ ] P02-007 — Implement exception creation, propagation, handling, and cleanup with the same Weft exception behavior on both runtimes. (W001, W002)
- [ ] P02-008 — Implement the [numeric contract](../../contracts/numeric-types.md): signed/unsigned 8/16/32/64-bit integers, char, float/double, C# decimal behavior, BigInteger/BigDecimal library support, and distinct decimal128; include C# promotions, literals/conversions, checked/unchecked behavior, comparisons, rounding, serialization, and edge cases on both targets. (W002)
- [ ] P02-009 — Implement UTF-16 strings, interpolation, ordinal/culture-aware comparisons, formatting, and agreed Unicode behavior. (W002)
- [ ] P02-010 — Implement locks and paired extern dotnet/extern jvm declarations, binding checks, and boundary validation under the agreed reflection policy. (W002)
- [ ] P02-011 — Complete both emitters for the ordinary-language constructs and verify host implementation differences do not change specified behavior. (W001, W002, W004)

## Standard-library foundation

- [ ] P02-012 — Implement Id, Secret, Response, Problem, Page, Message, Option, and other types required by the complete examples with documented contracts. (W018)
- [ ] P02-013 — Implement time/duration, Money and currency rounding, refinement types, validators, and standard transformations. (W002, W018)
- [ ] P02-014 — Provide generated serializers and logging/metrics interfaces that preserve the metadata required by provenance and effect analysis. (W018)
- [ ] P02-015 — Implement configurable Clock/Random behavior and deterministic test substitutions, with source stamps available to the compiler. (W005, W018, W031)

## Tasks, cancellation, and ownership

- [ ] P02-016 — Implement .NET task spawn/await/composition/delay/async-iteration intrinsics with the specified scheduling, failure, and cancellation contract. (W005)
- [ ] P02-017 — Implement JVM task spawn/await/composition/delay/async-iteration intrinsics with the same contract, including both immediately-awaited and escaping-task lowering. (W005)
- [ ] P02-018 — Implement .NET message scopes that own and track child tasks, cancel when specified, join children, and dispose resources in the specified order. (W005)
- [ ] P02-019 — Implement JVM message scopes with equivalent child ownership, joining, cancellation, and disposal behavior. (W005)
- [ ] P02-020 — Implement deadline narrowing, remaining budgets, nested cancellation, interruptible waits, and error mapping across both runtime implementations. (W005, W012)
- [ ] P02-021 — Implement the contract for uncooperative external operations, scope closure races, spawning during shutdown, and failures during resource disposal. (W005)
- [ ] P02-022 — Implement bounded channel primitives, full policies/backpressure, cancellation, and producer/consumer lifetime behavior on both platforms. (W005, W025)
- [ ] P02-023 — Implement timer/scheduler primitives and controllable test time for interval, scheduled, and one-shot execution. (W005, W026)

## Services, receivers, and ambients

- [ ] P02-024 — Implement service/receiver activation for singleton/scoped/transient lifetimes and message-owned resource disposal. (W010, W011)
- [ ] P02-025 — Implement requires/exposes binding, missing/ambiguous dependency diagnostics, configuration binding/validation, and test-profile substitutions. (W011, W032)
- [ ] P02-026 — Implement reentrant, serialized, and partitioned service execution on both platforms, including cancellation while queued and shutdown. (W011)
- [ ] P02-027 — Implement health probes, startup readiness, and runtime aggregation with documented failure behavior. (W011)
- [ ] P02-028 — Implement receiver declarations, per-binding entry generation, Request tagging, parameter/return contracts, guard placement, and symbolic outcomes. (W010)
- [ ] P02-029 — Implement ambient declaration, hidden-parameter passing, required/optional reads, provider assignment representation, and transport carry interfaces. (W012)
- [ ] P02-030 — Expose call, capture, scope, and boundary facts to Phase 03 for lifetime/availability/provenance proofs; integrate those checks before declaring these features complete. (W010–W012)
- [ ] P02-031 — Connect Channel and Timer to receivers and services for end-to-end message execution using the common origin interface; complete production origin coverage in Phase 05. (W010, W025, W026)

## Verification and phase completion

- [ ] P02-032 — Add conformance cases for ordinary-language semantics, decimal/Unicode/nullability edges, closures/aliasing, extern boundaries, and generated serializers on both targets. (W001, W002, W018)
- [ ] P02-033 — Verify nested scopes, child failures, cancellation/disconnect propagation, deadlines, queued service calls, ambient captures, and exact disposal ordering. (W005, W010–W012)
- [ ] P02-034 — Verify bounded channel overload and timer overlap/cancellation behavior using controlled workloads and clocks. (W025, W026)
- [ ] P02-035 — Complete compiler/runtime integration on both backends, update examples and documentation, and record evidence and unresolved cross-phase tasks in the feature register. (W001, W002, W005, W010–W012, W018)

## Decisions and blockers

The designer accepted the shared-pipeline syntax and authorized continued implementation
on 2026-09-06. Ordinary function behavior follows the accepted C# direction in decision
0002. No designer blocker is recorded for the next declaration/object work. Hosted CI
remains deferred to P06-030.

## Evidence and next action

First Phase 2 implementation checkpoint, 2026-09-06, recorded in commit `5c37877`
(including the completed Phase 1 foundation). The tasks above remain unchecked because their complete feature scope is
larger than this function/method checkpoint.

| Tasks | Implemented portion and evidence | Remaining work |
|---|---|---|
| P02-001 | Typed static-class/function declarations; methods, signature-based overload groups, private/internal/public checks, forward and cross-file calls, namespace/type name hiding, argument names/defaults and declaring-type metadata. [Binder](../../../src/Weft.Compiler/Semantics/Binder.cs), [function contract](../../contracts/functions.md) | Instance and generic declaration integration, imports, complete public contracts and separate-project boundaries |
| P02-002/004/008 | Named/optional argument binding; written-order evaluation; explicit int32-to-int64 widening in calls, returns, initializers, assignments and mixed integer expressions; checked evaluation of supported parameter-default constants | Remaining expressions/statements, conversion matrix, generic/nullable and full numeric behavior |
| P02-011 | Both emitters produce direct static methods and static argument-reordering adapters, without closures or runtime argument arrays. IR checks overload signatures, defaults, conversions and argument permutations; entry resolution handles overloads | All remaining ordinary-language constructs and host differences |
| P02-032 | Four new executable conformance programs: overloads, named argument traces/mutations, cross-file static methods, optional constants. Function-binding diagnostics cover invalid defaults, argument names/order, accessibility, conflicting signatures, namespace hiding and ambiguity | Full ordinary-language, decimal/Unicode/nullable/extern/serializer coverage |
| P02-035 | Runnable [functions example](../../../examples/functions/Program.weft), updated grammar/development/IR contracts, accepted pipeline syntax and current feature status | Whole-phase integration and remaining examples/contracts |

Verification on .NET SDK 10.0.111 and OpenJDK 26.0.2, emitting Java release 21:

- `dotnet test Weft.slnx --no-restore`: **92 passed, 0 failed, 0 skipped**; all 20
  executable conformance programs run through the applicable .NET/JVM checks. Runtime
  examples assert specified outputs rather than treating one backend as the oracle.
- CLI runs of `examples/functions` with `--backend dotnet` and `--backend jvm` each
  printed `price first`, `units second`, `Total: 50` in that order. This exercises
  a private helper, a public static method in another source file, named argument
  ordering, widening, and an omitted default through the actual project loader/build.
- Documentation links resolve and `git diff --check` passes. Hosted CI was not added.

### Control-flow checkpoint — 2026-09-30

Committed as `e567947`, covering portions of P02-002/011/032. Both backends now
execute for/do loops, break/continue, empty statements, and conditional expressions.
Loop locals, ordered initializer/iterator calls, nearest-loop exits, conditional
int-to-long widening, and conditional optional defaults share frontend checks.
[Shared flow analysis](../../../src/Weft.Compiler/IR/ControlFlow.cs) handles missing
returns and guaranteed exits in both binding and IR validation. See the
[control-flow contract](../../contracts/control-flow.md) for supported syntax and
remaining constant-reachability work.

- `dotnet test Weft.slnx --no-restore`: **130 passed, 0 failed, 0 skipped**, using
  .NET SDK 10.0.112 and OpenJDK 27, emitting Java release 21.
- Five new conformance programs assert independent output expectations on both runtimes;
  28 frontend/IR checks cover invalid control flow. There are now 25 conformance cases.
- Independent agent review completed with no actionable regressions. Additional CLI
  programs passed on both backends, covering named arguments throughout loop headers,
  conditional branch selection, continue order, nested returns, and unreachable iterators.
- `git diff --check` passes. Hosted CI remains deferred.

P02-002 remains open for matching, remaining expression forms, constant reachability,
and integration with collection iteration and cleanup. No parent task is marked
complete by this checkpoint; the full release scope is unchanged.

### Update-operator checkpoint — 2026-09-30

Worktree based on `e567947`, covering further portions of P02-002/008/011/032.
Prefix/postfix increment/decrement and arithmetic compound assignments now execute
on both backends for current local/parameter types. Shared binary binding preserves
conversion checks and uses existing runtime division/remainder contracts. Old-value
reads precede right-hand effects; prefix/postfix return values and unchecked integer
limits are explicit. String `+=` shares ordinary string concatenation.

- `dotnet test Weft.slnx --no-restore`: **148 passed, 0 failed, 0 skipped**, using
  .NET SDK 10.0.112 and OpenJDK 27, emitting Java release 21.
- Three new executable conformance programs cover source evaluation order, chained
  assignments, signed limits, named calls, short-circuiting, conditionals, and loop
  headers. Twelve frontend/IR checks cover invalid targets, types, defaults, and IR.
  There are now 28 conformance cases.
- Independent agent review completed with no actionable regressions. Extra CLI
  checks passed on both runtimes for chained assignments, prefix/postfix precedence,
  reordered named arguments, nested string updates, and signed-limit wrapping.
- `git diff --check` passes. Hosted CI remains deferred.

Object/indexer targets, the full numeric set, checked operations, matching, and
remaining expression forms remain within their original Phase 2 tasks. The user's
current milestone workflow is independent review followed by a local commit.

**Next:** continue P02-001/003 with ordinary classes/models/records, construction and
field/property access, using these method symbols and call rules. Extend instance
receiver evaluation and public type contracts before integrating services and shared
middleware. Preserve the accepted pipeline syntax; its graph/project binding remains
owned by P03-001 and P06-001/003. Append new work with unused task IDs when scope grows.
