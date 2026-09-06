# Phase 01: Design map and compiler foundation

**Status:** Complete, 2026-09-06. All 23 foundation tasks are complete under the designer-approved CI deferral to P06-030.

**Outcome:** Establish the full design map and a shared compiler architecture that builds and executes generated programs on both .NET and JVM.

**Prerequisites:** The existing design documents and full-release policy. Semantic decisions are resolved as affected work approaches; unrelated compiler work can proceed.

**Feature coverage:** W001, W002, W003, W004, W019, W032, W034. See the [feature register](../../feature-status.md) for scope and the [phase index](../README.md) for primary ownership.

All tasks contribute to the first full release on both backends. Follow the [tracking rules](../README.md#tracking-rules). Resolve design choices with the designer while progressing independent engineering work.

## Design and portable contracts

- [x] P01-001 — Decompose the full feature register into acceptance programs, expected diagnostics, runtime outcomes, and feature-interaction requirements; retain coverage of every documented subfeature. (W001–W035)
- [x] P01-002 — Record ordinary-language decisions for evaluation order, mutation/aliasing, model/class/record behavior, generics, callable values, collections, equality, nullability, numeric behavior, and exceptions. (W001, W002)
- [x] P01-003 — Resolve the foundational provenance/discharge and rule-composition decisions with the designer, including replacement scope, precedence, forbidden flows, and effects introduced by inserted code. (W006–W008)
- [x] P01-004 — Specify message ownership, receiver selection, guards, rejection, exceptions, cancellation, unwind order, response commit/completion, child-task joining, and resource disposal. (W005, W009–W012)
- [x] P01-005 — Document IR and intrinsic requirements from caching, all switch kinds, transactions/retries/compensation, dynamic routing, custom origins, and streaming before finalizing shared interfaces. (W013–W017, W019)
- [x] P01-006 — Resolve portable task-combinator, numeric/decimal, generic-erasure, external-binding, and nullability contracts; select supported SDK/JDK versions and preview-feature policy. (W002, W005)
- [x] P01-007 — Reconcile declaration and block grammar with the complete design, including match/entry/outcomes, rule-file placement, caches, effect scopes, custom origins, and explicit middleware connections between projects. (W001–W019, W032)
- [x] P01-008 — Define intrinsic signatures, observable behavior, failure/cancellation behavior, and platform implementation obligations; keep host-specific types below the IR boundary. (W003, W004, W005)
- [x] P01-009 — Establish persistent design-decision and acceptance-case records; choose the canonical overview and update duplicate-document navigation without losing design content. (W001–W035)

## Compiler architecture and project setup

- [x] P01-010 — Create the C# compiler solution and component boundaries for parsing, semantic analysis, IR, .NET/JVM emitters, CLI, runtimes, and conformance tests. (W003, W004)
- [x] P01-011 — Implement source text, spans, diagnostic identities/severity, error recovery infrastructure, and origin tracking for compiler-generated code. (W003)
- [x] P01-012 — Implement the lexer and parser infrastructure, contextual-keyword handling, and syntax nodes that can represent the full declaration inventory. (W003)
- [x] P01-013 — Implement symbol tables, scope/project resolution, type representation, and shared semantic nodes used by subsequent language features. (W003)
- [x] P01-014 — Define typed IR, control flow, intrinsic calls, source mapping, validation, and extension rules; retain analysis data until the passes that need it finish. (W003)
- [x] P01-015 — Implement the initial .NET path from shared IR through Roslyn/C# to a runnable artifact with compiler errors translated to Weft locations. (W004)
- [x] P01-016 — Implement the initial JVM path from the same IR through Java/javac to a runnable artifact with compiler errors translated to Weft locations. (W004)
- [x] P01-017 — Implement initial weft.toml loading, source discovery, backend selection, and check/build/run/emit commands sufficient to exercise both compiler paths. (W032)
- [x] P01-018 — Create both runtime packages and intrinsic binding/version checks; report a missing or incompatible runtime operation clearly. (W004, W005)

## Conformance infrastructure and checkpoint

- [x] P01-019 — Implement a runner that executes each backend against specified expectations, including compile failures and source locations, rather than treating either backend as the oracle. (W034)
- [x] P01-020 — Define repeatable concurrency-test controls and assertions for allowed orderings, task lifetime, cancellation, and outcomes without depending on incidental scheduling. (W034)
- [x] P01-021 — Verify clean source can build and execute generated programs through both toolchains locally with documented prerequisites. Hosted CI moved to P06-030 at the designer's request on 2026-09-06. (W004, W034)
- [x] P01-022 — Verify the initial programs, malformed-source diagnostics, IR validation, downstream-error translation, and source-location preservation on both targets. (W003, W004, W034)
- [x] P01-023 — Update design, grammar, examples, feature records, and task evidence; record remaining semantic choices and the next ready tasks without marking later features complete. (W001–W035)

## Decisions and blockers

- **P01-002/P01-006/P01-007:** [decision 0002](../../decisions/0002-ordinary-and-portable-contracts.md)
  records the designer's 2026-09-06 answers: ordinary values with C# behavior wherever
  possible; common C#/Java numeric types with runtime support for host gaps; explicit
  middleware selection/connections between projects. The numeric inventory, decimal
  profiles, public contracts, source grammar, and pipeline-use syntax are reconciled.
  Implementation details and syntax spelling remain open to normal iteration, not
  unanswered prerequisite questions.
- **P01-021:** the designer explicitly deferred hosted CI to Phase 6. That part of the
  original task now lives in P06-030; the unrun workflow was removed. P01-021 retains
  clean local build/run verification. The previous publication question is superseded;
  no permission to publish is needed for Phase 1. No branch or PR has been published.
- [Decision 0001](../../decisions/0001-phase-1-semantics.md) was accepted by the designer
  on 2026-09-05 and is reflected in provenance, rule replacement, lifecycle, and Task
  contracts. Transport-completion hook spelling and later effect/switch details stay
  tracked in [open questions](../../open-questions.md); they do not remove release work.

## Evidence and next action

Evidence identifies this uncommitted Phase 1 worktree, based on `493296c`; no commit or
remote publication has been performed. The original full-language examples remain
sketches and receive WF2009 when executable checking reaches an unimplemented pass.

| Tasks | Deliverable and verification |
|---|---|
| P01-001 | [Acceptance map](../../acceptance/README.md): 203 unique families, all W001–W035 mapped, including the expanded numeric inventory; future cases explicitly unimplemented |
| P01-002/006/007 | Accepted direction in decision 0002; [numeric contract](../../contracts/numeric-types.md), [project pipeline contract](../../contracts/project-pipelines.md), grammar/precedence/terminology, decimal vectors, and implementation tasks reconciled |
| P01-003/004 | Accepted decision 0001; [message lifecycle](../../contracts/message-lifecycle.md); provenance/rules docs reconciled and acceptance families recorded |
| P01-005/008/014 | [IR/runtime contract](../../contracts/ir-and-runtime.md): full-feature interface requirements, intrinsic schemas, failure/cancel obligations, analysis retention and extension rules; typed IR validator tests |
| P01-009 | Persistent decisions and acceptance records; docs/overview.md chosen canonical after byte-equality verification, root overview now links there |
| P01-010–013 | Six C# projects, source/diagnostics, contextual lexer, structural full declaration inventory, ordinary typed syntax/binding/scopes; namespaces resolve across local source groups. Full separate-project references and feature binding remain Phase 2/3/6 work |
| P01-015/016 | Same IR emits runnable .NET DLL/PDB and JVM jar/Java; both downstream compilers tested with deliberate invalid generated calls mapped to Weft locations |
| P01-017/018 | TOML discovery, local source groups, check/build/run/emit, JSON diagnostics, both runtime packages, ABI/signature compatibility checks; CLI integration tests on both targets |
| P01-019/020/022 | 16 executable conformance cases on each backend plus unit/integration tests; controlled clock/gates/ordering harness; C# division overflow and project-pipeline syntax coverage. 67 passing, zero skips in final clean verification below |
| P01-021 | Fresh source copy restores/builds and executes the local verification script; both targets run the example, with final evidence below. Hosted CI remains P06-030 work |
| P01-023 | [Development guide](../../development.md), grammar/overview navigation, runnable foundation example, project-pipeline design sketches, acceptance/feature records, accepted decisions and next work documented |

Initial validation on .NET SDK 10.0.111 / JDK 26.0.2 (Java release 21 output):

- `dotnet test Weft.slnx --no-restore`: 56 passed, 0 failed, 0 skipped.
- `bash eng/verify.sh` in fresh copy `/tmp/weft-phase1-clean-bisqzc3_`, excluding all
  bin/obj/.weft/artifacts/.git/TestResults: restore/build succeeded with 0 warnings/errors;
  56 tests passed; CLI check and both backend runs succeeded, each printing
  `Weft on both runtimes: 20`.
- That clean snapshot's source SHA-256 was
  `169c5e6b88a6466ce6b49e87a5663e3e2e670bf3daa481e81038ab3df56975bf`.
  Subsequent changes added structural Outcome recognition, restored existing ignore
  entries, and updated acceptance/decision/evidence documentation; the current compiler
  suite was rerun after Outcome recognition. Hosted workflow results are not claimed.
- Local Markdown link audit: no missing file targets. Acceptance audit: no duplicate
  family IDs and no unmapped W001–W035 entries. `git diff --check` passed.
- A second fresh copy `/tmp/weft-phase1-jdk21-clean-dyfvy1vr` ran the same entrypoint
  under Temurin 21.0.12.1: 0 build warnings/errors, 56 passing tests, CLI check and both
  target runs succeeded. Source SHA-256:
  `b4748fee8143e8448ba0b41afc3d497cc698a1ab45dce785159ebce620afbdba`.
  The temporary JDK archive was obtained from Adoptium's official GitHub release and
  verified against its published SHA-256 before extraction. System Java was unchanged.

Continuation audit on 2026-09-06 found and corrected a function-resolution defect:
bootstrap Print/Log bypassed declared functions, and local call targets could fall back
to unrelated namespace/global functions. The new conformance program reproduced wrong
output on both backends, and three local-shadow cases reproduced erroneous successful
compilation. All five regression cases now pass. The tool runner also rejects an
already-canceled invocation before process launch, with a dedicated regression test.
`dotnet test Weft.slnx --no-restore` now passes **62 tests, 0 failures, 0 skips**, under
both OpenJDK 26.0.2 and Temurin 21.0.12.1. Each run exercises .NET and JVM output.
The earlier clean-snapshot counts/hashes above remain historical evidence; these
subsequent changes are verified by the updated suite in the current worktree.

Final completion verification on 2026-09-06:

- C# ordinary-value behavior, the expanded numeric inventory, and explicit middleware
  connections between projects are recorded in decision 0002 and the shared contracts.
  The old source-group name and nested-source project terminology were reconciled.
- The runtime now throws for minimum signed int/long divided by -1 on both targets,
  following C#. Focused backend checks cover overflow, zero division, and normal
  truncation. Existing conformance retains wrapping arithmetic and remainder checks.
- Shared-library, mixed local/shared, and whole-pipeline adoption examples parse as
  structural declarations and report WF2009 for their unimplemented graph passes.
  They do not claim working cross-assembly middleware yet.
- `bash eng/verify.sh` passed in fresh source copy
  `/tmp/weft-phase1-final-clean-r65k7hii`, with no prior bin/obj/.weft/artifacts/TestResults:
  .NET SDK 10.0.111 and Temurin JDK 21.0.12.1; restore and Release build succeeded with
  **0 warnings/errors; 67 tests passed, 0 failed, 0 skipped**. CLI check succeeded;
  both .NET and JVM runs printed `Weft on both runtimes: 20`.
- That snapshot's compiler/runtime/test/example/build-input SHA-256 is
  `2d260a975ccfc1364fc0a01ef934aa0ba86892e8a19a306042fd91b13caf7d07`.
  Only completion documentation changed after this run. Earlier JDK 26/21 results
  above remain historical verification, not a claim that a hosted matrix ran.
- Local Markdown links resolve; 203 unique acceptance families cover W001–W035;
  numeric JSON is valid; `git diff --check` passes. No active hosted workflow exists.

**Next:** Phase 2 begins with P02-001 and its dependent type/runtime work. Numeric
execution belongs to P02-008; actual cross-project middleware binding/packaging belongs
to P03-001 and P06-001/003. Hosted CI remains deferred to P06-030. The first release
still includes every language feature; completing the foundation does not complete
W001–W004 or any later feature family.
