# Tooling, integration, and delivery acceptance

Use the [acceptance protocol](README.md). Phase 1 establishes executable foundations;
Phases 6–7 complete every product/integration/release behavior below.

| Case | Program/setup and expected outcome | Negative/failure expectation; interaction |
|---|---|---|
| W032-A01 | TOML manifest discovers .weft/.rules plus local source groups in stable order | Missing/bad TOML/source/backend/unknown section has file location and actionable code; no silently ignored rules or unsupported references |
| W032-A02 | CLI check/build/run/emit from paths containing spaces, both backends, explicit output and JSON diagnostics | Correct exit/stdout/stderr/artifacts; missing tool/runtime/invalid entry and cancellation fail clearly |
| W032-A03 | Separate project references/public exports, matching DLL/jar and Weft metadata, explicit pipeline-use files, packages, adapter/ruleset selection, service bindings and test profiles | Missing or incompatible binary/metadata, inaccessible export, duplicate/conflicting dependency fail; reference/source discovery never implies middleware activation; profile isolation (W006,W009,W011,W019) |
| W032-A04 | Compiler/runtime package installation, pinned dependencies, ABI/capability changes and repeat builds | Missing/incompatible operation before execution; no stale incremental analysis; clean install builds both targets |
| W032-A05 | Per-scope source/Clock/Random/switch test overrides | Overrides deterministic and isolated; production scope unaffected; incompatible override diagnosed (W031,W034) |
| W033-A01 | Source diagnostics through parser, semantic passes, inserted nodes and downstream compilers | Stable code/severity/span/related origins; error recovery retains later independent errors |
| W033-A02 | Rules inspection: targets/matches/insertions, suppression/precedence and runtime residue/cost | Counts reflect emitted sites; broad trigger/dead rule/static residue diagnostics agree with compiler (W007,W008) |
| W033-A03 | Graph view with selected receiver/origin paths, providers, dynamic admissible sets and counterexamples | Visual explanation names actual failing path/state; no dynamic edge outside emitted set (W009) |
| W033-A04 | Flag report with use sites, age/retirement, groups and active proof states | Controlled date drives warning/error thresholds; stale/unknown names not hidden (W014) |
| W033-A05 | Formatter roundtrip and language-server completion/navigation/rename/diagnostics for all declaration kinds | Formatting preserves meaning/comments; rename respects scopes and updates references; incomplete file remains usable |
| W033-A06 | Breakpoint/step/stack trace on both backends through generated policy code | Weft source/PDB/SMAP mapping including parent insertion site; generated host line never the only explanation |
| W034-A01 | Every executable case has independent compile or runtime expectations for both targets | Missing expectation fails, unsupported future case is not a passing skip; backend agreement alone insufficient |
| W034-A02 | Controlled gates/clock/random/snapshots enumerate meaningful race orders | Exact values/failures/counts plus allowed partial order; owned task closure before disposal; bounded hang failure |
| W034-A03 | Full-reference apps mix provenance/rules/triggers/graph/services/ambients/cache/switch/effects across all origins | Success plus forbidden path/reject/throw/cancel/rollback/abort/shutdown/recovery assertions; neither backend omitted |
| W034-A04 | Real adapter stacks and host-framework seams, failure injection and process restart | No mock-only production completion; delivery/commit uncertainty and external-write limits explicit |
| W034-A05 | Reproducible compile scaling, code size, allocation, throughput, latency, load/resource tests | Budgets recorded with environment/workload; leaks/deadlocks/regressions tracked and fixed before release |
| W034-A06 | Clean-source local build/run checks in Phase 1; hosted SDK/JDK checks deferred to Phase 6, with saved evidence | Meaningful required checks run; missing toolchain fails rather than skips; hosted success is never inferred from local success |
| W035-A01 | Reference applications collectively exercise every feature/subfeature and required interaction | Coverage audit catches orphan design paragraph/case/phase task; accepted additions gain stable IDs |
| W035-A02 | Install from release package, build/run/debug/deploy each target using only documented steps | Independent clean environment succeeds; missing runtime/config/actionable error tested |
| W035-A03 | Complete language/tutorial/operational/deployment/debugging docs and validated examples | No stale sketch presented as runnable; supported versions and known limits stated accurately |
| W035-A04 | Full release audit of all phases, both backends, failures/recovery/performance and documentation | No feature removal or implementation gap hidden by a version label; full-release gate blocks incomplete coverage |

Foundation evidence: [ProjectTests](../../tests/Weft.Tests/ProjectTests.cs),
[ConformanceTests](../../tests/Weft.Tests/ConformanceTests.cs),
[ScheduleTests](../../tests/Weft.Tests/ScheduleTests.cs), and the local
[verification entrypoint](../../eng/verify.sh). Hosted CI is deferred to P06-030;
there is no active workflow and no hosted execution result is claimed.
