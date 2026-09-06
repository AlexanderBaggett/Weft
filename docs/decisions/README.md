# Design decisions

Decision records distinguish existing design commitments, engineering selections, and
language proposals awaiting the designer. Implementation does not make a proposal an
accepted language rule. Record the designer's response and revise dependent artifacts
when a decision is made.

| Record | State | Scope |
|---|---|---|
| [0001: Phase 1 semantic choices](0001-phase-1-semantics.md) | Accepted, 2026-09-05 | Provenance, rule composition, lifecycle |
| [0002: Ordinary and portable contracts](0002-ordinary-and-portable-contracts.md) | Direction and pipeline syntax accepted, 2026-09-06; engineering details identified | C# ordinary values, broader numerics, explicit middleware connections between projects; CI deferred |

The compiler is implemented in C#, emits C#/Java through one shared IR, and targets both
runtimes from the start as already decided in [compiler.md](../compiler.md). The
engineering baseline uses .NET SDK 10 and Java source targeting release 21, tested with
JDK 21 and 26. Phase 1 requires no JVM preview API. Libraries are pinned
in their project files. These selections do not restrict first-release feature scope.
