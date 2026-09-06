# Phase 05: Origins and infrastructure integrations

**Status:** Not started.

**Outcome:** Deliver every built-in origin, source-only origin, custom-origin mechanism, and infrastructure integration on both runtimes.

**Prerequisites:** Origin/message contracts and usable compiler/runtime paths from [Phases 01–03](../README.md). Transactions, caching, and operational integrations use contracts from [Phase 04](../04-effects-caching-and-switches/tasks.md); their final acceptance is coordinated across both phases.

**Feature coverage:** W019, W020, W021, W022, W023, W024, W025, W026, W027, W028, W029, W030, W031. See the [feature register](../../feature-status.md) for scope and the [phase index](../README.md) for primary ownership.

All tasks contribute to the first full release on both backends. Follow the [tracking rules](../README.md#tracking-rules). Resolve design choices with the designer while progressing independent engineering work.

## Origin framework and shared contracts

- [ ] P05-001 — Implement built-in/custom origin registration, adapter configuration, address grammar/refinements, and compile-time address diagnostics. (W019)
- [ ] P05-002 — Implement envelope generation/refinement, symbolic outcome mapping in match/code forms, provided/carried ambients, and sink contributions. (W019)
- [ ] P05-003 — Implement typed topic/channel declarations, sender/receiver payload agreement, binding conflicts, missing registrations, and unused-address diagnostics. (W019)
- [ ] P05-004 — Implement .NET adapter loading/binding and common request/message/connection lifecycle interfaces. (W019)
- [ ] P05-005 — Implement equivalent JVM adapter interfaces and binding. (W019)
- [ ] P05-006 — Build and verify a custom-origin example on both backends, including invalid addresses, outcomes, ambient carry, and policy checks. (W019)
- [ ] P05-007 — Establish actual-service/container/process integration fixtures and a supported platform matrix for every adapter pair. (W019, W034)

## Channel and Timer completion

- [ ] P05-008 — Complete production Channel origin behavior on .NET using Phase 02 primitives: envelopes, sender, typed sends, full policies, request-reply, and lifecycle. (W025)
- [ ] P05-009 — Complete equivalent production Channel origin behavior on JVM. (W025)
- [ ] P05-010 — Verify request-reply outcomes, overload policies, cancellation, scope transfer/escape checks, and thread handoff on both backends. (W025)
- [ ] P05-011 — Complete .NET Timer bindings for interval/cron/time-zone/one-shot schedules, envelope timestamps/overrun, overlap policy, and outcome handling. (W026)
- [ ] P05-012 — Complete equivalent Timer origin behavior on JVM. (W026)
- [ ] P05-013 — Verify invalid schedules, time-zone transitions, overlap/backoff, controlled-clock substitutions, cancellation, and shutdown on both backends. (W026)

## HTTP

- [ ] P05-014 — Resolve common HTTP route/binding rules, repeated and case-insensitive headers, query/body semantics, malformed input, resource limits, response commit, and streaming lifecycle. (W020)
- [ ] P05-015 — Implement the .NET/Kestrel HTTP adapter, generated route/binding/serialization code, outcomes/problems, streaming, disconnect cancellation, and shutdown. (W020)
- [ ] P05-016 — Implement the chosen JVM HTTP adapter with equivalent generated binding, wire behavior, outcomes, streaming, and lifecycle. (W020)
- [ ] P05-017 — Verify the same HTTP contract suite on both actual hosts, including routing conflicts, middleware-scope matching, bypass prevention, malformed input, response hooks, cancellation, and streaming. (W009, W010, W020)

## Source-only origins and persistence

- [ ] P05-018 — Specify typed Db reads/writes, data mapping, transaction participation, source stamps, effect summaries, and compiler-visible write boundaries. (W031)
- [ ] P05-019 — Implement .NET database integration and generated mappings using the selected driver/provider contracts. (W031)
- [ ] P05-020 — Implement equivalent JVM database integration and mappings. (W031)
- [ ] P05-021 — Verify database failures, commit/rollback, outbox persistence, cache invalidation, and provenance against both actual database integrations. (W013, W017, W031)
- [ ] P05-022 — Implement .NET Config/Env sources, secret handling, startup validation, test substitutions, and error behavior. (W031)
- [ ] P05-023 — Implement equivalent JVM Config/Env behavior. (W031)
- [ ] P05-024 — Complete Cache/Clock/Random source stamping and substitutions across both runtimes, integrating Phase 02 and Phase 04 implementations. (W031)
- [ ] P05-025 — Verify source-only values retain required provenance and policy behavior through configuration, secrets, cached values, time, and random operations. (W006, W007, W031)

## Queue and Stream

- [ ] P05-026 — Specify Queue payload encoding, consumer groups, at-least-once delivery, attempt/key headers, ack/dead-letter/retry/nack, deduplication, and shutdown. (W023)
- [ ] P05-027 — Implement the .NET Queue adapter with sends/intake, outcomes, retries, ambient carry, pause/resume, and backpressure. (W023)
- [ ] P05-028 — Implement the JVM Queue adapter with equivalent behavior. (W023)
- [ ] P05-029 — Verify actual broker redelivery, poison input, outbox delivery, cancellation, intake kills, failure windows, and restart on both platforms. (W016, W017, W023)
- [ ] P05-030 — Specify Stream encoding, partitions/offsets, ordering, groups, replay, commit/halt, retries, and rebalancing/lifecycle behavior. (W024)
- [ ] P05-031 — Implement the .NET Stream adapter with sends/intake, partition ambients, commit/halt, backpressure, replay, and shutdown. (W024)
- [ ] P05-032 — Implement the equivalent JVM Stream adapter. (W024)
- [ ] P05-033 — Verify actual broker partition ordering, replay, rebalancing, commit/failure boundaries, halt/retry, external cache invalidation, and restart on both platforms. (W013, W017, W024)

## Signal and Watch

- [ ] P05-034 — Implement the .NET Signal origin for documented lifecycle/OS signals, startup/health ordering, graceful stop, and deadlines. (W029)
- [ ] P05-035 — Implement equivalent Signal origin behavior for the supported JVM platform matrix. (W029)
- [ ] P05-036 — Verify process-level startup, reload, stop/signals, in-flight work draining, and deadline-limited shutdown on both platforms. (W029)
- [ ] P05-037 — Implement the .NET Watch origin for patterns, path/change envelopes, lifecycle, and specified duplicate/coalesced-event behavior. (W030)
- [ ] P05-038 — Implement equivalent JVM Watch behavior. (W030)
- [ ] P05-039 — Verify real filesystem create/modify/delete events, invalid configuration, bursts, shutdown, and platform-specific event normalization. (W030)

## WebSocket and gRPC

- [ ] P05-040 — Specify Ws open/frame/close ordering, frame types, connection-owned state/ambients, sends/rejection, backpressure, and cancellation. (W021)
- [ ] P05-041 — Implement the .NET Ws adapter and binding generation with complete connection lifetime behavior. (W021)
- [ ] P05-042 — Implement equivalent JVM Ws behavior. (W021)
- [ ] P05-043 — Verify real connections, frames, rejects/closes, shared connection state, abrupt disconnects, scoped resources, and shutdown on both platforms. (W005, W010–W012, W021)
- [ ] P05-044 — Specify Grpc schema/contract generation, typed addresses/envelopes, unary/streaming behavior, status outcomes, metadata, and deadlines. (W022)
- [ ] P05-045 — Implement the .NET Grpc adapter and generated contracts/bindings. (W022)
- [ ] P05-046 — Implement equivalent JVM Grpc behavior. (W022)
- [ ] P05-047 — Verify real cross-platform clients/servers, contract mismatches, status mapping, stream backpressure, cancellation, and deadlines. (W022)

## TCP and UDP

- [ ] P05-048 — Specify Tcp framing, connection ownership, byte/frame mapping, rejects, partial reads/writes, and resource/cancellation behavior. (W027)
- [ ] P05-049 — Implement .NET Tcp framing/bindings, send/receive, connection ambients, and lifecycle. (W027)
- [ ] P05-050 — Implement equivalent JVM Tcp behavior. (W027)
- [ ] P05-051 — Verify real sockets with fragmented/coalesced frames, invalid framing, peer close, cancellation, and shutdown on both platforms. (W027)
- [ ] P05-052 — Specify Udp datagram/address semantics, size bounds, remote metadata, loss/duplicate behavior, and outcomes. (W028)
- [ ] P05-053 — Implement .NET Udp bindings, datagram send/receive, outcomes, and lifecycle. (W028)
- [ ] P05-054 — Implement equivalent JVM Udp behavior. (W028)
- [ ] P05-055 — Verify real datagrams, remote metadata, malformed/oversized input, cancellation, and shutdown on both platforms. (W028)

## Whole-origin completion

- [ ] P05-056 — Verify identical source applications can select either backend and use every origin through the common graph, rules, services, and outcome machinery. (W019–W031)
- [ ] P05-057 — Complete adapter documentation/configuration/examples, record actual integration evidence per platform, and resolve remaining lifecycle or wire-contract mismatches. (W019–W031)

## Decisions and blockers

None recorded yet. Record the affected task IDs, the concrete semantic choice or blocker, the designer's decision when available, and any independent work that can continue.

## Evidence and next action

Implementation has not started. For completed work, record the task ID, revision or worktree state, relevant code/test paths, commands and results, and documentation changes. Keep partial backend progress explicit and record the next ready task for the following AI session.

The initial entry point is P05-001, subject to the prerequisite contracts above. Append newly accepted work with unused task IDs; preserve existing IDs and reopen tasks whose accepted contracts change.
