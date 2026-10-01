# Phase 02: Language foundation and message runtime

**Status:** In progress — ordinary functions, classes/models/records, static/instance data, construction, and structured control flow (P02-001/002/003).

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

- [ ] P02-036 — Complete portable type-initialization failure/rethrow behavior and cross-thread dependency-cycle handling as exception/task/lock support integrates; retain once-only publication and non-null guarantees on both runtimes. Current single-thread cycles, concurrent acyclic first use, and failed-initializer no-retry checks are in the [static contract](../../contracts/static-members.md). (W001, W002, W005)

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

Committed as `6fe0f95`, covering further portions of P02-002/008/011/032.
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

Property/indexer targets, the full numeric set, checked operations, matching, and
remaining expression forms remain within their original Phase 2 tasks. The user's
current milestone workflow is independent agent review followed by commit and push.
Both the control-flow and update-operator checkpoints are pushed to `master`.

### Ordinary-class checkpoint — 2026-09-30

Committed and pushed as `acd4b92`, covering portions of P02-001/003/011/032. Both backends
execute ordinary classes, overloaded constructors, instance/static methods, fields,
readonly checks, identity equality, and alias mutation. Constructor arguments run
before ordered field initializers; instance receivers precede written-order arguments.
Compound field updates capture the receiver and old value once before right-hand
side effects. Non-null reference fields must be initialized before construction
completes or `this` escapes. Public signatures cannot expose an internal class.
See the [object contract](../../contracts/objects.md).

- `dotnet test Weft.slnx --no-restore`: **193 passed, 0 failed, 0 skipped**, using
  .NET SDK 10.0.112 and OpenJDK 27, emitting Java release 21.
- Five new class conformance programs specify independent outputs for both backends;
  a sixth case covers bare returns. There are now 34 executable conformance cases.
  Class-binding checks cover invalid access, initialization/escape, static/instance
  use, name hiding, public contracts, and malformed object IR.
- Independent review found a bare-return traversal regression, field names falling
  through to outer functions, and empty IR sequences allowing invalid statements.
  All three are fixed with regression coverage; implementation re-review is clean.
  Additional reviewer programs verify nested compound assignment, receiver reassignment,
  named arguments containing updates, and constructor do/break/continue initialization.
- CLI runs of the [class example](../../../examples/classes/Program.weft) on both
  backends printed `50`, `50`, `true`, `false`, and `Ada` in order. Final documentation
  and example review is complete; two wording clarifications were incorporated.
- All 43 Markdown documents have valid relative link targets, and `git diff --check`
  passes. Grammar, shared IR, and developer documentation preserve the remaining
  release coverage. Hosted CI remains deferred.

### Instance-property checkpoint — 2026-09-30

Committed and pushed as `b661315`, covering further portions of P02-003/011/032. Instance
properties support automatic storage, custom/expression-bodied accessors, restricted
accessibility, getter-only constructor assignment, and non-null initialization checks.
Compound assignment and prefix/postfix updates preserve receiver/getter/RHS/setter
order. The assignment result remains the supplied value when a setter changes its
parameter or stored value. Getter calls retain source-level statement restrictions.
See the [property contract](../../contracts/properties.md) and runnable
[property example](../../../examples/properties/Program.weft).

- `dotnet test Weft.slnx --no-restore`: **245 passed, 0 failed, 0 skipped**, using
  .NET SDK 10.0.112 and OpenJDK 27, emitting Java release 21.
- Five new property conformance programs bring the executable case count to 39.
  Forty-two property binding/IR checks cover invalid access, initialization, signatures,
  accessor forms, public contracts, source statement forms, and metadata.
- Independent agent review is complete with no outstanding findings. Additional
  reviewer programs passed on both runtimes for nested receivers, field/property
  interactions, named arguments, and 64-bit updates. A source-level statement issue
  discovered during implementation was fixed and independently verified: a bare
  property read remains invalid even though it lowers to a getter call.
- CLI runs of the property example on both backends printed `A:5`, `-3`, `0`, `2`,
  and `A:3`. Documentation/example review is complete, all 44 Markdown documents have
  valid relative link targets, and `git diff --check` passes. Hosted CI remains deferred.

At this checkpoint, static/init properties, object initializers, indexers, models/records,
and remaining type features were still required work; the next checkpoint advances
initialization. No parent task is marked complete by this property checkpoint.

### Object-initializer checkpoint — 2026-09-30

Committed and pushed as `81287b2`, extending P02-003/011/032. Object initializers run after
construction with source-ordered member assignments and enclosing lexical scope.
Nested member initializers mutate existing objects. Automatic/custom init accessors
restrict writes to construction; required fields/properties make caller assignments
explicit. Non-null constructor and initializer checks prevent reads/escapes of
incomplete reference storage. Shared IR marks the fresh initialization identity and
checks init call authority. See the [initialization contract](../../contracts/initialization.md)
and runnable [example](../../../examples/initializers/Program.weft).

- `dotnet test Weft.slnx --no-restore`: **297 passed, 0 failed, 0 skipped**, using
  .NET SDK 10.0.112 and OpenJDK 27, emitting Java release 21.
- Seven new executable conformance programs bring the case count to 46. Thirty-nine
  initialization binding/IR checks cover required obligations, incomplete references,
  access, readonly/init authority, nested targets, and constructor identity.
- Independent agent review found two issues: empty nested initializers received a
  read check despite emitting no reads, and malformed constructors could return an
  existing object while retaining init privileges. Both are fixed with regression
  coverage and independent rechecks; no findings remain. Constructor IR must allocate
  its receiver first, preserve its identity, and return that receiver.
- CLI runs of the initializer example on both backends printed `Ada:London:3` and
  `Ada:London:4`. Documentation/example review is complete; all 45 Markdown documents
  have valid relative link targets and `git diff --check` passes. Hosted CI remains
  deferred.

Constructor chaining, static initialization, collection/indexer initialization,
models/records, and broader type integration remain required work. No parent task
is marked complete.

### Constructor-chain checkpoint — 2026-09-30

Committed and pushed as `50829b0`, extending P02-003/011/032. `: this(...)` delegates through
ordinary overload/argument rules; `: base()` is accepted for the current root class.
Field and auto-property initializers run once at the chain's terminal constructor,
then bodies run from inner to outer on the same object. Parameter mutations and named
arguments retain written order. Definitely initialized fields propagate across all
normal return paths, and required caller obligations remain explicit. Shared graph
checks reject circular delegation in source and IR while preserving fresh allocation.
See [constructor chaining](../../contracts/constructors.md) and its
[runnable example](../../../examples/constructors/Program.weft).

- `dotnet test Weft.slnx --no-restore`: **332 passed, 0 failed, 0 skipped**, using
  .NET SDK 10.0.112 and OpenJDK 27, emitting Java release 21.
- Five new executable conformance programs bring the case count to 51. Constructor
  binding and malformed-IR checks cover argument errors, scope, circular delegation,
  initialization summaries, required obligations, and receiver freshness.
- Independent implementation, documentation, and example review is complete with no
  actionable findings. Additional reviewer programs passed on both targets, covering
  argument-side property updates, parameter mutations, early returns, readonly/init
  state, and final required-member initialization.
- CLI runs of the constructor example on both backends printed `Ada:25` and `Grace:75`.
  All 46 Markdown documents have valid relative link targets and `git diff --check`
  passes. Hosted CI remains deferred.

Inheritance/base arguments, static initialization, models/records, and remaining type
integration stay required. No parent task is marked complete by this checkpoint.

### Model/record checkpoint — 2026-09-30

Committed and pushed as `59e2b1c`, extending P02-003/011/032. Plain models retain identity
and default public fields. Body/positional records retain reference layout while
comparing/hash-combining stored data. Positional get/init properties and constructor
parameters reuse ordinary overload and initialization checks. Shallow `with` copies
preserve aliasing, evaluation order, required-member completion, and init authority.
Explicit record copy constructors run before updates without declaration initializers.
See [models and records](../../contracts/data-types.md) and the
[runnable example](../../../examples/data/Program.weft).

- `dotnet test Weft.slnx --no-restore`: **383 passed, 0 failed, 0 skipped**, using
  .NET SDK 10.0.112 and OpenJDK 27, emitting Java release 21.
- Seven new executable conformance programs bring the case count to 58. Binding/IR
  coverage checks data-kind and positional contracts, invalid copies, access,
  non-null initialization, and preservation of init authority.
- Independent implementation review found no actionable code defects. Additional
  reviewer programs passed on both targets for parameter mutations in positional
  initializers, source-ordered named arguments, explicit positional fields, and custom
  copies. Four malformed copy-metadata cases were rejected. Roslyn comparisons confirm
  initializer order and explicit-field behavior. A wording issue was corrected: a
  non-copy positional constructor requires a `this(...)` initializer, which may target
  a copy constructor as well as the primary/other ordinary constructors.
- CLI runs of the data example on both backends printed `Ada:50`, `Ada:75`, `true`,
  `true`, `2`, `false`, and `Grace:2`. All 47 Markdown documents have valid relative
  link targets; `git diff --check` passes. Independent documentation/example review is
  complete. All 37 record binding/IR checks also passed after the wording correction.
  Hosted CI remains deferred.

Record customization, source-callable synthesized copy constructors,
display/deconstruction, inheritance and broader type/provenance integration stay
required work. No parent task is marked complete.

### Static-member checkpoint — 2026-10-01

Worktree based on `59e2b1c`, extending P02-003/011/032. Static fields, auto/custom
properties, readonly/getter-only writes, and explicit static constructors execute on
both backends. Registered type initializers run declaration initializers in order
before the constructor body. Method/constructor entry and static field access activate
the owning type. Runtime guards preserve non-null references through indirect reentry.
Static storage is excluded from instance initialization and record equality/hash/copy.
See [static members](../../contracts/static-members.md) and the
[runnable example](../../../examples/statics/Program.weft).

- `dotnet test Weft.slnx --no-restore`: **432 passed, 0 failed, 0 skipped**, using
  .NET SDK 10.0.112 and OpenJDK 27, emitting Java release 21.
- Seven new executable conformance programs bring the case count to 65. Thirty-two
  static binding/IR checks cover declaration/access rules, non-null storage,
  registration, readonly authority, and invalid direct initializer calls.
- Four native runtime harness cases verify serialized concurrent first use and
  cached failure after an indirect uninitialized-reference read on both targets.
  The harnesses use events/latches rather than sleeps or timing assertions.
- Independent implementation/documentation/example review is complete. Review found
  that an ordinary method could name an unregistered owner in malformed IR; validation
  now rejects it, with regression coverage and an independent recheck. Extra reviewer
  programs passed on both targets for initializer reentry, nested assignments,
  compound evaluation, constructor-chain ordering, and static reference properties.
- CLI runs of the static example on both backends printed `start`, `catalog ready`,
  `main`, `1`, `100`, `101`, and `true`. All 48 Markdown documents have valid relative
  link targets and `git diff --check` passes. Hosted CI remains deferred.

P02-036 explicitly retains uniform exception wrappers and cross-thread initialization
cycles for the complete release. No parent task is marked complete.

**Next:** continue P02-003 with remaining record features, then the remaining
inheritance/type work under P02-004. Generic/interface/nullable types and complete public
project contracts remain under their original tasks before services and shared
middleware integration. Preserve the accepted pipeline syntax; its graph/project binding remains
owned by P03-001 and P06-001/003. Append new work with unused task IDs when scope grows.
