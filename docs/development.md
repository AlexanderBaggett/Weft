# Building and working on Weft

Weft's first release still includes the full language on .NET and JVM. The current
compiler includes the Phase 1 foundation and the first Phase 2 function work: ordinary
functions, static helper classes, overloads, named/optional arguments, loops with
break/continue, conditional expressions, update/compound-assignment operators, signed integers, booleans, strings, and two working emitters. Cross-cutting declarations have
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
resolve first, and local variables shadow callable or namespace names. Calling a local
integer is a type error; a not-yet-supported member call is diagnosed rather than
silently resolving an unrelated namespace function. An already-canceled tool invocation
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
[control-flow contract](contracts/control-flow.md).
