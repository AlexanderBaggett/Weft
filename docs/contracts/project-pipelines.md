# Sharing middleware between projects

**Designer decision, 2026-09-06:** use “project” for a separately built unit. One project
may exist only to supply middleware; another may implement an API and have middleware
of its own. Combining them across .NET assemblies or JVM jars must be explicit and
controlled by the developer. Even an application with no local middleware needs a
source file that explicitly adopts the shared pipeline.

The behavior and syntax below are accepted by the designer (2026-09-06, “Syntax looks
good, proceed”). They can evolve through subsequent design decisions. Cross-project
execution is Phase 3/6 work.

The [Shared](../../examples/project-pipelines/Shared/middleware.weft),
[Orders](../../examples/project-pipelines/Orders/pipeline.weft), and
[SharedOnly](../../examples/project-pipelines/SharedOnly/pipeline.weft) design examples
exercise these syntax forms. They are parsed as foundation declarations and diagnosed
as requiring the later graph passes; they are not executable sample applications yet.

## Reference, select, connect

A project reference makes public declarations available. It does not activate nodes,
select a pipeline, change an existing connection, or search assemblies for middleware.
Only the consuming application's pipeline file makes those choices.

For example, the Orders project's future manifest reference is:

```toml
[references]
Shared = "../Shared/weft.toml"
```

Published references resolve a package containing the selected backend's assembly/jar
and Weft contract metadata. Source groups in one project are a different concept:
`[source-groups]` only discovers local files. The Phase 1 loader supports source groups
and reports project references as not yet supported, instead of flattening dependency
source into the application.

Select and connect individual nodes in `pipeline.weft`:

```csharp
use middleware Shared.Logging as Logging;
use middleware Shared.Auth as Auth;

pipeline Main entry match
{
    Http => Logging -> Auth -> OrderAudit -> Route;
}
```

`OrderAudit` belongs to Orders. Each `use middleware` creates a named node use in the
consuming pipeline. Another alias permits another placement of the same definition
with different connections; it does not change the lifetime of services used by it.
Names are checked for ambiguity and accessibility. Arrows explicitly set the order.

## Reusable nodes have an explicit connection point

```csharp
public middleware Logging
{
    effect Log.Request(msg.Address);
    after  Log.Response(msg.Response);
    next   continue;
}
```

Here `continue` means the next connection supplied by the consuming pipeline. It is
only a middleware routing target, distinct from the ordinary loop statement. In
`Logging -> Auth -> Route`, Logging's continuation is Auth, and Auth's is Route.
Conditional paths can select `continue`, another declared node, or a terminal. A
rejection or response still terminates that path and unwinds entered nodes normally.

Existing concrete connections such as `next Auth;` retain their meaning. Appending
`-> Other` to a node or fragment with no continuation is an error; the compiler never
silently replaces its internal connections. A developer who wants a different internal
layout can select the project's exported nodes and connect those explicitly.

## Reuse a pipeline or insert local middleware after it

A library project can export a fragment whose normal exit is open:

```csharp
public pipeline Web entry match
{
    Http => Logging -> Auth -> continue;
}
```

The consuming file chooses the origin entry and connects the fragment's continuation:

```csharp
use pipeline Shared.Web as Common;

pipeline Main entry match
{
    Http => Common.Http -> OrderAudit -> Route;
}
```

`Common.Http` is that exported pipeline's HTTP entry. All reachable internal nodes and
branches come with the selected entry; other origins are not activated by this use.
All `continue` exits of that fragment instance connect to `OrderAudit`. An unconnected
continuation is an error in an executable application. It is permitted in an exported
library fragment whose metadata declares the open connection and its requirements.

A library can instead export a complete pipeline, ending in `Route` or another
terminal on every path. Even when Orders has no local middleware, its file says:

```csharp
use pipeline Shared.Default as Common;
pipeline Main = Common;
```

This explicitly adopts all of Common's origin entries and reachable middleware.
`Route` dispatches through the consuming application's receiver table. Unreachable
exports are not automatically added. Multiple imported pipelines never merge their
entries automatically; use an entry block to select the desired entry for each origin.
The application declares one pipeline as its active pipeline. Library projects may
export several named alternatives without running any of them.

## Contracts across assemblies and jars

Libraries ship enough Weft metadata to check the resulting application without having
their source: public identities and versions, origin restrictions, reachable routes,
continuations, provides/requires, body refinements, policy/effect summaries, outcomes,
and lifetime/cleanup obligations. The metadata includes its schema/runtime ABI version
and identifies the matching binary. Missing, incompatible, or mismatched metadata is
a build error; loading a DLL or jar alone cannot supply an unverified pipeline.

The consuming compiler resolves connections and checks the entire reachable graph,
including shared internals: missing providers, origins, unmapped outcomes, cycles,
unbound exits, and forbidden flows still produce application-level diagnostics with
the contributing library declaration identified. Internal library helpers can remain
private while their required summaries are available for analysis.

Backends call the library's compiled guard/effect/after/routing entry points through
generated adapters. The application owns connection and unwind orchestration; library
code cannot discover or attach arbitrary nodes at runtime. This fits both DLL and jar
references without requiring .NET or Java middleware-framework discovery conventions.

P03-001 owns graph binding and connection checks; P06-001/003 own project references,
binary metadata, and packaging. W009-A10 and W032-A03 cover explicit adoption, mixed
local/shared nodes, multiple library alternatives, absence of implicit activation,
and the same rejected connections on both targets.
