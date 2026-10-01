# Implementation phases

This directory turns the [roadmap](../roadmap.md) into executable work lists for the **first full release**. All seven phases and all accepted additions are required. The user leads language design; AI leads implementation and engineering.

Each phase owns a folder and a `tasks.md` document. The feature register describes release scope; the phase trackers describe concrete work and verification evidence. Phase 1 is complete, with local verification evidence; Phase 2 is in progress with ordinary functions, static methods, and structured control flow. Hosted CI was moved to Phase 6 at the designer's request. Tasks are checked only when their evidence is recorded.

## Phase index

| Phase | Task tracker | Main outcome |
|---|---|---|
| 01 | [Design map and compiler foundation](01-design-and-compiler-foundation/tasks.md) | Establish the full design map and a shared compiler architecture that builds and executes generated programs on both .NET and JVM. |
| 02 | [Language foundation and message runtime](02-language-and-message-runtime/tasks.md) | Implement the ordinary language, standard-library foundation, async/message lifetime machinery, and service/receiver runtime behavior on both platforms. |
| 03 | [Cross-cutting compiler machinery](03-cross-cutting-compiler/tasks.md) | Implement the full middleware graph, provenance, rules, triggers, lifetime/ambient proofs, and their combined lowering on both backends. |
| 04 | [Effect scopes, caching, and switches](04-effects-caching-and-switches/tasks.md) | Complete transactions, deadlines, retries, compensation, caches, flags, kill switches, and canaries with shared semantics and paired runtime implementations. |
| 05 | [Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) | Deliver every built-in origin, source-only origin, custom-origin mechanism, and infrastructure integration on both runtimes. |
| 06 | [Whole-language integration and developer experience](06-integration-and-developer-experience/tasks.md) | Make the full language usable through complete reference applications, diagnostics, editing/debugging tools, packaging, and verified feature interactions. |
| 07 | [Full-release completion and delivery](07-full-release/tasks.md) | Complete and deliver the entire accepted language design on both backends, with no missing first-release features. |

## Dependencies and implementation order

The numbers establish the main implementation sequence. A phase's prerequisites identify the contracts and facilities its tasks need; they do not require every unrelated task in earlier phases to finish. Tooling and tests develop alongside the compiler. Origin adapters can proceed once the shared lifecycle and relevant effect contracts are usable.

Phases 04 and 05 integrate together: effects/caches/switches need real infrastructure for final verification, while adapters need their effect and lifecycle contracts. Develop the contracts and implementations in dependency order, then complete the combined acceptance tests. Phase 07's release gate requires completion evidence from every phase and accepted addition.

## Tracking rules

Use `- [ ]` for an open task and `- [x]` for a completed task. A task ID such as `P03-014` remains stable when other tasks are added or reordered.

- Check a task only after its stated work and verification are complete. A task explicitly limited to one backend can be checked independently; its feature remains incomplete until all required backend and integration tasks are done.
- Keep an in-progress or blocked task unchecked. Record its partial work, decision, or blocker in the phase document using the task ID.
- Record evidence with the task ID: relevant code/tests, source revision or worktree state, commands/results, documentation, remaining issues, and the next concrete action.
- Make routine engineering decisions within the agreed design. Bring language semantic choices to the designer with examples and implementation implications; continue independent ready work while a choice is pending.
- Accepted features join the owning phase and feature register. Assign unused stable IDs, update dependencies and acceptance cases, and add a new phase folder if the work genuinely needs a separate phase.
- Reopen affected tasks when the accepted contract changes and revalidate affected interactions on both runtimes. Preserve unaffected work and evidence.
- A phase is complete when its tasks, accepted additions, and required cross-phase integrations are complete. Update the phase status and feature records together. All phases remain required for the first release.

## Primary feature ownership

The primary tracker coordinates a feature's completion. Other phases may supply its contracts, runtime pieces, integrations, debugging, or release verification; all such work still counts toward completion. Each task also lists the feature IDs it affects. Ranges such as `W006–W008` are inclusive.

| Feature | Primary tracker |
|---|---|
| W001 | [Phase 02: Language foundation and message runtime](02-language-and-message-runtime/tasks.md) |
| W002 | [Phase 02: Language foundation and message runtime](02-language-and-message-runtime/tasks.md) |
| W003 | [Phase 01: Design map and compiler foundation](01-design-and-compiler-foundation/tasks.md) |
| W004 | [Phase 01: Design map and compiler foundation](01-design-and-compiler-foundation/tasks.md) |
| W005 | [Phase 02: Language foundation and message runtime](02-language-and-message-runtime/tasks.md) |
| W006 | [Phase 03: Cross-cutting compiler machinery](03-cross-cutting-compiler/tasks.md) |
| W007 | [Phase 03: Cross-cutting compiler machinery](03-cross-cutting-compiler/tasks.md) |
| W008 | [Phase 03: Cross-cutting compiler machinery](03-cross-cutting-compiler/tasks.md) |
| W009 | [Phase 03: Cross-cutting compiler machinery](03-cross-cutting-compiler/tasks.md) |
| W010 | [Phase 02: Language foundation and message runtime](02-language-and-message-runtime/tasks.md) |
| W011 | [Phase 02: Language foundation and message runtime](02-language-and-message-runtime/tasks.md) |
| W012 | [Phase 02: Language foundation and message runtime](02-language-and-message-runtime/tasks.md) |
| W013 | [Phase 04: Effect scopes, caching, and switches](04-effects-caching-and-switches/tasks.md) |
| W014 | [Phase 04: Effect scopes, caching, and switches](04-effects-caching-and-switches/tasks.md) |
| W015 | [Phase 04: Effect scopes, caching, and switches](04-effects-caching-and-switches/tasks.md) |
| W016 | [Phase 04: Effect scopes, caching, and switches](04-effects-caching-and-switches/tasks.md) |
| W017 | [Phase 04: Effect scopes, caching, and switches](04-effects-caching-and-switches/tasks.md) |
| W018 | [Phase 02: Language foundation and message runtime](02-language-and-message-runtime/tasks.md) |
| W019 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W020 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W021 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W022 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W023 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W024 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W025 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W026 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W027 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W028 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W029 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W030 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W031 | [Phase 05: Origins and infrastructure integrations](05-origins-and-infrastructure/tasks.md) |
| W032 | [Phase 06: Whole-language integration and developer experience](06-integration-and-developer-experience/tasks.md) |
| W033 | [Phase 06: Whole-language integration and developer experience](06-integration-and-developer-experience/tasks.md) |
| W034 | [Phase 06: Whole-language integration and developer experience](06-integration-and-developer-experience/tasks.md) |
| W035 | [Phase 07: Full-release completion and delivery](07-full-release/tasks.md) |

See the [feature register](../feature-status.md) for the detailed feature descriptions and design sources. These work lists implement the full current design; accepted additions extend them as design and engineering progress.
