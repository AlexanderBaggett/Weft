# Building and working on Weft

Weft's first release still includes the full language on .NET and JVM. The current
compiler includes the Phase 1 foundation and Phase 2 ordinary-language work: functions,
classes, constructors, object initializers, required members, fields, instance
properties/get/set/init accessors, instance/static methods,
overloads, named/optional
arguments, loops with break/continue, conditionals, update/compound-assignment
operators, signed integers, booleans, strings, and two working emitters. Cross-cutting declarations have
syntax representation and explicit not-yet-implemented diagnostics. They remain
required work in the [phase trackers](phases/README.md).

## Prerequisites and verification

Install .NET SDK 10 and a full JDK (javac, java, jar) version 21 or newer on PATH. Java
source targets release 21 without preview features. Development validation uses SDK
10.0.111 and both Temurin JDK 21.0.12.1 and OpenJDK 26.0.2 for the foundation;
the September 30 control-flow checkpoint uses SDK 10.0.112 and OpenJDK 27. `global.json` permits the
latest installed .NET 10 feature band. NuGet package versions are pinned in the project
files. Initial restore requires network access or a populated package cache.

```sh
bash eng/verify.sh
```

This local entrypoint restores/builds the solution, executes
the independent conformance expectations on both targets, and runs the example through
the CLI. Tests use isolated temporary directories, including paths with spaces, and
bound child-process execution. Results are written as TRX under the test project.
No installed Weft compiler or previously generated artifact is required.

Hosted CI is deferred to Phase 6 (P06-030), following the designer's 2026-09-06 direction.
There is no active GitHub Actions workflow. Keep tests focused on working language
behavior, backend agreement with the specification, and concrete regressions; do not
add workflow scaffolding or release gates to the early implementation phases.

## CLI

```sh
dotnet run --project src/Weft.Cli -- check --project examples/foundation
dotnet run --project src/Weft.Cli -- run --project examples/foundation --backend dotnet
dotnet run --project src/Weft.Cli -- run --project examples/foundation --backend jvm
dotnet run --project src/Weft.Cli -- build --project examples/foundation --backend jvm
dotnet run --project src/Weft.Cli -- emit --project examples/foundation --backend dotnet
```

`--project` accepts a directory or manifest path. `--output` overrides the default
`<project>/.weft/<backend>` directory. `--diagnostics json` writes one diagnostic JSON
object per stderr line, with code, severity, message, and Weft location. Success output
and program stdout use stdout. Exit codes: 0 success, 1 compilation/build/I/O failure,
2 command usage, 130 canceled CLI; `run` otherwise returns the program's exit code.

`check` resolves the configured entry and checks supported semantics without invoking
a host compiler. `emit` writes inspectable C#/Java and the source-map sidecar. `build`
also invokes Roslyn/javac and packages runnable output. `run` builds before execution.
The .NET artifact includes a runtime DLL/config and portable PDB. The JVM artifact is
an executable jar with its runtime included, plus a separately packaged runtime jar.
Cancellation terminates CLI-owned compiler/program subprocesses as a process tree.

The bootstrap supplies `Print` and `Log` as fallback output helpers. User functions
resolve first, and local variables and fields shadow callable or namespace names.
Calling an integer local or field is a type error. Unknown members are diagnosed
rather than silently resolving an unrelated namespace function. An already-canceled tool invocation
does not launch a subprocess.

The [function contract](contracts/functions.md) describes the current ordinary-call
behavior and remaining declaration/type work. Try the runnable example:

```sh
dotnet run --project src/Weft.Cli -- run --project examples/functions --backend dotnet
dotnet run --project src/Weft.Cli -- run --project examples/functions --backend jvm
```

Both runs print `price first`, `units second`, and `Total: 50`, in that order. The call
uses out-of-order named arguments, int-to-long widening, and an omitted optional
parameter. Static method visibility is checked by Weft before either host compiler.

The [class example](../examples/classes/Program.weft) exercises construction, readonly
fields, instance methods, alias mutation, and identity equality:

```sh
dotnet run --project src/Weft.Cli -- run --project examples/classes --backend dotnet
dotnet run --project src/Weft.Cli -- run --project examples/classes --backend jvm
```

Each run prints `50`, `50`, `true`, `false`, and `Ada`. See the
[object contract](contracts/objects.md) for initialization checks and remaining work.

The [property example](../examples/properties/Program.weft) adds getter-only and
restricted-setter auto-properties, a custom clamping setter, and a computed getter:

```sh
dotnet run --project src/Weft.Cli -- run --project examples/properties --backend dotnet
dotnet run --project src/Weft.Cli -- run --project examples/properties --backend jvm
```

Each run prints `A:5`, `-3`, `0`, `2`, and `A:3`. The assignment result is `-3` while
the setter stores zero. See the [property contract](contracts/properties.md).

The [initializer example](../examples/initializers/Program.weft) constructs required
init-only identity properties and a mutable quantity:

```sh
dotnet run --project src/Weft.Cli -- run --project examples/initializers --backend dotnet
dotnet run --project src/Weft.Cli -- run --project examples/initializers --backend jvm
```

Each run prints `Ada:London:3` and `Ada:London:4`. See the
[initialization contract](contracts/initialization.md) for required-member and non-null checks.

## Initial manifest

```toml
[project]
name = "sample"
entry = "App.Main"
sources = ["src/**/*.weft", "policies/**/*.rules"]

[build]
backend = "dotnet"

[source-groups]
Shared = ["shared/**/*.weft"]
```

`name` is required and contains letters/digits/underscore/hyphen, starting with a
letter. Entry defaults to `Main`; it is parameterless and returns void or int32.
Backend defaults to dotnet. Sources default to all `.weft` and `.rules` files beneath
the project. Patterns support `*`, `**`, and `?`; paths are project-relative. Discovery
sorts/deduplicates paths and excludes `.git`, `.weft`, bin, obj, artifacts, and symlinked
directories. A named source group contributes local file patterns; it is not a project
reference or assembly boundary. The earlier `[modules]` spelling was replaced with
`[source-groups]` following the designer's terminology decision. Full project references,
public exports, and service bindings remain later work. Unknown options and
unsupported manifest sections fail clearly rather than being ignored.

## Component boundaries

| Component | Responsibility |
|---|---|
| `src/Weft.Compiler` | Source/diagnostics, lexer/parser, symbols/types/binder, shared IR/validator, backend contracts |
| `src/Weft.Backend.DotNet` | C# source/Roslyn assembly emission and diagnostic mapping |
| `src/Weft.Backend.Jvm` | Java source/javac/jar execution and diagnostic mapping |
| `src/Weft.Cli` | TOML project loading, discovery, command dispatch |
| `runtime/dotnet/Weft.Runtime` | .NET portable runtime operations |
| `runtime/jvm/src` | JVM portable runtime operations |
| `tests/Weft.Tests`, `tests/conformance` | Unit/integration tests and backend-independent executable expectations |
| `docs/acceptance`, `docs/contracts`, `docs/decisions` | Full-release acceptance map, shared contracts, designer decisions |

See [IR/runtime contracts](contracts/ir-and-runtime.md) for extension requirements.
Add the expectation before implementing an operation; do not use one backend as the
other's oracle. Keep future acceptance records separate from executable passing cases.

Compiler diagnostics are grouped by stable family: WF1xxx lexical/syntax, WF2xxx
binding/types/unsupported passes, WF3001 invalid IR, WF4001–4003 runtime ABI/operation/
entry, WF41xx Roslyn, WF42xx JVM toolchain, WF50xx manifest/CLI I/O. Future features add
identities with their acceptance cases rather than reusing unrelated error codes.
Function binding adds WF2011 for inaccessible methods, WF2012 for invalid modifiers,
WF2013 for invalid defaults/parameter order, WF2014 for invalid named arguments, and
WF2015 for ambiguous overloads. Duplicate signatures remain WF2002. Control flow adds
WF2016 for jumps outside a loop, WF2017 for an unbraced declaration used as a branch/loop
body, and WF1104 for multiple variables in a `var` for-initializer. See the
[control-flow contract](contracts/control-flow.md). Objects add WF2019 for a public
contract exposing an internal class, WF2020 for invalid instance/static use, WF2021
for a readonly field write, and WF2022 for incomplete reference-field initialization.
See the [object contract](contracts/objects.md). Properties add WF2023 for a missing
getter/setter and WF2024 for invalid accessor declarations or accessibility. Initialization
adds WF2025 for init writes outside construction, WF2026 for missing required members,
WF2027 for invalid required declarations, and WF2028 for invalid member initializers.
