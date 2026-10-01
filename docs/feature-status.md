# First-release feature register

**Release scope:** the complete current design plus additions accepted during development.
This register follows [roadmap.md](roadmap.md). The first release includes every feature
below on both backends; tentative semantics remain design work within that scope.

**Current state:** Phase 1 foundation is complete; Phase 2 is in progress. W001–W004 and
W032–W034 have executable foundation portions; no complete feature is claimed. The
cross-cutting language, message runtime, and production adapters remain required work.
The original examples are design sketches; `examples/foundation`, `examples/functions`,
`examples/classes`, `examples/properties`, `examples/initializers`, and
`examples/constructors` are executable. Phase 2 now supports
static helper methods, overloads, named/optional arguments, ordinary classes with constructors/fields/instance methods, instance
properties with accessors and auto-property initialization, object/nested initializers,
required members and init-only writes, constructor delegation and cycle checks, reference
initialization and visibility checks, int32-to-int64 widening, for/do loops,
break/continue, conditional expressions, and update/compound-assignment operators
on both backends; see the
[Phase 2 evidence](phases/02-language-and-message-runtime/tasks.md).
See [foundation progress](phases/01-design-and-compiler-foundation/tasks.md),
[acceptance cases](acceptance/README.md), and [shared contracts](contracts/ir-and-runtime.md).

The descriptions are an index into the linked specifications, not a replacement for
them or a limit on their contents. A documented subfeature remains in scope even if it
does not yet have its own row. Decompose rows into stable child IDs as implementation
work is planned; preserve the parent coverage.

## Language, compiler, and runtime

| ID | Feature and coverage | Design source  Primary task tracker |
|---|---|---|---|
| W001 | Ordinary language: expressions, statements, functions/callable values, models/classes/records, fields, collections, generics, nullability, exceptions, visibility/projects, and specified evaluation behavior. | [Compiler](compiler.md), [grammar](grammar.md)  [Phase 02](phases/02-language-and-message-runtime/tasks.md) |
| W002 | Platform semantics: integer/floating/decimal numerics, overflow, UTF-16 strings/comparison, equality, locks, paired external bindings, and generated facilities under the reflection policy. | [Compiler](compiler.md)  [Phase 02](phases/02-language-and-message-runtime/tasks.md) |
| W003 | Shared C# compiler: parsing, binding, typing, analysis, effect insertion, erasure, portable IR, diagnostics, and source-location preservation. | [Compiler](compiler.md)  [Phase 01](phases/01-design-and-compiler-foundation/tasks.md) |
| W004 | Both code-generation paths: Roslyn/C# and Java/javac, runtime packaging, source/debug mappings, and inspectable generated code. | [Compiler](compiler.md)  [Phase 01](phases/01-design-and-compiler-foundation/tasks.md) |
| W005 | Tasks and message runtime: async/await, combinators, delay, async iteration, cancellation/deadlines, structured task ownership, joining, disposal, and channels. | [Compiler](compiler.md), [ambients](ambients.md)  [Phase 02](phases/02-language-and-message-runtime/tasks.md) |
| W006 | Provenance: origin/model/service stamping, propagation, inference/signatures, field/container/alias/capture behavior, `from` matching, `via`, lineage, transforms/discharge, and erasure. | [Provenance](provenance.md), [open questions](open-questions.md)  [Phase 03](phases/03-cross-cutting-compiler/tasks.md) |
| W007 | Rules: vocabulary, all target/filter/point/action forms, named filters/sinks, static and runtime matching, precedence, rulesets/use/suppression, switch interaction, insertion, and diagnostics. | [Rules](rules.md)  [Phase 03](phases/03-cross-cutting-compiler/tasks.md) |
| W008 | Triggers: all boundary kinds/modifiers, enter/exit/throw, argument/result/site filters, point data with provenance, ordering, switches, and cost diagnostics. | [Triggers](triggers.md)  [Phase 03](phases/03-cross-cutting-compiler/tasks.md) |
| W009 | Middleware: scopes, origin refinement, transparent filters, guards, effects, unwind, literal/match/code routing, tables, `next any`, terminals, body refinement, bounded reentrancy, and graph proofs. | [Middleware](middleware.md), [overview](overview.md)  [Phase 03](phases/03-cross-cutting-compiler/tasks.md) |
| W010 | Receivers: multi-origin bindings, parameter binding, symbolic returns/rejection, guards, Request tagging, service/ambient requirements, lifetime checks, and boundary triggers. | [Receivers](receivers.md)  [Phase 02](phases/02-language-and-message-runtime/tasks.md) |
| W011 | Services: singleton/scoped/transient, dependency and exposed-contract binding, configuration/test profiles, capture/escape checks, boundary stamping, health, reentrant/serialized/partitioned modes. | [Services](services.md)  [Phase 02](phases/02-language-and-message-runtime/tasks.md) |
| W012 | Ambients: declaration, provider assignment, required/optional reads, middleware/call-graph availability, hidden-parameter lowering, capture checks, transport carry, and deadline budgeting. | [Ambients](ambients.md), [compiler](compiler.md)  [Phase 02](phases/02-language-and-message-runtime/tasks.md) |
| W013 | Caches: method/response targets, filtering, keys, TTL, invalidation, ignores, write coverage, tenant partitioning, derivability, provenance, and specified external-write/concurrency behavior. | [Caching](caching.md), [overview](overview.md)  [Phase 04](phases/04-effects-caching-and-switches/tasks.md) |
| W014 | Flags: types/defaults, sources/refresh/failure policy, static flags, per-message snapshots, ambient targeting, construct scopes, receiver conditions, retirement, and joint-state verification. | [Switches](switches.md)  [Phase 04](phases/04-effects-caching-and-switches/tasks.md) |
| W015 | Canaries: service/middleware/rule/trigger targets, arm contracts, split/shadow modes, sticky selection, metrics/judging, promotion, rollback, and safe effects. | [Switches](switches.md)  [Phase 04](phases/04-effects-caching-and-switches/tasks.md) |
| W016 | Kill switches: every documented scope, degradation and outcome contracts, transparency/fail-closed behavior, tripwires, reset/probes, source failure, and prohibition restrictions. | [Switches](switches.md)  [Phase 04](phases/04-effects-caching-and-switches/tasks.md) |
| W017 | Effect scopes: pure/idempotent/external classifications, transactions and outbox, deadlines, retry/backoff, saga steps/compensation/finality, and failure/recovery behavior. Tentative details require design completion. | [Effect scopes](effect-scopes.md)  [Phase 04](phases/04-effects-caching-and-switches/tasks.md) |
| W018 | Standard library: Id, Secret, Response, Problem, Page, Message, Option and other types required by the examples; time/duration, Money, refinements, validation, serialization, logging, and metrics contracts. | [Overview](overview.md), [compiler](compiler.md), [examples](../examples/orders.weft)  [Phase 02](phases/02-language-and-message-runtime/tasks.md) |

## Origins and infrastructure

Each origin entry includes the two platform implementations, address validation,
envelopes, binding, outcomes, ambient behavior, cancellation/lifecycle, and integration
tests. Adapter choices follow the design; complete the promised origin behavior using
appropriate existing infrastructure on each platform.

| ID | Feature and coverage | Design source  Primary task tracker |
|---|---|---|---|
| W019 | Origin framework: custom origins/adapters, address declarations/grammars, envelopes, symbolic outcome maps, provided/carried ambients, typed topic/channel payloads, sink contributions, and validation. | [Origins](origins.md), [rules](rules.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W020 | Http: routing and conflicts, parameter binding, headers/query/body, responses/problems, streaming contract, cancellation, and host integration. | [Origins](origins.md), [receivers](receivers.md), [roadmap](roadmap.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W021 | Ws: open/frame/close bindings, typed frames, connection lifetime/ambient, sends, and rejection. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W022 | Grpc: schema/contract addresses, typed envelopes, status mapping, and the agreed streaming/lifecycle behavior. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W023 | Queue: typed intake/send, consumer groups, delivery/attempt metadata, ack/dead-letter/retry/nack, and redelivery behavior. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W024 | Stream: partitions, offsets, groups/replay, ordering, commit/halt behavior, and partition ambients. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W025 | Channel: typed bounded in-process messaging, full policies/backpressure, sender, thread handoff, and request-reply behavior. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W026 | Timer: interval/cron/time-zone/one-shot bindings, schedule metadata, overlap policy, outcomes, and controllable-clock testing. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W027 | Tcp: port/framing contracts, connections, typed frames/bytes, remote metadata, and lifecycle. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W028 | Udp: datagram bindings, bytes/remote metadata, and outcome/lifecycle contracts. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W029 | Signal: process start/stop/reload and supported OS signal bindings, graceful shutdown, and deadline behavior. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W030 | Watch: filesystem patterns, path/change envelopes, event handling, and lifecycle. | [Origins](origins.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |
| W031 | Source-only origins: Db, Cache, Config, Env, Clock, Random; stamps, substitutions, configuration/secret rules, and typed database read/write/transaction contracts. | [Origins](origins.md), [services](services.md), [caching](caching.md), [effect scopes](effect-scopes.md)  [Phase 05](phases/05-origins-and-infrastructure/tasks.md) |

## Developer experience and release verification

| ID | Feature and coverage | Design source  Primary task tracker |
|---|---|---|---|
| W032 | Project tooling: `.weft`/`.rules` discovery and scope, `weft.toml`, projects/adapters/rulesets/bindings/profiles, build/check/run/emit, and compiler/runtime dependency management. | [Overview](overview.md), [compiler](compiler.md), [services](services.md), [roadmap](roadmap.md)  [Phase 06](phases/06-integration-and-developer-experience/tasks.md) |
| W033 | Inspection and editing: meaningful diagnostics, graph inspection, rule matches/insertions/runtime costs, flag reporting, formatter, language-server navigation/diagnostics, and source debugging. | [Rules](rules.md), [middleware](middleware.md), [switches](switches.md), [roadmap](roadmap.md)  [Phase 06](phases/06-integration-and-developer-experience/tasks.md) |
| W034 | Conformance and integration: specified expectations for both backends, invalid-program tests, interaction/failure/recovery tests, controllable sources, real adapter tests, and reproducible performance/resource checks. | [Compiler](compiler.md), [roadmap](roadmap.md)  [Phase 06](phases/06-integration-and-developer-experience/tasks.md) |
| W035 | Full release delivery: reference applications covering the language, complete docs/examples, installation/packages, reproducible builds, deployment/debugging instructions, and the full-release gate. | [Roadmap](roadmap.md)  [Phase 07](phases/07-full-release/tasks.md) |

## Updating progress and design

Concrete work is tracked in the [phase folders](phases/README.md) with stable task IDs
and Markdown checkboxes. Each row links its primary tracker; related phases must also
finish their required integration work before the feature is complete.

Use these states as work develops: **Design**, **Specified**, **Implementing**,
**Implemented**, **Verified**, **Complete**. Track compiler, .NET runtime, JVM runtime,
tests, and documentation separately inside a feature's detailed record where their
progress differs. Use **Needs revision** when an accepted design change invalidates
part of an existing record; preserve unaffected evidence.

A detailed record contains:

- Stable ID, current semantics, design decision links, and examples.
- Dependencies and affected feature interactions.
- Compiler/IR, .NET, JVM, diagnostics, test, and documentation work.
- Implemented paths and links to code; incomplete paths and concrete remaining work.
- Conformance and integration evidence with the revision checked.
- Open designer decisions and the next ready engineering task.

**Complete** means the feature and its required interactions satisfy the current
contract on both backends and are documented. It does not mean merely that the syntax
parses, a prototype runs, or the backends agree with each other.

Add stable IDs for accepted additions and update their design docs, acceptance cases,
dependencies, and release coverage together. Pending ideas can be recorded with their
open design questions. AI may propose changes; removing release coverage requires an
explicit designer decision.
