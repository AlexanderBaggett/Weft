# Repeatable concurrency tests

Tests assert the language contract independently on each backend. Host scheduling order,
thread IDs, elapsed sleep time, and the other backend's output are not oracles.

The foundation harness [ControlledSchedule](../../tests/Weft.Tests/ControlledSchedule.cs)
provides named one-shot gates, a monotonic logical clock, cancelable waits, an event
trace, and before/after assertions. Its own tests verify that cancellation of one
waiter does not cancel a shared gate and that timers fire only on explicit advancement.
This is test infrastructure; it is not the Weft message runtime.

Phase 2/5 runtime tests expose equivalent controls through injected Clock/Random,
adapter completion gates, and scope-owned task hooks. Both platform adapters consume
the same serialized scenario and emit the same portable event fields: scenario, scope,
task, operation, state, and logical tick. Random draws use a supplied recorded sequence;
tests need not assume a particular host RNG. Real-adapter tests retain bounded wall-clock
timeouts solely to fail hangs and clean up subprocesses.

| Scenario | Control | Required assertions |
|---|---|---|
| Await order | Hold child at a named gate | Caller proceeds after completion; value/failure observed once |
| WhenAll failure | Fail A, hold B, then release B | Aggregate remains pending until B stops; no automatic B cancellation; faults retained |
| WhenAny winner | Release B before A | Returned task is B even if failed; A remains owned; scope cannot dispose before A stops |
| Completion tie | Release both before observation | Winner belongs to the inputs; do not require a particular one |
| Scope failure | Hold two children, fail parent | Cancel request precedes child terminal events; all terminal events precede service disposal |
| Uncooperative child | Ignore token, hold external completion | Operational diagnostic present; resource remains live until completion |
| Nested deadline | Advance just before/at deadline | No early timeout; effective deadline is min(parent, child); no extension |
| Streaming abort | Hold stream after headers | After-preparation precedes commit; stream scope lives after commit; abort joins before disposal |
| Channel backpressure | Fill capacity, hold reader | Next send stays pending; one read permits one acceptance; canceled send is not duplicated |
| Cache fill/invalidate race | Hold read/fill around invalidation | Old generation cannot publish after invalidation; tenant keys never collide |
| Transaction/cancel race | Gate commit acceptance | Outcome distinguishes committed from rollback/uncertain; outbox/cache visibility agrees |
| Switch change mid-message | Advance snapshot version between calls | Current message remains on its snapshot; next message sees the new version |

Compare exact values, failures, counts, and ownership closure. Compare partial orders
for events that may interleave. For a finite race scenario enumerate each meaningful
gate-release ordering, retaining the seed/script and full trace on failure. Never mark
a future semantic scenario as a passing skipped test: its acceptance record stays
unimplemented until both runtimes execute and satisfy it.
