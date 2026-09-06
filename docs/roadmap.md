# Roadmap to Weft's first full release

**Release policy:** the first release delivers the full language design on both .NET
and JVM, including features accepted as design and implementation progress. Every
documented feature is in scope. Tentative sections require design work and implementation;
their tentative status is not permission to omit them from the release.

Milestones order engineering work and provide evidence of progress. They do not define
reduced releases. Internal programs and builds can exercise progressively larger parts
of the language; the first release waits for the complete release criteria below.

**Working model:** the user is the primary language designer. AI is the primary
implementer and engineer. Design and implementation evolve together, and this roadmap
is maintained as that work proceeds.

The architecture follows [compiler.md](compiler.md): a compiler written in C#, a shared
checked and lowered IR, C#/Java source emission, and two implementations of the same
runtime contract. Reuse the host compilers, managed runtimes, and infrastructure
libraries. Concentrate Weft engineering on language semantics, analyses, lowering,
generated integrations, and runtime behavior shared across platforms.

## Release scope and its evolution

[feature-status.md](feature-status.md) records the full starting inventory, including
all origin families, operational features, effect scopes, and developer tooling.
Feature details remain authoritative in their linked design documents. The inventory
must expand as those documents acquire new accepted behavior.

- Both backends participate from the beginning. A feature is complete when the shared
  compiler and each required platform implementation satisfy the same contract.
- New ideas are welcome during implementation. The designer decides language behavior;
  AI supplies concrete examples, implementation implications, and recommendations.
- An accepted addition joins the first-release inventory unless the designer explicitly
  assigns it elsewhere. Update the roadmap, dependencies, and acceptance cases with it.
- Difficulty, an early successful demo, or the amount of remaining work does not justify
  deleting or deferring a feature. Resolve the engineering problem or present the
  specific design issue to the designer.
- Revisions can change an existing feature's semantics or syntax. Record the decision
  and migrate affected implementation, tests, and examples together. AI does not silently
  substitute a simpler language contract to make an implementation pass.
- Implementation alternatives already marked optional in the compiler design retain
  that meaning. The release inventory tracks promised capabilities and the chosen
  implementations needed to deliver them.

## Phase task trackers

The [implementation phases](phases/README.md) provide a folder and task-tracking document
for each of the seven phases below. Use their stable task IDs and `- [ ]` / `- [x]`
checkboxes to track work, decisions, verification evidence, and the next action. The
feature register links each feature to its primary phase; accepted additions extend
these trackers while remaining part of the full first release.

## How AI engineering proceeds

Organize work around concrete contracts and executable evidence, with persistent state
in the repository. Do not depend on a conversation retaining the entire project history.

For each piece of work:

1. Read the relevant design, feature record, dependencies, and current implementation.
   Identify the observable behavior and the proof obligations involved.
2. Resolve semantics that affect this work. Bring genuine language choices to the
   designer with representative programs and consequences. Make routine engineering
   decisions autonomously within the agreed design.
3. Define acceptance examples: valid behavior, expected diagnostics, failures, and
   interactions with existing features. A test's expectation comes from the language
   contract, not from whatever one backend currently does.
4. Implement the shared analysis/lowering and both platform paths where needed, reusing
   existing infrastructure through specified adapters. Keep generated code inspectable.
5. Validate the change and its affected interactions. Diagnose disagreements as possible
   implementation, specification, or test errors. Correct the cause; do not weaken the
   expectation merely to obtain agreement.
6. Update the feature record, design changes, documentation, and examples. Record the
   checks actually run, unresolved issues, and the next concrete work item.
7. Continue with ready work. A pending design question blocks its dependent work;
   independent compiler, runtime, adapter, or tooling work can proceed.

Each feature record should make the next AI work session able to resume directly:
current contract, relevant code/tests, decisions, implementation status for both
backends, verification evidence, and outstanding integration work. A parser stub,
isolated lowering, or working single-backend example is recorded as partial progress.

The roadmap is a dependency map. Independent work can advance together; foundational
changes receive integration checks before dependent work is considered complete. Work
boundaries follow the problem and available tooling, without assuming human staffing
or a fixed sprint schedule.

## 1. Establish the full design map and shared compiler foundation

Inventory the complete language before choosing IR and runtime interfaces. Identify
requirements from every feature, including late-bound routing, canaries, cache
invalidation, transactions, compensation, and streaming. This ensures early interfaces
can accommodate the full design without requiring every semantic question to be
settled before implementation begins.

Develop the normative semantics and intrinsic contracts as their implementations
approach. Start with the decisions that cross the most features:

| Contract | Questions to settle |
|---|---|
| Ordinary code and platform semantics | Evaluation order, mutation and aliasing, models/classes/records, generics and callable values, collections, nullability, exceptions, numeric behavior, equality, and public signatures. |
| Provenance and rule composition | Derivations, aliases, containers, service stamps, `via`, signature inference, transforms, scope composition, precedence, replacement, prohibitions, and effects introduced by generated code. |
| Message lifecycle | Receiver selection, guards, rejection, failure, cancellation, unwind order, child-task ownership, joining, disposal, and ambient propagation. |
| Responses and streaming | Response preparation, commit, completion, mutable state, streaming lifetime, backpressure, and which hooks apply at each point. |
| Effects and state | Read/write classification, transaction participation, retry and compensation semantics, cache visibility, flag snapshots, canary isolation, and metrics. |
| Proof boundaries | Public and external contracts, indirect calls, configurable destinations, interacting switches, bounded reentrancy, and analysis limits. |

Reconcile the grammar, examples, and detailed design as decisions are made. Maintain
one canonical overview. Any syntax in a documented feature must be accounted for in
the grammar and feature register.

Build the frontend, semantic model, IR, source emitters, runtime packages, CLI, and
conformance runner. Preserve source locations through every pass. Compile a small
program on both backends as the first engineering checkpoint, then extend the same
architecture across the full feature inventory.

**Checkpoint:** both toolchains execute generated programs locally from clean source;
hosted CI is deferred to Phase 6 by the designer's 2026-09-06 direction. Design and feature
records map the full release; new work has a repeatable path from semantics to tests.

### Known portability work

These issues need explicit implementation contracts; they are engineering tasks within
the full release:

- Define task combinator cancellation and exception behavior. .NET's `WhenAll` waits
  for all supplied tasks and `WhenAny` leaves other tasks running; Java's
  `ShutdownOnFailure` interrupts unfinished subtasks after failure. [Task.WhenAll](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?view=net-10.0),
  [Task.WhenAny](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenany?view=net-10.0),
  [StructuredTaskScope](https://docs.oracle.com/en/java/javase/21/docs/api/java.base/java/util/concurrent/StructuredTaskScope.html)
- Specify child-task tracking and joining in the .NET message scope as well as
  cancellation. Define cancellation while already waiting and scope exit when an
  external operation is slow to cooperate.
- Select supported SDK/JDK versions and a preview-feature policy. `StructuredTaskScope`
  is a preview API in JDK 21; keep that API choice behind Weft's runtime contract.
  [JDK 21 API](https://docs.oracle.com/en/java/javase/21/docs/api/java.base/java/util/concurrent/StructuredTaskScope.html)
- Implement the [numeric contract](contracts/numeric-types.md): common signed/unsigned
  integer widths, float/double, C# decimal behavior on both platforms, arbitrary-precision
  library types, and the distinct extended decimal128 contract. Runtime helpers bridge
  host differences; a missing primitive does not justify removing a numeric type.
- Specify debugger mappings, external calls, generic erasure, and nullability enforcement
  at platform boundaries, with executable checks of their actual behavior.

## 2. Implement the language foundation and message runtime

Complete ordinary syntax and semantics as defined by the evolving language spec:
declarations, control flow, models and ordinary code, generics, functions and closures,
collections, nullability, exceptions, numerics, strings, locks, and paired platform
interop. Supply the standard-library types and generated facilities those constructs
need, including serialization support.

Implement tasks, async/await, task composition, asynchronous iteration, cancellation,
deadlines, channels, clocks, and message scopes through the intrinsic contract. Use
controllable clocks and scheduling controls to make lifecycle tests repeatable.

Implement services and receivers with singleton/scoped/transient lifetimes, dependency
and contract binding, configuration, health, and all concurrency modes. Implement
ambients as hidden parameters, their capture rules, and transport propagation contracts.

Exercise Channel and Timer early to run the language and runtime end to end. These
origins establish reusable message machinery for every other adapter.

**Checkpoint:** the complete foundation has positive and negative conformance coverage
on both runtimes, including task ownership, escaping values, failures, and disposal.

## 3. Implement all cross-cutting compiler machinery

Build middleware graph checks and lowering for the documented routing forms: direct
destinations, match and code blocks, tables, `next any`, terminals, path refinement,
scope/filter transparency, guards, effects, unwind, and bounded reentrancy. Availability
proofs follow both the middleware graph and the call graph. Track origin and receiver
facts and reconstruct useful counterexample paths.

Implement provenance over the full language, with interprocedural summaries, fields,
containers, aliases, captures, service boundaries, transforms, and lineage queries.
Complete every rule filter, point, action, precedence rule, ruleset, suppression, and
static/runtime distinction. Add triggers with typed provenance-preserving point data.

Make insertion and analysis cooperate: a generated effect can introduce another sink,
ambient dependency, or side effect. Specify termination and rechecking, then validate
the resulting behavior before erasing proof information.

Use graph/dataflow and symbolic techniques appropriate to the contracts. Analysis
budgets must have clear diagnostics and preserve the promised guarantees. An unproved
program is not accepted by silently ignoring a path or feature interaction.

**Checkpoint:** all cross-cutting forms work on both backends, including combined use
and informative rejection of programs that violate their contracts.

## 4. Implement effects, caching, and operational controls

Develop these as their dependencies become ready, including in-process integration
before all network adapters are complete:

| Area | Complete behavior required |
|---|---|
| Effect scopes | Call classifications; transactions and outbox integration; deadlines; retries with idempotence checks; sagas, compensation, and their failure/recovery semantics. |
| Caching | Method and response caches; key generation/adequacy/derivability; TTL; invalidation and explicit ignores; provenance on hits; transaction and concurrent read/write behavior. |
| Flags | Declared types, sources, targeting, snapshots, static flags, all scope/binding uses, source failure behavior, retirement, and interacting-state verification. |
| Kill switches | All documented target constructs, type-checked degradation, fail-closed paths, trip metrics, reset/probes, and source failure behavior. |
| Canaries | All target constructs, compatible arms, split/shadow modes, sticky selection, metrics and judging, promotion/rollback, and side-effect isolation. |

The designer resolves tentative semantics through examples and implementation findings;
the feature remains tracked through completion. Verify all allowed switch combinations.
Checking one switch while holding the others at defaults does not establish safety for
arbitrary combinations; implement sound composition or explicit, enforced state contracts.

**Checkpoint:** every operational feature has compiler checks, two runtime
implementations where required, and tests of decisions, failures, and interactions.

## 5. Complete every origin and infrastructure integration

Implement the full inbound set: Http, Ws, Grpc, Queue, Stream, Channel, Timer, Tcp, Udp,
Signal, and Watch. Complete source-only Db, Cache, Config, Env, Clock, and Random
behavior. Implement custom origins, typed addresses, envelopes, outcome maps, adapter
registration, and ambient carry rules.

Use the sequencing in [compiler.md](compiler.md) to establish shared machinery:
Channel/Timer, then HTTP, durable messaging, lifecycle/filesystem events, and the
connection/framing-oriented origins. Advance independent adapters when their contracts
are ready. Every origin belongs to the first release on both backends.

Specify common wire behavior and host integration precisely: repeated headers,
binding, serialization, malformed input, resource bounds, streaming, connection lifetime,
cancellation, delivery/commit timing, retries, partition ordering, backpressure, and
shutdown. Use existing networking, broker, database, and serialization implementations
behind those contracts.

Complete typed database operations, transaction participation, and durable messaging
needed by the language's effects and cache proofs. Document and test the exact boundary
between compiler-visible writes and external changes.

**Checkpoint:** every origin has paired adapter coverage and integration tests against
its actual transport or source. Simulated adapters support tests but do not stand in
for a missing production implementation at release.

## 6. Integrate the whole language and developer experience

Introduce hosted CI here (P06-030), when integration work benefits from it. Reuse the
local checks that already catch meaningful failures. Keep workflow complexity and
release gates proportional to demonstrated needs; early compiler progress must not
depend on troubleshooting speculative CI infrastructure.

Expand the orders example into a reference service using the complete language.
Maintain additional focused applications for streaming, connection lifetimes, custom
origins, concurrency, and recovery so every feature has a realistic usage example.

Track feature interactions explicitly. Representative cases include:

- Rules and provenance through middleware, services, triggers, caches, and serialization.
- Scoped services and ambients through tasks, channels, streams, connections, and shutdown.
- Guards and `after` during rejection, exceptions, deadlines, and committed responses.
- Provider availability under flags, canaries, kill switches, tables, and reentrant paths.
- Cache visibility and invalidation around transactions, outbox delivery, and redelivery.
- Shadow execution, retries, and compensation around external and transactional effects.

Develop tooling alongside the compiler: project configuration, CLI build/run/check/emit,
test profiles and substitutions, formatting, language-server support, graph inspection,
rule explanations and cost reporting, flag reporting, source maps, and debugging.
Complete installation, dependency/runtime packaging, repeatable builds, and deployment
documentation for both targets.

Use correctness, cancellation, resource-leak, load, and recovery tests to find defects.
Profile generated behavior and improve its implementation while preserving the specified
semantics. Record reproducible workloads and results.

**Checkpoint:** complete applications exercise the integrated design, and the tooling
makes generated behavior, errors, and runtime operation understandable.

## 7. Complete and publish the first full release

The release is ready when all of the following hold:

1. Every feature in the current first-release inventory, including accepted additions,
   has finished semantics, implementation, diagnostics, and documentation.
2. Both backends satisfy the specification. Required runtime intrinsics, adapters, and
   integrations are implemented and tested on the supported platform matrix.
3. Conformance includes valid programs, invalid programs, runtime outcomes, failures,
   and feature interactions. Required cases have no skips masking unfinished features.
4. Reference applications use the full design and pass deployment, load, cancellation,
   and recovery checks. Known issues are classified honestly; feature gaps and broken
   language guarantees remain completion work.
5. Installation, compiler/runtime packages, tooling, debugging, examples, and user
   documentation are ready to use together.
6. The designer reviews the resulting language experience and accepts it for release.

Earlier engineering checkpoints provide evidence and opportunities to redesign. They
do not trigger a reduced public release or remove remaining completion work.

## Handling additions and redesign during implementation

For each proposed addition or revision, record a short design entry with motivating
programs, semantics, interactions, affected compiler/runtime contracts, and open choices.
The designer's acceptance updates the feature register and the relevant design docs.

AI then updates implementation dependencies and acceptance cases, migrates affected
code and examples, and revalidates the affected features on both backends. Mark prior
completion evidence as needing review when its contract changes. Keep unaffected
work progressing and preserve useful completed implementation.

This process applies throughout the roadmap. The release gate always refers to the
current accepted design, so the project can grow without losing track of what remains.

## Immediate next work

1. Decompose the full feature register into concrete acceptance cases and dependency
   records, preserving coverage of every documented behavior.
2. Work with the designer on message lifecycle, provenance/discharge, rule composition,
   task ownership, and the initial portable runtime contracts. Resolve other questions
   as their implementation approaches.
3. Establish the shared frontend/IR and both source-emission paths, with the conformance
   runner and persistent progress records from the first executable program.
4. Continue through the ready dependencies, extending semantics, implementations,
   tests, and documentation together until the full release gate is satisfied.
