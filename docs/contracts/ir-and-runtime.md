# Shared IR and runtime contracts

## Current executable seam

The initial typed IR is [IrNodes.cs](../../src/Weft.Compiler/IR/IrNodes.cs). It contains
functions, block scopes, initialized locals, assignments, returns, branches, loops,
typed constants/reads/operators/calls, prefix/postfix updates, explicit int32-to-int64
conversions, and versioned intrinsic calls. Class declarations, construction, fields,
and method receivers are also explicit. Executable types are void, bool, int32, int64,
string, and ordinary nominal class references. The type model also names the future
array, nullable, generic, signed/unsigned integer widths, char, float32/float64,
decimal, and decimal128 forms. These numeric identities follow the
[numeric contract](numeric-types.md); adding a type identity is not its implementation.
Unsupported semantic passes
produce WF2009; parsing a declaration does not make its checks pass.

Control flow is structured and explicit. Operands evaluate left to right; boolean
operators short-circuit. Conditional expressions evaluate exactly one arm. A value-returning
function cannot reach its end without a return; a statement after a guaranteed exit
is diagnosed before either backend. Shared [flow analysis](../../src/Weft.Compiler/IR/ControlFlow.cs)
tracks return/break/continue and consumes jumps at the nearest loop. See the
[control-flow contract](control-flow.md) for loop scope and current reachability limits. Every variable/function
has a stable identity within one compilation. Source files and declarations are visited
in deterministic order. A symbol's emitted name is independent of host keywords.

The first Phase 2 extension binds overloaded functions and static methods. Symbols
retain visibility, containing type, parameter names/defaults, and signature locations.
`IrCall.Arguments` is in written evaluation order; `ParameterOrder` maps the resulting
values to parameters. Both emitters use shared [call adapter discovery](../../src/Weft.Compiler/Backends/CallAdapters.cs)
to emit static reordering helpers. Instance receivers evaluate before arguments.
New expressions/statements must extend shared [IR traversal](../../src/Weft.Compiler/IR/IrTraversal.cs)
along with the validator and emitters; both call and temporary discovery use it. Optional constants keep
the declaration and call origin. See [function contracts](functions.md).
Compound assignments reuse typed reads, binary operations, and assignments; the old
local value is evaluated before the right-hand side. `IrUpdate` retains prefix/postfix
result behavior and validates its target's scope and integer type.

`IrClass` and `FieldSymbol` describe reference objects and stable field identities.
Constructors are factory functions; `IrAllocate` is restricted to the corresponding
constructor. `IrFieldRead`/`IrFieldWrite`/`IrFieldUpdate` carry explicit receivers.
`IrSequence` contains one or more scoped initialized temporaries followed by a value,
preserving target capture and old-field reads before right-hand effects. Its locals
cannot escape the expression in validated IR. Both emitters use typed static sequence
helpers and method-local temporaries. See the [object contract](objects.md).

Property getters are ordinary receiver calls; `IrSetterCall` invokes a void setter
and produces the supplied argument value. Typed static assignment helpers preserve
that value even if the setter changes its parameter or stores a different value.
The validator checks the registered single-argument void signature, receiver, and
value type. Shared traversal discovers helpers inside branches, arguments, and
sequences. Property metadata remains available in binding results; auto-properties
use compiler-owned fields with ordinary initialization rules. See [properties](properties.md).

Object initializers use `IrSequence.Initializing` to identify the first binding's
fresh constructor result. Constructors must start by allocating their receiver or
delegating to a constructor of the same class, and return only that receiver. Shared
constructor-graph checks reject direct/indirect delegation cycles, so each accepted
chain ends in allocation. See [constructor chaining](constructors.md). The sequence yields that same object after ordered member
writes. The validator authorizes init accessor calls only on that identity or the
current receiver in a constructor/init accessor, and prevents identity reassignment.
Required member obligations and incomplete-reference checks run before lowering;
field/property/function metadata retain required/init markers. See [initialization](initialization.md).

The [validator](../../src/Weft.Compiler/IR/IrValidator.cs) checks function/local identity,
scope, portable types, constant representations, operator signatures, call signatures,
intrinsic registration, class/field ownership, receiver signatures, readonly writes,
return/branch types, and fallthrough. Invalid IR is a compiler
diagnostic at its source origin and must not reach code generation. ABI and required
operation signatures are checked before building an artifact.

Every node retains `SourceOrigin(Location, GeneratedBy, Parent)`. Inserted nodes will
name their generating rule/trigger/cache/graph operation and retain the original call
site as a parent. Generated source emits a line map with this chain. C# also emits
`#line` and portable PDB information. Both downstream compilers translate errors to
Weft locations. JVM SMAP/interactive source debugging remains Phase 6 work; the current
JVM deliverable is generated Java plus the diagnostic source-map sidecar.

## Analysis must precede erasure

The complete pass order is parse → declare/bind → type/refinement/effect analysis →
graph/lifetime/state proofs → insertion → recheck inserted effects → erasure → IR
validation → platform emission. The ordinary bootstrap has no insertion or erasure work
to do, so its binder directly constructs ordinary typed IR. That shortcut must not be
used for unimplemented declarations.

Before erasure, semantic nodes/side tables must retain:

- Nominal types plus conservative provenance refinements on values, fields, elements,
  aliases, captures, and public signatures; roots/crossed services/transform lineage.
- Boundary identities, static rule matches, runtime predicates, priority/scope order,
  suppression, and insertion identities/cycle witnesses.
- Ambient availability, providers, lifetime owners/borrows, cancellation/deadline
  context, call effects, and receiver/binding selection.
- Graph admissible sets, outcomes, body/origin refinements, reentrancy bounds, and
  switch-state proof contexts, including counterexample paths.
- Cache read/write dependencies, partitioned keys and invalidation derivations;
  transaction/outbox participation; retry/compensation eligibility and ownership.

Only after the last consumer has checked this data may it be erased. An analysis
failure cannot be converted into an unchecked host call. Incremental builds must
invalidate summaries when a public contract, rule scope, or admissible graph changes.

## Extension rule

Add an IR type/operation only with its portable semantics, validator rule, source-origin
propagation, both lowerings, runtime binding if needed, and independently specified
positive/negative conformance cases. No Task, CancellationToken, CompletableFuture,
Thread, host System.Decimal object, reflection object, or adapter-specific response
enters IR. Weft's own `decimal` type is portable; only its backend representation uses
System.Decimal or a JVM wrapper.
Unknown operations are errors. ABI changes require an explicit version decision;
adding an operation adds a capability requirement rather than assuming every older
runtime has it. Backend-specific optimizations must preserve observable behavior.

Separately compiled middleware also carries [project contract metadata](project-pipelines.md).
Public nodes/pipelines, continuation points, reachable paths, requirements, effects,
and origins must survive in that metadata so the application can validate explicit
connections across DLL/jar boundaries. A project reference alone contributes no active
middleware. These bindings extend the shared analysis before graph checks and erasure.

## Intrinsic vocabulary

This is the required interface map for the complete release. `Scope`, `Task<T>`,
`Cancel`, `Deadline`, `Channel<T>`, `Cache`, `Snapshot`, `Transaction`, `Message`,
`Response`, `Address`, `NodeId`, and `Failure` below are **portable IR/runtime handles
or generated records**, never host types. Generic signatures are compile-time schemas;
runtime type arguments are unnecessary. Hidden scope/context arguments are explicit
here even when omitted from user syntax. Error means a portable failure with origin
metadata. Cancellation means cooperative termination under the owned scope contract.

Only the four bootstrap operations below are implemented today. All other rows are
required contracts for subsequent phases, not claims that runtime stubs exist.

| Intrinsic/schema | Observable contract | Failure/cancellation and both-platform obligation |
|---|---|---|
| `rt.console.write_line(string) -> void` | Write supplied text followed by a platform line terminator | Propagate output I/O failure; conformance normalizes CRLF only |
| `rt.text.int32(int32) -> string` | Invariant base-10 signed digits | Total; identical text on both |
| `rt.text.int64(int64) -> string` | Invariant base-10 signed digits | Total; identical text on both |
| `rt.text.bool(bool) -> string` | Lowercase true/false | Total |
| `rt.scope.enter(parent?, context) -> Scope` | Register ownership and context before work | Allocation/startup failure cannot leak resources |
| `rt.scope.exit(Scope, Completion) -> Task<Completion>` | Join/cancel/join/dispose under the lifecycle contract | Preserve primary plus cleanup failures; no early disposal |
| `rt.task.spawn<T>(Scope, () -> T) -> Task<T>` | Register then run; async start order unspecified | Closed scope rejects; child captures checked before IR |
| `rt.task.await<T>(Task<T>, Cancel) -> T` | Observe value or portable failure | Cancellation cannot detach owned work |
| `rt.task.when_all<T>(Task<T>[]) -> Task<T[]>` | Input-order values; empty succeeds; wait for all | All failures retained; fault beats cancellation; no sibling cancel |
| `rt.task.when_any<T>(Task<T>[]) -> Task<Task<T>>` | First completed task; no specified tie winner | Empty errors; does not cancel losing tasks |
| `rt.task.delay(Duration, Cancel) -> Task<void>` | Logical duration; zero completes | Negative duration errors; cancellation wakes promptly |
| `rt.cancel.token(Scope) -> Cancel` | Obtain linked scope cancellation handle | Never expose the host token |
| `rt.cancel.check(Cancel) -> void` | Continue if active | Raise portable cancellation if requested |
| `rt.deadline.remaining(Deadline, Clock) -> Duration` | Nonnegative remaining budget using monotonic time | Expired yields zero; nested deadlines cannot extend parents |
| `rt.channel.open<T>(Scope, capacity, FullPolicy) -> Channel<T>` | Typed capacity and configured backpressure/drop policy | Invalid capacity errors; owner closes and joins users |
| `rt.channel.send<T>(Channel<T>, T, Cancel) -> Task<SendResult>` | Enqueue/drop/backpressure as declared | Cancellation while waiting cannot duplicate acceptance |
| `rt.channel.receive<T>(Channel<T>, Cancel) -> Task<Option<T>>` | Receive in queue order; completion is distinct from null payload | Closed drained channel completes; failures/cancel preserved |
| `rt.switch.snapshot(Scope) -> Snapshot` | One immutable message view of flag/routing state | Declared last-known/default/fail-closed policy on unavailable source |
| `rt.switch.read<T>(Snapshot, SwitchId, TargetKey) -> T` | Typed value/cohort from that snapshot | Invalid source value diagnosed; no undeclared state |
| `rt.switch.observe(SwitchId, ArmId, MetricSample) -> void` | Attribute split/shadow outcomes and overhead | Observation failure follows declared metrics policy, never changes arm contract silently |
| `rt.switch.transition(SwitchId, expectedVersion, State) -> Result` | Atomic promotion/rollback/kill/probe state change | Version conflict retries control-plane work, not user side effects |
| `rt.cache.get<K,V>(Cache, K, Cancel) -> Task<CacheRead<V>>` | Hit/miss/version distinct; preserve value contract/provenance | Storage failure follows explicit cache policy; miss never fabricates a value |
| `rt.cache.put<K,V>(Cache, K, V, TTL?, readVersion, Cancel) -> Task<PutResult>` | Publish against an invalidation generation | Reject stale fill; cancellation has a specified accepted/not-accepted outcome |
| `rt.cache.invalidate<K>(Cache, K, Transaction?, Cancel) -> Task<void>` | Advance generation; enlist when transaction-controlled | Rollback does not expose a committed invalidation; concurrent fills cannot resurrect stale data |
| `rt.transaction.begin(Scope, participants) -> Transaction` | Enlist declared transactional resources | Incompatible participants fail before effects |
| `rt.transaction.commit(Transaction, Cancel) -> Task<CommitResult>` | Commit writes, outbox records, and coordinated invalidation | Distinguish committed/rolled-back/in-doubt; never assume cancellation means rollback |
| `rt.transaction.rollback(Transaction) -> Task<void>` | Attempt rollback once | Preserve rollback failure with primary failure |
| `rt.outbox.append<T>(Transaction, Address<T>, T) -> void` | Persist intent atomically with transaction | No transport send before commit; delivery/recovery is adapter work |
| `rt.retry.delay(attempt, Backoff, Clock, Random, Cancel) -> Task<void>` | Bounded controlled delay inside remaining budget | Eligibility is proven before IR; never retry unknown external effects |
| `rt.saga.record(SagaId, StepId, State, payload, Cancel) -> Task<void>` | Durable step/compensation state before recovery decisions | Recovery contract must distinguish completed, uncertain, and compensated effects |
| `rt.saga.compensate(Scope, completedSteps, Failure) -> Task<Completion>` | Reverse completed-step order, respecting finality | Record and report compensation failures; never claim atomic rollback |
| `rt.route.resolve(TableId, Key, Snapshot, AdmissibleSet) -> RouteChoice` | Return a checked NodeId or explicit fallback | Invalid/stale/out-of-set rows cannot bypass ambient/outcome/cycle proofs |
| `rt.origin.<name>.bind<T>(Scope, Message, BindingId) -> Task<T>` | Typed address/envelope-to-parameter binding | Decode failures map Invalid; cancellation preserves ownership |
| `rt.origin.<name>.respond(Scope, Response) -> Task<Completion>` | Single commit after preparation/unwind | Precommit failure can map outcome; postcommit failure aborts |
| `rt.origin.<name>.reject(Scope, Outcome, Problem?) -> Task<Completion>` | Map symbolic failure to protocol behavior | Every reachable outcome must have a compile-checked mapping |
| `rt.origin.<name>.send<T>(Scope, Address<T>, T, Context) -> Task<SendResult>` | Typed payload and permitted ambient carry | Adapter classifies acceptance/uncertainty/retry behavior; honors deadline |
| `rt.origin.<name>.stream(Scope, Stream<T>, Cancel) -> Task<Completion>` | Backpressured lifetime through end/abort | Do not dispose borrowed services when headers commit |
| `rt.metrics.record(MetricId, MetricSample, Context) -> void` | Record boundary/arm/site attribution | Bounded observer behavior; preserve user failure on observer failure |
| `rt.metrics.read(MetricId, Window, Clock) -> MetricSnapshot` | Explicit window and sample semantics | Empty/insufficient data distinct from zero; judges cannot guess |

Additional library operations (numeric/collection/serialization/DB/locks) must enter the
same registry when their Phase 2/5 contracts are implemented. Reusing a platform library
does not permit its defaults to override Weft's contract.

## Requirements that constrain this interface now

Caches need generations and transaction association, not just get/put. Flags, canaries,
and kills need immutable message snapshots plus an atomic control plane. Transactions
need commit uncertainty; retry eligibility belongs above IR. Sagas need durable step
identity and compensation outcome records. Routing requires checked finite identities,
not reflection or an arbitrary host delegate. Custom origins instantiate the same
binding/send/commit schemas and provide generated serializers/outcome maps. Streaming
requires completion ownership separate from response preparation. These requirements
are retained before any of those features are lowered.
