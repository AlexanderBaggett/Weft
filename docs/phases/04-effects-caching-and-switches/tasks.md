# Phase 04: Effect scopes, caching, and switches

**Status:** Not started.

**Outcome:** Complete transactions, deadlines, retries, compensation, caches, flags, kill switches, and canaries with shared semantics and paired runtime implementations.

**Prerequisites:** Relevant analyses and runtime contracts from [Phases 01–03](../README.md). Database, broker, and host integrations in [Phase 05](../05-origins-and-infrastructure/tasks.md) are required for final production verification; contracts and in-process tests can advance before those integrations finish.

**Feature coverage:** W013, W014, W015, W016, W017, W018, W031, W033. See the [feature register](../../feature-status.md) for scope and the [phase index](../README.md) for primary ownership.

All tasks contribute to the first full release on both backends. Follow the [tracking rules](../README.md#tracking-rules). Resolve design choices with the designer while progressing independent engineering work.

## Effect classification and scopes

- [ ] P04-001 — Resolve classification declaration/inference, transitive effects, unclassified/extern behavior, transaction enlistment, and saga recovery semantics with the designer. (W017)
- [ ] P04-002 — Implement pure/idempotent/external and transactional effect summaries through ordinary, indirect, service, and compiler-generated calls. (W017)
- [ ] P04-003 — Implement transaction syntax, enlistment, commit/rollback, nested behavior, and rejection of incompatible external effects. (W017)
- [ ] P04-004 — Implement outbox participation and after-commit delivery contracts; integrate with database and messaging adapters from Phase 05. (W017, W023, W024, W031)
- [ ] P04-005 — Implement deadline blocks, nesting, timeout mapping, cancellation propagation, and interaction with message/connection budgets. (W005, W012, W017)
- [ ] P04-006 — Implement retry eligibility, attempts/backoff, failure selection, cancellation, and the agreed interaction with Queue/Stream delivery retry. (W017)
- [ ] P04-007 — Implement saga steps, compensation/finality checks, reverse compensation, compensation failures, and the specified recovery/durability behavior. (W017)
- [ ] P04-008 — Implement the required effect-scope runtime operations on .NET. (W017)
- [ ] P04-009 — Implement equivalent effect-scope runtime operations on JVM. (W017)
- [ ] P04-010 — Verify transaction/outbox failure windows, retry idempotence, deadline expiry, and compensation failures using both actual adapter stacks. (W017, W031)

## Caches

- [ ] P04-011 — Resolve cache visibility, transaction ordering, concurrent read/write races, versioning, and deliberate external-write limitations. (W013)
- [ ] P04-012 — Implement cache declarations, method/response targets, filters, keys, TTL, invalidate/ignore clauses, and cache metadata. (W013)
- [ ] P04-013 — Implement write-coverage analysis, key derivability/adequacy, automatic ambient partitioning, and precise stale-cache diagnostics. (W013, W031)
- [ ] P04-014 — Preserve service/source provenance and downstream policy behavior on cache hits, misses, invalidation, and refresh. (W006, W013)
- [ ] P04-015 — Implement the .NET cache runtime, including TTL, invalidation, response-cache behavior, and transaction integration. (W013)
- [ ] P04-016 — Implement the JVM cache runtime with equivalent semantics. (W013)
- [ ] P04-017 — Verify cross-tenant isolation, stale-cache diagnostics, TTL and concurrent invalidation, commit/rollback visibility, and external invalidation behavior on both backends. (W013, W017, W031)

## Flags and interacting states

- [ ] P04-018 — Implement flag declarations, typed defaults, static flags, sources/refresh/unreachable policy, retirement, and per-message snapshots. (W014)
- [ ] P04-019 — Implement every documented flag scope and receiver condition, per-state rule precedence, complementary bindings, and off-state transparency. (W007–W009, W010, W013, W014)
- [ ] P04-020 — Implement ambient-targeted reads and their availability proofs, including first-read snapshot behavior. (W012, W014)
- [ ] P04-021 — Resolve and implement sound verification of all allowed interacting switch states, bounded groups, and enforcement of any designer-approved state restrictions. (W014–W016)
- [ ] P04-022 — Implement the .NET switch source/snapshot/refresh runtime and deterministic test overrides. (W014)
- [ ] P04-023 — Implement the equivalent JVM switch runtime and test overrides. (W014)
- [ ] P04-024 — Implement flag use/age reporting and retirement diagnostics; verify source outages, concurrent changes, state combinations, and targeting. (W014, W033)

## Kill switches

- [ ] P04-025 — Implement every documented kill scope, fallback/outcome checking, default/failure direction, and forbid restrictions. (W016)
- [ ] P04-026 — Re-prove provider availability and routing when killed; implement legal degradation and fail-closed paths. (W009, W012, W016)
- [ ] P04-027 — Implement trip metrics, aggregation windows, reset/probes, half-open behavior, and origin pause/resume contracts. (W016, W018)
- [ ] P04-028 — Implement .NET kill evaluation, state transitions, degradation, and metrics plumbing. (W016)
- [ ] P04-029 — Implement equivalent JVM kill behavior. (W016)
- [ ] P04-030 — Verify trip/reset races, source failures, queue intake pause/resume, degradation typing, and interactions with flags and graph requirements. (W014, W016)

## Canaries and operational integration

- [ ] P04-031 — Resolve shadow-side-effect isolation, diff semantics, metric windows/sample requirements, promotion/rollback, and target-specific contract compatibility. (W015)
- [ ] P04-032 — Implement canary binding for services, middleware, rules, and triggers, including arm compatibility and per-arm path/outcome proofs. (W015)
- [ ] P04-033 — Implement split/shadow selection, percentages, sticky ambient targeting, judge expressions, promotion steps, and rollback policy. (W012, W015)
- [ ] P04-034 — Implement .NET canary execution, diff/metric collection, judging, selection changes, and cleanup. (W015, W018)
- [ ] P04-035 — Implement equivalent JVM canary behavior. (W015, W018)
- [ ] P04-036 — Verify shadow effects, rollout/rollback failures, provider contracts, switch snapshots, cache interaction, and transaction/retry behavior across both backends. (W013–W017)
- [ ] P04-037 — Complete operational diagnostics/reports, test substitutions, documented examples, feature evidence, and remaining integration tasks. (W013–W018, W033)

## Decisions and blockers

None recorded yet. Record the affected task IDs, the concrete semantic choice or blocker, the designer's decision when available, and any independent work that can continue.

## Evidence and next action

Implementation has not started. For completed work, record the task ID, revision or worktree state, relevant code/test paths, commands and results, and documentation changes. Keep partial backend progress explicit and record the next ready task for the following AI session.

The initial entry point is P04-001, subject to the prerequisite contracts above. Append newly accepted work with unused task IDs; preserve existing IDs and reopen tasks whose accepted contracts change.
