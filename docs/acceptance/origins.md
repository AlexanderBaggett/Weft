# Origin and infrastructure acceptance

Owner: Phase 5, with message/runtime foundations in Phase 2 and full-stack integration
in Phase 6. Use the [acceptance protocol](README.md). Every row runs on **both real
platform adapters** as well as a controlled driver; no adapter implementation exists in
the bootstrap. All origin families also run the lifecycle/failure matrix below.

## Shared adapter matrix — W019

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W019-A01 | Declare typed address/topic/channel and send/bind matching payload | Unknown address, payload mismatch and duplicate/conflicting binding error at use; unconsumed topic warning |
| W019-A02 | Custom Mqtt origin with typed/ranged address, envelope, adapter, provides/carries and outcome map | Bad qos/range/type at address; unavailable envelope/ambient at access; missing adapter counterpart at declaration |
| W019-A03 | Custom/built-in send contributes Egress/Persist point identity and stamps values | Rule sees custom sends and source roots; omitted unsafe binding metadata diagnosed (W006,W007) |
| W019-A04 | Exercise every built-in symbolic outcome plus custom Outcome/Problem through every reachable origin | Exhaustive mapping or wildcard; unmapped outcome error includes origin-to-terminal path (W009,W018) |
| W019-A05 | Carry Correlation/Deadline where supported; opt-in Principal; refine msg.Origin/body | Exact declared carry only, malformed metadata handled; common headers empty where absent (W012) |
| W019-A06 | Plug the same Weft middleware graph into paired host adapters | Receiver selection, parameter binding, prepare/unwind/commit semantics equivalent; host middleware bridge cannot bypass Weft proof obligations |

For **each W020–W030 adapter**, execute: startup/bind conflict; successful receive;
scope/filter bypass; guard/effect rejection; receiver throw; after throw; cancellation
before selection/during bind/during output; held child task; uncooperative borrowed
resource; shutdown; reconnect/recovery when supported. Assert exact protocol action,
single response/ack/commit, preserved primary failure, allowed ordering, joined tasks,
and disposal. Streaming/connection adapters additionally test post-header/frame abort
and parent connection lifetime. A transport without a body response must diagnose it
instead of inventing HTTP semantics.

## Built-in inbound origins

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W020-A01 | Http.Get/Post/Any route with path/query/header/cookie/body parameters | Verb/route conflict, missing parameter and invalid conversion at binding; bad body maps Invalid |
| W020-A02 | Refined Http msg fields and typed Body<T>; Response/Problem/headers and every outcome map | Correct status/body/header representation; unavailable non-Http field at access; no duplicate commit |
| W020-A03 | Stream chunks through response-preparation middleware, client disconnect and slow reader | Backpressure, after-before-commit, scope retained until stream ends, postcommit abort (W005,W009) |
| W020-A04 | Real .NET host and chosen JVM HTTP server route into Weft graph | Host request cancellation/deadline mapped; scoped services and host connection resources close in correct order |
| W021-A01 | Ws route Open→Frame→Close on one connection with typed frames | Shared connection lifetime/Connection ambient; message scopes distinct; frame type/route conflict at binding |
| W021-A02 | Respond sends frame; unauthorized reject closes with declared code; held sender on disconnect | Protocol mapping and bounded sends; no fresh HTTP response after upgrade; tasks joined before connection disposal |
| W022-A01 | Grpc schema/Weft contract addresses with typed requests/results | Schema/signature mismatch and unknown method at binding; outcomes mapped to statuses |
| W022-A02 | Unary, client/server/bidirectional streaming and peer cancellation | Ordered/backpressured streams under declared contract; cancel/deadline joins producers; status only before terminal commit |
| W023-A01 | Queue typed topic, competing consumers, named group, key/headers/body/enqueue time | At-least-once delivery with Attempt ambient; wrong/undeclared topic/payload at source |
| W023-A02 | Respond, Invalid, TooMany(after), wildcard reject | Ack, dead-letter, delayed retry, nack exactly as declared; no ack on failed preparation |
| W023-A03 | Redelivery after crash before/after ack, kill intake then resume, outbox delivery | Durable messages retained; retry attempts observable; idempotent handler/origin authority prevents assumed exactly-once (W016,W017) |
| W024-A01 | Stream typed partitioned topic, key/partitions, group and Earliest/explicit offset replay | Envelope Partition/Offset/Key/Body/Timestamp correct; within-partition order; Partition ambient available |
| W024-A02 | Respond commits offset; reject halts partition; another partition progresses | Never silently skip failed record; restart/rebalance uses committed position; offset conflict handled |
| W024-A03 | Retry/transaction/cache invalidation during replay and cancellation | Defined owner of retry; no duplicate unsafe side effects or invalidation preceding failed commit (W013,W017) |
| W025-A01 | Channel capacity with Backpressure/DropOldest and all accepted full policies | Exact accepted/dropped item sequence and Sender; blocked writer released by reader/cancel (W005) |
| W025-A02 | Typed thread handoff, producer/consumer end and owner shutdown | Payload types exact, no detached scope capture, close wakes/joins participants |
| W025-A03 | Fire-and-forget channel versus explicit request-reply channel | Body respond is compile error for ordinary channel; request-reply correlates result/reject/cancel exactly |
| W026-A01 | Timer every/cron/at+tz/after+once against controlled clock | Validated schedule, ScheduledAt/FiredAt/Overrun exact; invalid cron/zone/duration at address |
| W026-A02 | Hold first invocation across next tick, then `concurrent:true` variant | Default prevents overlap; explicit concurrency permits owned tasks; one-shot fires once |
| W026-A03 | Reject general/TooMany plus shutdown and daylight-saving transition | Log+Skip or Backoff; declared calendar policy tested at gap/fold before adapter completion |
| W027-A01 | Tcp port plus required framing, split/coalesced network reads, typed Frame/Bytes | Decoder reconstructs frames; missing/invalid framing or port conflict at declaration/startup |
| W027-A02 | Connection/Remote metadata, half-close/disconnect, held send, shutdown | Connection ambient/lifetime and cancellation obey protocol; malformed/oversize frame handled without leaked scope |
| W028-A01 | Udp port datagrams with bytes/Remote metadata | Datagram boundaries preserved; invalid address/size/refinement diagnosed or mapped under contract |
| W028-A02 | No-response/reject/loss/duplicate/out-of-order datagrams and shutdown | No reliable-delivery assumption; per-datagram ownership ends correctly; declared outcome behavior exact |
| W029-A01 | Signal.Start after health ready, Reload, Stop and supported OS signals | Enter ordinary graph/rules with correct envelope; unsupported signal binding diagnosed on target |
| W029-A02 | Stop under deadline while child tasks/services remain active | Stop intake, cancel/join per contract, preserve failure/operational diagnostics; no premature singleton disposal |
| W030-A01 | Watch `config/*.toml` Create/Modify/Delete events | Pattern validation, Path/Change envelope and scope; unrelated paths excluded |
| W030-A02 | Burst/coalesced events, watcher failure/restart and shutdown | Document adapter event-delivery guarantees; no assumed exactly-once; tasks/resources close under cancellation |

## W031 — Source-only origins and database contracts

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W031-A01 | Db query/write/update/delete with typed models and parameterized bindings | Db provenance/read-write metadata retained; invalid row/nullability/conversion diagnosed at boundary (W006,W013) |
| W031-A02 | Config/Env reads, validation, Secret fields, startup profile selection | Correct root stamps; missing/invalid config fails before intake; forbidden secret sink points to origin (W007,W011) |
| W031-A03 | Cache hit/miss/source stamp under injected store/clock | No provenance discharge on hit; TTL/external invalidation and cancellation contract (W013) |
| W031-A04 | Scoped Clock/Random substitutes and source-only origin access | Deterministic values/draws per test scope; source-only origin cannot be a receiver binding (W032,W034) |
| W031-A05 | Paired DB drivers, transactions, outbox, commit uncertainty and connection pool reuse | Type/effect/lifetime contracts preserved; no use-after-dispose, double commit, or silent partial rollback (W017) |
| W031-A06 | External process write followed by typed topic/stream invalidation or expiry | Visible event invalidates; invisible write remains explicitly outside compile-time proof (W013,W023,W024) |
