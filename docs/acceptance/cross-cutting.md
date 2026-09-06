# Cross-cutting acceptance

Use the [acceptance protocol](README.md). These are required full-release programs and
failure variants. Their production compiler/runtime passes are not implemented by the
Phase 1 bootstrap. W006–W009 belong to Phase 3; W013–W017 to Phase 4. Decisions 0001
and 0002 govern resolved/proposed shared contracts; remaining feature-specific choices
must be recorded before executable expectations are finalized.

## W006 — Provenance

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W006-A01 | Bind `LoginRequest from Http`, read model sourced from Db, call service and read source-only values | Correct roots/service stamps at every source; untagged source cannot be invented by annotation (W010,W011,W031) |
| W006-A02 | Copy/alias/return tagged value; concatenate/interpolate; read `.Field`, `.Length`, `.Count`; encode/decode | All derivatives retain required roots, unions sound at joins; forbidden sink error names origin-to-use path (W007) |
| W006-A03 | `List<string from Http>`, arrays, nested model fields, collection insertion and extraction | Element refinement retained separately from nominal type; unknown write cannot launder an element (W001) |
| W006-A04 | Root Http traverses S1/S2/repeated S1; query `from Http via S1`, `lineage crossed`, `hops` | Crossed-service set is not mistaken for ordered path/hop count; conservative summary cannot prove a false negative |
| W006-A05 | Mix Http/Queue roots; `from Http`, union, `only`, wildcard and optional-origin signature | Match exact documented set rules; invalid required-root/empty-provenance boundary at argument/return |
| W006-A06 | Explicit transform discharges its roots, preserving unrelated roots and separate transform lineage | Ordinary replacement/type conversion does not discharge; untrusted extern/indirect flow requires contract (W002,W007) |
| W006-A07 | Lambda/interface/delegate/public project signature carries inferred provenance summary | Captured tag retained; unsafe external/public summary at declaration/call with related origin (W001,W011) |
| W006-A08 | Cached/transformed values and trigger point args flow back to Log | Same provenance on hit/miss and generated calls; erasure occurs only after checking; no runtime tag object (W008,W013) |

## W007 — Rules

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W007-A01 | Target Request, `in {A,B}`, nominal/wildcard/field/service-return targets and source unions | Only selected values match; unreachable target warns with scope/site count (W006,W010) |
| W007-A02 | Type union/wildcard, attribute and field Name/Path/Type/Index/Optional filters | Static matching emits no runtime branch; impossible/dead filter warns at filter line |
| W007-A03 | Nested structural/property patterns, relational/type/or/and/not, `where`, regex/range predicates | Values match under declared C#-like patterns; invalid member/type/predicate diagnosed at filter (W001) |
| W007-A04 | Several filters AND; named file/ruleset filter reused; `||`, `&&`, `!`/not grouping | Cyclic/unknown named filter and inconsistent structural members diagnosed; prior filters narrow later ones |
| W007-A05 | Collection any/all and `each of` over empty/nonempty arrays | Any(empty)=false, all(empty)=true; per-element action visits selected elements once; tags preserved (W006) |
| W007-A06 | Lineage crossed/transformed/hops plus ambient `when` | Static lineage removed at erase; runtime condition reads proven ambient; absent ambient diagnosed (W012) |
| W007-A07 | Effects at default bind, explicit bind, bare/call/glob, after Fn, cross into/out/all, named Egress/Persist | Each point has correct caller/callee/site/arg/result metadata; invalid after-result access at use (W008,W019) |
| W007-A08 | Site predicate `point.Caller in Api.*`, argument index restriction, multiple points per effect | Restrict call sites independently of value filters; generated caller retains original policy context |
| W007-A09 | Shorthand Fn, explicit `_` arguments, void observer, require/reject/throw, replacement block | Replace type mismatch, incomplete replacement return, unsupported forbid point diagnosed at action |
| W007-A10 | Two `Log(req.User)` uses with lazy Sanitize; separately inspect req.User | Each selected use gets transformed; original value remains tagged; eager/explicit clean value avoids repeated work (decision 0001) |
| W007-A11 | Narrow/broad rules across method/type/namespace/project, target specificity, numeric priority, declaration tie | Specific winner replaces broader rule at that point; ties run stably with warning; all matching forbids still enforced |
| W007-A12 | Narrow runtime filter/flag false, broad rule true; structural scope list plus flag predicate | Broader fallback applies; structural scopes union and flag predicates intersect; no forbidden state (W014) |
| W007-A13 | `static rule` with static filters versus property/when predicates | Static-only emits no residue; runtime residue errors at exact offending lines; cost report names branches/sites |
| W007-A14 | Ruleset use, named rule/ruleset suppression scoped to Tests/project | Only declared scope suppressed; missing names diagnosed; file location does not alter application (W032) |
| W007-A15 | Inserted transform/observer calls a policy sink or requires an ambient/external effect | Generated code rechecked; recursive rule/site expansion errors with cycle path (W008,W012,W017) |
| W007-A16 | A broad forbid overlaps narrower transforming rule; already-explicit transform variant | Incoming forbidden flow fails despite narrower rule; already-discharged input can pass; cannot switch forbid (W014–W016) |

## W008 — Triggers

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W008-A01 | Method internal/external, service/all/named, receiver, transform targets | Internal requires both ends in scope; exact selected boundary counts; unsupported boundary at target |
| W008-A02 | Enter/exit/throw for success, throw, rejection and cancellation; sync observation | Result only on exit, exception only on throw, elapsed only terminal; wrong-stage member at use (W005) |
| W008-A03 | Args/result/name/count/site predicates and point caller/callee/ambient fields | Filtering selects correct boundary; point metadata preserves types and provenance (W006,W012) |
| W008-A04 | Two triggers with scope/priority/tied declaration order | All run in documented trigger order; trigger cannot replace argument/result (W007) |
| W008-A05 | Trigger logs point.Args from Http then observer itself fails | Value rules apply to generated Log; cleanup does not discard primary failure; insertion cycles detected |
| W008-A06 | Broad boundary trigger, flag on/off, kill at overhead threshold, canary backend | Correct per-site cost/count, snapshot-stable enablement, per-arm metrics and failure attribution (W014–W016,W033) |

## W009 — Middleware graph

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W009-A01 | Pipeline origin entries plus node origin/receiver scope selection | Missing entry errors; bypasses keep next but supply no effect/after/provider; receiver selected before scoped traversal (W010) |
| W009-A02 | Filter false/true, guard match/block approve/reject, effect respond/reject/throw | Guard must be exhaustive and not mutate msg; rejected guard does not register own after (W005) |
| W009-A03 | A→B→C success/failure; after logs each eligible visit | Reverse actually-taken unwind before commit; effect entry registers own after; primary/cleanup failures retained |
| W009-A04 | Literal next, condition match, subject match, code returning literal node/terminal | Nonexhaustive/missing next/nonenumerable return diagnosed with route branch; invalid terminal body/outcome diagnosed |
| W009-A05 | `provides Principal` and downstream `requires` across Http/Queue/Timer branches | Every reachable path satisfies provider; bypass/kill/flag-off counterexample prints exact path (W012,W014,W016) |
| W009-A06 | Origin-refined msg fields, common envelope, Body<T> parse node, union-origin paths | Unavailable field/body access at member with originating path; typed body keeps inbound tag (W006,W019) |
| W009-A07 | Table with declared codomain, refresh, invalid row, explicit fallback | Valid rows route only within codomain; unknown/ill-typed/out-of-set row rejected at load/use, fallback invoked (W031) |
| W009-A08 | `next any Lookup(Tenant) else reject Internal` | Derived admissible set enforces ambients/outcomes/cycles; rejected target cannot dynamically bypass proof |
| W009-A09 | Cycle, bounded `reentrant(max:N)`, dead node, nonterminating branch | Unbounded cycle/nontermination errors; bound enforced per visit; dead node warning; cleanup once per entered visit |
| W009-A10 | Explicitly select DLL/jar middleware nodes, connect local/shared nodes, embed an exported fragment, or adopt all entries of a complete pipeline using a source file | A reference alone activates nothing, even with no local middleware; conflicting entries/unbound continuations/invalid providers fail; private internals retain checked summaries; fixed connections cannot be silently replaced (W032, decision 0002) |
| W009-A11 | Handler short-circuit/stream response and after modifies headers | Response preparation precedes single commit; completion/abort owns resources through end (W020–W022) |

## W013 — Caches

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W013-A01 | Cache service boundary and Http.Send; filter true/false; configured/default keys and TTL | Hit bypasses memoized work only; filter miss executes normally; invalid target/key/TTL at declaration |
| W013-A02 | Db.Write/Update/Delete and typed Queue/Stream writes invalidate by field | Uncovered visible write warns with cache and write site; valid `.Id` derives matching key (W017,W023,W024,W031) |
| W013-A03 | `ignore Db.Update(Order.Status)` with unrelated writes | Ignore suppresses only that site/field; rest still checked; nonexistent/overbroad ignore diagnosed |
| W013-A04 | Target requires Tenant/partitioning ambient; same logical id in two tenants | Ambient automatically in key; explicit omission/type mismatch errors; no cross-tenant hit (W012) |
| W013-A05 | Miss result from service, hit result from Cache, downstream sanitation | Value provenance/refinement remains semantically equivalent; cache source stamp cannot discharge prior roots (W006,W007) |
| W013-A06 | External writer versus TTL/typed external invalidation | No false claim to prove invisible writes; no-TTL/no-external-source information diagnostic; expiry controlled by clock |
| W013-A07 | Hold fill across concurrent invalidation/commit/rollback | Generation prevents stale resurrection; rollback visibility and outbox ordering satisfy declared contract (W017) |
| W013-A08 | Flag/canary/kill cache with in-flight operations and store failure/cancel | Defined bypass/degraded behavior, stable message state, no duplicate side effects; transaction uncertainty explicit (W014–W016) |

## W014 — Flags

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W014-A01 | Declared typed default from Config/Db/Remote; refresh/unreachable last-known/value | Unknown name, invalid value/default/source diagnosed; deterministic refresh and failure policy |
| W014-A02 | First read then flip source during message; next message reads | Current value stable; next message observes refreshed state; by-Tenant requires ambient at every read (W012) |
| W014-A03 | Static flag and dynamic complement in rule/middleware/trigger/cache scopes and receiver when | Static dead arm erased; dynamic states all checked; disabled construct has declared transparent behavior |
| W014-A04 | Receiver same-address complementary conditions and lone conditional | Partitions checked; overlap/gap diagnostic for alternatives; lone off binding yields NotFound (W010) |
| W014-A05 | Grouped related flags, independent flags at defaults, oversize group | Enumerate specified joint states; bound error is actionable and never silently skips proof; unsafe combination counterexample |
| W014-A06 | Controlled date at retire, after retire, and +30-day boundary; `weft flags` | Warning then error at every use; listing reports age/sites; no dependence on uncontrolled test date (W033) |
| W014-A07 | More-specific flag rule toggled while broad fallback remains; forbid inside switched rule | Precedence recomputed per state; runtime forbid switch rejected even if default safe (W007) |

## W015 — Canaries

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W015-A01 | Service/middleware/rule/trigger baseline and candidate arms | Match exposes/from/provides/requires/outcomes/signatures/boundary kind; mismatch diagnosed at arm with related contract |
| W015-A02 | Split 5% with recorded draws; sticky by Tenant/Principal | Exact cohort under fixed selection input; unavailable targeting ambient at read; no per-call mid-message arm flip |
| W015-A03 | Shadow baseline plus candidate, baseline returned, recorded differences | Candidate external effect prohibited; candidate cannot mutate baseline state; provider-shadow semantics resolved before implementation (W017) |
| W015-A04 | Judge error/reject rates, p50/p95/p99, throughput, trigger overhead, shadow diff rate | Same logical window for arms; missing/insufficient samples not zero; invalid metric context diagnosed |
| W015-A05 | Manual/automatic promotion with steps, automatic/manual rollback and concurrent config updates | Atomic versioned transition, same verified graph contracts at every step; failure does not duplicate user effects |
| W015-A06 | Distinct middleware next edges, transform lineage under shadow, observer failures | Both graph paths checked; shadow result never replaces baseline; per-arm metrics preserve attribution (W006,W009) |

## W016 — Kill switches

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W016-A01 | Service fallback and receiver reject across all served origins | Degraded result type/contract and outcome maps checked; missing fallback diagnostic at killed scope |
| W016-A02 | Middleware transparent kill versus fail-closed kill on sole Auth provider | Unsafe bypass rejected with missing-provider path; fail-closed outcome valid and unwinds entered ancestors (W009,W012) |
| W016-A03 | Kill rule validator, trigger/glob, origin intake, and cache | Declared effects stop, queued intake retained, cache follows degradation; no undefined response (W013,W023) |
| W016-A04 | Trip threshold/window, reset interval, half-open probe percentage under controlled metrics | Deterministic state transitions, bounded probes, reopen/close behavior; invalid threshold/metric at declaration |
| W016-A05 | Default on/off, source/refresh failure, last-known versus kill | Declared fail direction honored; switch snapshot stable for message; no silent fail-open |
| W016-A06 | Kill/flag/canary targeting any rule containing forbid | Compile error at switch/rule relationship, regardless of default or fallback (W007,W014,W015) |

## W017 — Effect scopes

These features remain in the full release even where classification, transaction
participants, or stream retry policy still need designer decisions.

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W017-A01 | Declared/inferred pure/idempotent/external functions plus built-in I/O and unknown extern | Classification summaries cover inserted calls; pure cannot read ambients/I/O; unknown effects never assumed retry-safe |
| W017-A02 | Transaction enlists Db/transactional services; multiple writes and exception | Commit all participants under contract, rollback on precommit failure; external Email.Send forbidden at call |
| W017-A03 | Transaction writes Order and Outbox.Send, process failure before/after commit | Intent atomically persisted; transport sends only committed intents; recovery duplicates follow declared delivery contract |
| W017-A04 | Commit/cancel/timeout race and failed rollback | Committed/rolled-back/in-doubt distinguished; cleanup retains primary; no claimed rollback after uncertain commit |
| W017-A05 | Deadline nested inside/outside transaction/retry; I/O honors min budget | Longer inner deadline warning; timed-out task joined before resource disposal (W005,W012) |
| W017-A06 | Retry fixed attempts/exponential backoff with fake clock/random | Only eligible effects repeated, bounded attempts and budget; non-idempotent/unknown call error names call chain |
| W017-A07 | Saga ordered steps with compensations and final marker | Missing compensation error at step; reverse completed-step compensation after failure; finality obeyed |
| W017-A08 | Compensation throws, crash/restart between step and journal, uncertain external completion | Durable recovery does not invent completion; record compensation failures and operator-recoverable state |
| W017-A09 | Rule/trigger/cache wrapper introduces external/non-idempotent effect inside restricted scope | Rechecking inserted code reports originating policy and restricted call; no post-erasure policy bypass |
| W017-A10 | Stream handler with block retry, cache invalidation and transaction/outbox | Agreed retry authority controls partition halt/commit; no skipped offsets, stale cache, or duplicated non-idempotent effect (W013,W024) |
