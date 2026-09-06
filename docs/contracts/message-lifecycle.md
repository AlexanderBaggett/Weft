# Message lifecycle and task ownership

**Authority:** [accepted decision 0001](../decisions/0001-phase-1-semantics.md).
This is the shared contract for Phase 2 runtime work, Phase 3 graph lowering, and all
Phase 5 adapters. The bootstrap does not yet execute messages or async code.

## State transitions

1. **Accept:** the origin creates an envelope and a message scope, imports permitted
   carried ambients, and snapshots message-stable configuration/switch state.
2. **Select:** identify the receiver and binding (including origin/address/flag/`when`
   partition). This makes receiver-specific middleware scopes meaningful. Selection
   does not call the handler or require eagerly decoding its entire body.
3. **Traverse:** apply each node's scope/origin/filter, then guard, effect, and next.
   A scope/origin/filter bypass follows next but supplies no ambient and registers no
   after block. Guard rejection registers no after for that node. Entering effect
   registers its after exactly once, even if effect later fails or cancels. Bounded
   reentrant visits have distinct registrations.
4. **Bind/dispatch:** `Route` binds parameters for the selected binding, applies eager
   bind rules, verifies/provides the required refinements, and enters the receiver
   boundary. Receiver guard rejection precedes the body. Boundary triggers observe
   the specified enter/exit/throw stages; inserted calls undergo ordinary policy checks.
5. **Prepare:** a handler/terminal produces a response description or a failure/outcome.
   No transport bytes have been committed merely by constructing `Response<T>`.
6. **Unwind:** run registered after blocks in reverse order of the path actually taken.
   Await their work. A response description can still be changed here. When failure
   prevented response construction, response access must account for its absence;
   response-independent cleanup still runs. Cancellation must not skip cleanup.
7. **Join:** before buffered response commit, join message-owned work that can affect
   preparation. Normal completion waits. Failure, deadline, or abort requests child
   cancellation and then waits. Child failure becomes message failure before commit.
8. **Commit:** the origin maps the final outcome and response description once. Buffered
   completion then releases message resources; streaming keeps the resources required
   by the stream until its completion/abort and final child join.
9. **Dispose:** dispose scoped resources only after every task that may access them
   terminates. Reverse dependency order governs services. Origin completion and
   shutdown cannot bypass this ownership rule.

Connection/partition scopes can outlive individual messages. A child message does not
own or dispose its parent connection. All scopes record parentage and permitted
borrows; a longer-lived owner cannot capture a shorter-lived resource.

## Failure and cleanup

The primary failure is preserved. After-block/disposal failures are attached as
associated cleanup failures in observed unwind order; they do not replace it. With no
primary failure, a cleanup failure makes the message fail. All registered cleanup is
attempted. Origin mapping converts a pre-commit failure into an allowed failure outcome.
After commit, status replacement is impossible; adapters abort/close according to their
protocol and retain diagnostic context.

Cancellation is a request, not proof that a task stopped. An uncooperative external
operation keeps its borrowed resources alive and yields an operational diagnostic.
Forced service disposal is never a substitute for joining. A cleanup operation that
needs I/O must have an explicit cleanup budget; the exact library spelling remains
Phase 2 design work and cannot silently reuse an already-expired user-work budget.

`after` observes **response preparation**, not delivery to the remote peer. A separate
transport-completion observation hook remains required before streaming implementation;
its language/library name is an open design detail, not omitted release scope.

## Task contracts

| Operation | Success | Failure/cancellation |
|---|---|---|
| Spawn | Register child with its current scope before user work can escape | Refuse spawn into a closing scope; never detach implicitly |
| Await | Produce the task's value once complete | Rethrow its portable failure or cancellation; no host future wrapper |
| WhenAll | Empty input completes immediately; values preserve input order | Wait for all inputs; faults take precedence over cancellation; preserve all input faults; does not cancel siblings |
| WhenAny | Return the first completed **task**, even if that task failed/canceled | Empty input is an argument error; losers stay owned and running; awaiting the returned task observes its result |
| Delay | Complete when logical duration elapses | Honor cancellation; negative duration is invalid |
| Scope exit | Join all owned tasks, including WhenAny losers | Failure cancels then joins; service disposal follows terminal children |

Simultaneous completions have no specified winner. Async body execution may begin
before or after the call returns, as already documented in [compiler.md](../compiler.md).
Explicit synchronization establishes ordering; pre-first-await scheduling does not.

## Acceptance and deterministic control

The task and lifecycle cases in [the acceptance map](../acceptance/language.md) specify
allowed orderings. Tests use [concurrency controls](concurrency-testing.md), never
wall-clock sleeps as correctness evidence. For every origin run successful response,
guard rejection, effect rejection, receiver throw, after failure, cancellation before
dispatch/during I/O, child failure, streaming abort where supported, and shutdown.
