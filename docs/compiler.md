# Compiler and backends

**Decision:** one compiler, written in C#, with two code generators from day one — .NET and
JVM. Everything above the IR is shared. Everything below it is per-platform. A JVM backend
bolted on later would inherit a .NET-shaped IR; building both from the start forces the
platform decisions into the language spec where they belong.

## Pipeline

```
.weft / .rules
   │
   ▼
frontend (C#)
   parse ──► bind ──► check ──► insert ──► erase ──► WeftIR
             │         │          │          │
             │         │          │          └─ provenance, ambients, switch states erased
             │         │          └─ rule effects, trigger effects, guards, middleware
             │         │             edges, cache wrappers become plain code + intrinsics
             │         └─ provenance, path proofs, lifetimes, outcome exhaustiveness,
             │            switch states, cache invalidation, address validation
             └─ symbols, types, scopes, rulesets
   │
   ├──► Backend.DotNet   WeftIR → Roslyn syntax trees → assembly   (NativeAOT optional)
   └──► Backend.Jvm      WeftIR → Java source → javac              (ASM bytecode later if needed)

weft-rt spec — the intrinsics the IR may call
   ├── weft-rt.dotnet
   └── weft-rt.jvm
```

Why source emission rather than IL/bytecode: Roslyn and javac do optimization, and the
generated code is readable — the "what you would have written by hand" property becomes
literal, and users get an eject path. `#line` on .NET and SMAP on JVM give debugging in
Weft source.

## The IR contract

WeftIR is post-check, post-insertion, post-erasure: plain typed code plus calls into a
fixed intrinsic set. The rule that keeps the seam honest:

> Nothing goes in the IR that one backend cannot lower.

No `Task`, `CancellationToken`, `Span`, `Thread`, `CompletableFuture`. Instead:

```
rt.task.spawn / rt.task.await / rt.task.when_all / rt.task.when_any / rt.task.delay
rt.cancel.token / rt.cancel.check / rt.deadline.remaining
rt.channel.open / rt.channel.send / rt.channel.receive
rt.switch.read / rt.switch.snapshot
rt.cache.get / rt.cache.put / rt.cache.invalidate
rt.origin.<name>.bind / rt.origin.<name>.respond / rt.origin.<name>.reject
rt.scope.enter / rt.scope.exit                       (message scope for lifetimes)
rt.metrics.record / rt.metrics.read                  (boundary metrics for judges/tripwires)
```

Each intrinsic has a spec entry: signature, semantics, error behavior. Both runtimes
implement all of them. The spec is the contract; the backends are implementations of it.

## Platform decisions made by the language

These are settled in the spec so neither backend invents semantics.

| Area | Decision | .NET lowering | JVM lowering |
|---|---|---|---|
| Async | `Task<T>` and `await` are language surface. See below. | state machines | virtual threads |
| Generics | compile-time only; no runtime type arguments, no `typeof(T)` | reified, unused | erased |
| Value types | none user-defined; `record` = value equality, reference layout | class | class (Valhalla later, maybe) |
| Numerics | Common C#/Java numeric types, including signed/unsigned widths, float/double, C# `decimal`, and arbitrary-precision library types; retain distinct `decimal128`. See [numeric contract](contracts/numeric-types.md). | native types plus runtime libraries | native types plus unsigned/decimal compatibility helpers |
| `Money` | decimal128 + currency, banker's rounding, spec'd | runtime type | runtime type |
| Nullability | non-null by default | NRT | annotations + runtime checks at `extern` edges |
| Exceptions | all unchecked | — | `RuntimeException` subclasses |
| Strings | UTF-16, ordinal comparison unless a `Culture` is passed | — | — |
| Overflow | C# checked/unchecked rules; runtime unchecked by default; signed minimum / -1 throws | native operations plus normalization where needed | checked/unsigned/division helpers where Java differs |
| Reflection | none; everything is generated | — | — |
| Interop | `extern dotnet` / `extern jvm`; a library binding platform code must provide both | — | — |
| Locks | `lock` in the language | `Monitor` | `ReentrantLock` — never `synchronized` (pins virtual threads pre-24) |

## Async and `await`

`Task<T>` and `await` stay in the language. The IR carries them as intrinsics; each backend
lowers them natively.

| Weft | .NET | JVM (21+) |
|---|---|---|
| `Task<T>` | `Task<T>` (`ValueTask` where the backend proves it safe) | `WeftTask<T>` over `CompletableFuture<T>` |
| `async` body | state machine | body on a virtual thread, future returned |
| `await t` | suspension | `t.join()` — parks the virtual thread |
| `Task.WhenAll` / `WhenAny` | specified Task behavior | WeftTask combinators; no automatic sibling cancellation |
| `Task.Delay` | same | `Thread.sleep` on a virtual thread |
| cancellation / `Deadline` | `CancellationToken` threaded by the compiler | `interrupt()` + runtime token checked at each `await` |
| exceptions | on the Task | `CompletionException` unwrapped by the runtime |
| `await foreach` | `IAsyncEnumerable<T>` | producer on a virtual thread + channel |

### Two lowerings on JVM, chosen by usage

A C# async method runs synchronously until its first `await`; a spawned virtual thread
starts concurrently. The compiler sees how the returned task is used:

- **Awaited immediately** (`await Foo()`) → direct call on the caller's virtual thread,
  blocking at inner awaits. Identical ordering and exception behavior to .NET, no spawn.
- **Stored, passed, or an async lambda** → spawn. The caller wants concurrency and must
  not rely on pre-first-await ordering.

The spec therefore says: *an async method's body may begin executing before or after the
call returns; ordering before the first suspension point is unspecified.*

### Not in the language

`ValueTask`, `ConfigureAwait`, `SynchronizationContext`, `async void`, `.Result`, `.Wait()`.
Backend details or footguns; none belong in a web tier.

### Fallback

If exact scheduling parity is ever required, the JVM backend can switch to a CPS transform
onto a continuation interface (Kotlin's approach). It is a backend-only change; nothing
above the IR moves. Not the starting point.

## Lifetimes and scopes at runtime

Every message gets a scope (`rt.scope.enter/exit`). `scoped` services and tasks spawned
inside it are owned by it. Both runtimes maintain an explicit ownership registry; the
.NET implementation uses linked cancellation and the JVM implementation uses a runtime
token plus virtual-thread interruption. No JVM preview API is required. Normal scope
exit joins children; failure requests cancellation and then joins. Disposal follows
child completion. `WhenAll` waits for every input before reporting failure; `WhenAny`
returns the first completed input without canceling the others. See the accepted
[lifecycle decision](decisions/0001-phase-1-semantics.md#c-message-lifecycle-and-structured-tasks).

## Ambients at runtime

Ambients lower to hidden parameters threaded by the compiler — no thread-locals, no
`AsyncLocal`. `Deadline` becomes the platform cancellation token; users never see it.

## Conformance

A suite of Weft programs with specified expected behavior runs locally on both backends.
Checks should demonstrate working features or catch real compiler/runtime regressions.
If backends disagree, identify whether the specification or an implementation is wrong
and correct it against the shared contract. Hosted CI and release gates are deferred
to Phase 6 (P06-030), as requested by the designer on 2026-09-06. The local verification
script remains available; pipeline maintenance is not a Phase 1 completion requirement.

## Sequencing

The first release includes the full language on both backends. This sequence orders
implementation checkpoints; all stages are completed before that release. Design and
implementation evolve together, with the user leading language design and AI leading
engineering. See [roadmap.md](roadmap.md) and [feature-status.md](feature-status.md) for
the living plan, accepted additions, and completion evidence.

Origins are the expensive part — each is two adapter implementations. Prove the seam on the
pure language first.

1. Expressions, models, services, rules, triggers, switches, caches — with `Channel` and
   `Timer` as the only origins. Both backends, conformance green.
2. `Http` on both: Kestrel / Netty (or the JDK's built-in server to start). Agreeing on what
   `msg.Headers` means will be harder than either adapter.
3. `Queue` and `Stream`: Confluent.Kafka / kafka-clients.
4. `Signal`, `Watch`.
5. `Ws`, `Grpc`, `Tcp`/`Udp` — last; they have the least shared shape.

## Why not LLVM

Everything that makes Weft interesting is frontend work and it all erases. On LLVM the
runtime — GC, async, TLS, HTTP, gRPC, Kafka, JSON, DB drivers — is yours to write before
hello-world. That's a systems language's problem; this isn't one.
