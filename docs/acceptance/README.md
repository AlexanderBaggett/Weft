# Full-release acceptance map

This map decomposes every W001–W035 feature into stable acceptance families. It
supplements the original design documents; no feature disappears because its final
test program is not executable yet. Each row gives a program/setup, expected result,
negative diagnostic or failure case, and interacting features. Expand a family with
suffixes (`-01`, `-02`, …) when implementation needs additional vectors; preserve IDs.

| Record | Coverage |
|---|---|
| [Language and messages](language.md) | W001–W005, W010–W012, W018 |
| [Policies, graphs, effects, and switches](cross-cutting.md) | W006–W009, W013–W017 |
| [Origins and infrastructure](origins.md) | W019–W031 |
| [Tooling and delivery](tooling.md) | W032–W035 |

**Execution state:** only the explicitly linked files in `tests/conformance` and the
foundation unit/integration tests are executable today. Every other row is a required
acceptance specification for its owning phase. They are not registered as passing or
skipped tests. A sketch's helper names describe its fixture setup; they do not add a
new language keyword. Pending choices link to decision records before executable
expectations are fixed.

Each future executable case records:

- Stable case ID and feature/subfeature coverage; authoritative design/decision link.
- Complete source files, manifest/profile, entry or inbound messages, adapter setup,
  deterministic clock/random/switch controls, and prerequisite services.
- Independent expected diagnostics (identity, severity, file/span, related source or
  counterexample path), or stdout/values/outcomes/transport actions, plus exit status.
- Failure/cancellation injection and allowed event orderings, child/resource closure,
  side-effect counts, and required absence of leakage/duplicate work.
- Both backend results and test evidence. Matching backend outputs alone do not pass.

The initial [JSON runner](../../tests/Weft.Tests/ConformanceTests.cs) requires either
exact source-located compile diagnostics or explicit runtime stdout/stderr/exit-code
expectations. Missing expectations fail. Additional scenario types extend this schema
with explicit assertions instead of substituting one backend as an oracle.

Future diagnostic labels in these maps describe stable semantic categories and primary
locations; numeric WF identities are assigned with the implementing pass. Every error
case requires an adjacent valid variant to distinguish the intended rule from an
unrelated parser or missing-symbol failure. Adapter tests run against both real host
stacks as well as controlled drivers.
