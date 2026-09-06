# Language and message acceptance

All rows use the [acceptance protocol](README.md). Ordinary contracts follow
[decision 0002](../decisions/0002-ordinary-and-portable-contracts.md); provenance and
task/lifecycle contracts use accepted [decision 0001](../decisions/0001-phase-1-semantics.md).
The default owner is Phase 2, except compiler/backend infrastructure in Phase 1.

## W001 — Ordinary language

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W001-A01 | `Sum(Mark(1), Mark(2))`; trace 1,2 before Sum; nested expressions and initializers evaluate once left to right | Wrong arity/type points to argument; inserted rule calls must preserve order (W007) |
| W001-A02 | `false && SideEffect()`, `true || SideEffect()`, conditionals/coalescing; unselected arms do not execute | Non-bool condition diagnosed at condition; nullable flow joins checked (W002) |
| W001-A03 | Blocks, if/else, loops, break/continue, match expressions and function returns; exact values and only reachable effects | Missing return/nonexhaustive match/unreachable statement at offending branch; cleanup on exits (W005) |
| W001-A04 | `var b = a; b.Field = 2`; a sees 2; parameter reference copying and nested lexical shadows | Duplicate declaration/unknown identifier/out-of-scope local at use; alias provenance retained (W006) |
| W001-A05 | Construct model/class/record, access fields/properties, use record `with`; identity versus member equality and shallow copy follow 0002 | Bad initializer/member/constructor and illegal mutation diagnosed; model origin and record field provenance survive (W006) |
| W001-A06 | Generic collection/function/interface instances and type inference; constrain calls through exposed contract | Constraint violation, unsafe variance, ambiguous conversion, erased-overload collision at declaration/call (W002,W011) |
| W001-A07 | Function values and lambdas capture a mutable local; repeated calls see the same cell | Incompatible callable effects or scoped capture escape at capture/storage, related owner (W005,W007,W012) |
| W001-A08 | Arrays, lists, dictionaries, iteration, indexing, collection initializers, equality/comparer operations | Invalid index is portable failure; element type/nullability violations at write; element tags survive (W006) |
| W001-A09 | Private/internal/public names across namespaces and projects; source order does not affect name resolution | Inaccessible/duplicate/ambiguous symbol at use with related declarations; exposed summaries checked (W011,W032) |
| W001-A10 | `try/catch/finally`, throw/rethrow, return through finally; original error location retained | Invalid exception/catch use diagnosed; cleanup failures preserve primary error (W005,W008) |

Foundation execution: [ordering](../../tests/conformance/ordering.json),
[arithmetic](../../tests/conformance/arithmetic.json), [scopes](../../tests/conformance/scopes.json),
[recursion](../../tests/conformance/recursion.json), [namespaces](../../tests/conformance/namespaces.json),
[type-error](../../tests/conformance/type-error.json), [missing-return](../../tests/conformance/missing-return.json),
[unknown](../../tests/conformance/unknown.json), [duplicate](../../tests/conformance/duplicate.json),
[unreachable](../../tests/conformance/unreachable.json), and
[function-resolution](../../tests/conformance/function-resolution.json). These cover the bootstrap
portions only, not complete W001 families.

Phase 2 function execution extends W001-A01/A09 with
[overloads and entry selection](../../tests/conformance/function-overloads.json),
[named argument evaluation](../../tests/conformance/named-arguments.json),
[static methods across source files](../../tests/conformance/static-methods.json), and
[optional constants](../../tests/conformance/optional-constants.json).
[Function binding checks](../../tests/Weft.Tests/FunctionBindingTests.cs) cover access,
ambiguous/duplicate signatures, name hiding, and invalid argument/default contracts.

## W002 — Portable platform semantics

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W002-A01 | Signed minima/maxima; unchecked add/subtract/multiply/negate wrap; minimum divided by -1 throws; minimum remainder -1 is zero | Zero divisor fails; C# checked and constant-expression overflow diagnosed correctly; conversion rules checked |
| W002-A02 | Binary32/64 rounding, signed zero, NaN/infinity, comparisons, conversion and formatting vectors | No host-specific reassociation; invalid/overflowing conversions obey explicit contract |
| W002-A03 | Distinct decimal128: 34-digit rounding, halfway ties, exponent extremes, subnormals, arithmetic and canonical serialization | Finite profile throws on zero division/overflow; no narrowing to C# decimal's precision (W018) |
| W002-A04 | UTF-16 surrogate pairs, embedded null/escapes, ordinal equality/order, explicit Culture operations | No process-locale-dependent output; both sources/diagnostics count UTF-16 columns (W003) |
| W002-A05 | Equal records/strings/decimals as dictionary keys; reference-equal class/model objects | Equal values require equal hashes; iteration/hash numeric identity not asserted (W001,W013) |
| W002-A06 | Paired extern call with nullable/generic/exception/ownership/effect metadata | Missing counterpart, null crossing non-null edge, type-argument reflection, erased signature clash diagnosed at binding/use (W006,W017) |
| W002-A07 | Contended/reentrant language lock on shared object; mutual exclusion and release on failure | Scoped capture/lock misuse diagnosed; JVM uses the declared lock mechanism, never accidental host monitor semantics (W005) |
| W002-A08 | Generated serializers/type descriptors for known nominal types; no user runtime reflection | `typeof(T)`/unsupported reflection at source expression; generic erasure cannot expose host type arguments (W018) |
| W002-A09 | Signed/unsigned 8/16/32/64-bit values, char, C# promotions, suffixes, casts, comparisons, shifts, parsing and wire round trips | Java byte signedness cannot leak into Weft byte; ulong high-bit division/comparison/formatting stay unsigned; checked narrowing fails (W018,W019) |
| W002-A10 | C# decimal: 0.1m+0.2m, 1m/3m, 96-bit coefficient bounds, scale 28, rounding ties and scale-sensitive formatting | Overflow/zero division fail; JVM wrapper matches independently specified results; 1.0m and 1.00m compare/hash equally; binary-float mixing needs conversion |
| W002-A11 | BigInteger exact operations beyond 64 bits; BigDecimal exact coefficient/scale and explicit rounding contexts | Nonterminating exact division fails without a rounding context; conversion to bounded decimal checks range/scale; no silent precision truncation |

Foundation execution: [overflow](../../tests/conformance/overflow.json),
[strings](../../tests/conformance/strings.json), plus runtime division checks in
[BackendTests](../../tests/Weft.Tests/BackendTests.cs); full numeric conversions,
checked syntax/constant evaluation, floating/decimal/extern/locks remain
Phase 2 implementation work.

## W003–W004 — Compiler and both emitters

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W003-A01 | CR/LF/CRLF, Unicode, comments, escaped/verbatim/interpolated strings and contextual words | Unterminated string/comment/group and invalid escape have bounded recovery and exact Weft locations |
| W003-A02 | Parse every documented declaration including complete checked-in sketches; retain nested blocks/token origins | Mismatched delimiters recover; unimplemented semantic declaration emits WF2009, never check success |
| W003-A03 | Multi-file symbols, nested scope lookup and typed operator/call nodes | Unknown/duplicate/type mismatch errors precede emission; no cascade for an already-error expression |
| W003-A04 | Lower ordinary control flow and validate typed IR; retain generated origin chains | Corrupt constant/operator/local/function/intrinsic/return/branch rejected by WF3001 rather than crashing |
| W003-A05 | Insert a rule whose generated call reaches another rule/trigger/effect scope | Recheck generated effects and detect insertion cycle at related rule/site; preserve provenance until final erasure (W006–W009,W017) |
| W003-A06 | Incremental change to public provenance/ambient/switch/cache summary | All dependent proofs rerun; stale cached analysis cannot pass (W006,W009,W013,W014) |
| W004-A01 | One shared typed project builds/runs through Roslyn and javac; exact expected stdout/stderr/exit | Invalid entry WF4003; missing/incompatible runtime WF4001/4002 before codegen |
| W004-A02 | Intentionally invalid emitted C#/Java mapped to an inserted rule origin | WF4101/WF4201 at Weft span; parent call-site chain persists in source map |
| W004-A03 | Inspect emitted source and execute installed package without compiler checkout | Missing javac/jar/runtime yields actionable diagnostic; package includes ABI-compatible runtime |
| W004-A04 | Debug stepped ordinary and generated operations using Weft source | PDB/SMAP mappings cover insertions and exceptions; host source lines do not replace Weft origin (Phase 6) |

Foundation execution: [FrontendTests](../../tests/Weft.Tests/FrontendTests.cs),
[BackendTests](../../tests/Weft.Tests/BackendTests.cs),
[hello](../../tests/conformance/hello.json), [exit](../../tests/conformance/exit.json),
[deferred-feature](../../tests/conformance/deferred-feature.json), and every executable
JSON case on both targets. W003-A05/A06 and W004-A04 are future required work.

## W005 — Tasks, cancellation, and ownership

Use [lifecycle](../contracts/message-lifecycle.md) and
[controlled ordering](../contracts/concurrency-testing.md); no incidental schedule assertions.

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W005-A01 | Async function immediately awaited versus stored then awaited; value/exception equal | Body start may precede or follow call return; no async void/.Result/.Wait/host task APIs (W002) |
| W005-A02 | WhenAll empty, ordered successes, A fault while B held, mixed fault/cancel | Empty succeeds; nonempty waits for all, results input-ordered, all faults retained before cancellation; B not auto-canceled |
| W005-A03 | WhenAny success/fault/cancel winner, simultaneous tie, held loser | Return winning task; losers stay owned; empty input errors; only membership asserted for ties |
| W005-A04 | Delay and nested deadline with logical-clock ticks and explicit cancellation gates | No early completion or deadline extension; cancellation wakes waiters and preserves ownership |
| W005-A05 | Parent success/failure with children borrowing a scoped service | Join on success; cancel then join on failure; no disposal before last child terminal |
| W005-A06 | Child ignores cancellation until external gate released | Resource remains live and operational diagnostic names task/owner; disposal follows completion |
| W005-A07 | Async iteration producer/channel with backpressure, cancellation, early consumer exit | Producer terminates/joins; no dropped ownership or leaked enumerator (W025) |
| W005-A08 | Scope owns transient/scoped disposables; cleanup throws after primary failure | Reverse dependency disposal, all cleanup attempted, primary plus cleanup errors retained (W011) |
| W005-A09 | Buffered response versus streaming response; abort during body production | Preparation/after before commit, stream ownership after headers, postcommit abort cannot send new status (W020–W022) |

## W010 — Receivers

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W010-A01 | Orders.Create binds Http.Post and Queue to one method; exact typed arguments and source tag per binding | Missing/ambiguous binding or wrong payload at address/parameter (W019,W020,W023) |
| W010-A02 | Request model's fields are trimmed at bind and lazy-sanitized at a later sink | Request tag appears only for receiver-parameter models; field paths and originating binding retained (W006,W007) |
| W010-A03 | Receiver guard returns approve/reject before body; symbolic Created/NotFound/Invalid results | Nonexhaustive guard, guard mutation, unmapped outcome diagnosed at verdict with origin path (W009,W019) |
| W010-A04 | Receiver-specific middleware scope observes selected binding before Route | Excluded receiver bypasses effect/after; required ambients still proven on its own paths (W009,W012) |
| W010-A05 | Receiver/service requirements and scoped lifetime; boundary triggers on enter/exit/throw | Missing service/ambient, scoped escape, invalid singleton capture at use/capture (W008,W011) |
| W010-A06 | Complementary flag conditions on one address and one uncomplemented binding | Complement partitions; overlap/gap errors for declared alternatives; uncomplemented off yields NotFound (W014) |

## W011 — Services

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W011-A01 | Singleton shared across messages, scoped shared within message, transient new per resolution | Capturing shorter-lived service/msg/lambda into longer owner diagnosed with capture path (W005,W012) |
| W011-A02 | `requires`, `exposes`, contract implementation, `[bind]` and `[bind.test]` profiles | Missing/duplicate implementation, inaccessible nonexposed boundary, dependency cycle diagnosed (W032) |
| W011-A03 | Public exposed method stamps returns and receives triggers; internal helper follows declared boundary rules | Provenance/ambient public summary violation at boundary; out-parameter stamps preserved (W006,W008) |
| W011-A04 | `config T` from Config with Secret fields; startup validation before serving | Invalid/missing config fails startup; secret flow rejected at sink (W007,W031) |
| W011-A05 | Reentrant, serialized, partitioned(key) calls under controlled contention | One-at-a-time globally/per-key as declared; different keys may progress; cancel queued work without executing it twice |
| W011-A06 | Singleton health probes gate Signal.Start and pipeline opening | Failed probe blocks readiness with named dependency; shutdown joins and disposes under deadline (W029) |

## W012 — Ambients

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W012-A01 | Declare typed/custom ambient; provider assigns on all fallthrough paths | Unassigned promised provider diagnosed at missing branch; bypassed provider supplies nothing (W009) |
| W012-A02 | Required bare read versus optional `X.Current`; service call graph uses hidden context | Missing required read/call at use with entry-to-use counterexample; optional absent is Option.None |
| W012-A03 | Nested calls and parallel messages carry separate Correlation/Tenant values | No thread-local/AsyncLocal dependence; cross-message contamination forbidden (W005) |
| W012-A04 | Lambda/static/singleton/detached task attempts to capture ambient | Lifetime error at capture/storage with scope owner; owned in-scope task succeeds |
| W012-A05 | Send/receive Correlation and Deadline across configured transport | Only declared carried fields move; Principal absent by default; forged/invalid header follows adapter policy (W019) |
| W012-A06 | Nested deadline narrows outbound HTTP/DB/cache budget | Longer deadline warns and cannot extend parent; every registered I/O honors remaining budget (W017,W031) |

## W018 — Standard library

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W018-A01 | Id<T>, Option<T>, Page<T>, Message<T>, Response<T> construction, match, serialization | Cross-type Id, absent Option dereference, invalid response type at use (W001,W019) |
| W018-A02 | Secret<T> and generated model metadata feed validation/serialization/logging | Implicit secret reveal rejected or redacted under declared policy; no reflection escape (W006,W007) |
| W018-A03 | Problem payload with message/field/details and every symbolic outcome | Origin-incompatible/custom unmapped outcome at terminal; transport-independent problem preserved (W019) |
| W018-A04 | Duration/Instant/time-zone/calendar parsing and controllable Clock/Random | Invalid duration/time zone/refinement diagnosed; no culture or wall-clock nondeterminism in test profiles (W026,W031) |
| W018-A05 | Money rounding, explicit currency conversion, canonical wire form | Mixed-currency arithmetic and invalid amounts fail under decision 0002; decimal extremes covered (W002) |
| W018-A06 | Generated serializers, validators, logging and metrics across generic/nullable collections | Nullability/provenance preserved on decode; observer failures do not erase primary failures (W007,W008,W034) |
