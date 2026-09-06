# Middleware

Middleware in Weft is a **routing graph**, not a linear pipeline. Each node inspects the
message in flight, may transform it, may terminate, and chooses the next node — like an
automated phone line where every menu decides where you go next. The compiler
enumerates every path through the graph and checks properties over all of them.

## Anatomy

```csharp
middleware Auth
    from Http                                  // origin filter — omit for all origins
    provides Principal                         // what this node guarantees downstream
    requires Correlation                       // what must already be guaranteed
{
    scope   receiver Orders, Admin.*;          // where — omit for global
    filter  !msg.Path starts "/public";        // when false, node is transparent
    effect  msg.Principal = Verify(msg.Headers["Authorization"]) or reject Unauthorized;
    after   Metrics.Count("auth", msg.Principal.Id);   // runs on unwind
    next    RateLimit;
}
```

### `from`

Restricts the node to messages from the listed origins **and** refines `msg` to that
origin's envelope. Under `from Queue`, `msg.Attempt` and `msg.Key` exist; under
`from Http`, `msg.Path` and `msg.Cookies` do. Without `from`, `msg` is the common
envelope: `Origin`, `Address`, `Headers` (empty for origins without them), `Body`, and
any ambients provided so far.

### `scope`

Which receivers or bindings this node applies to. A node whose scope excludes the current
message is transparent — as if `filter` were false.

### `filter`

Boolean over `msg`. False means the node's `effect` and `after` are skipped but `next`
is still followed. Use `next` branching instead when a false filter should route elsewhere.

### `guard`

Validation with an explicit verdict. Runs after `filter`, before `effect`.

```csharp
guard
{
    if (msg.Files.Count < 3) reject Invalid("Bulk upload needs at least 3 files", field: "files");
    approve;
}

guard match
{
    Tenants.IsSuspended(msg.Tenant) => reject Forbidden("Tenant suspended");
    else                            => approve;
}
```

- Every path must end in `approve` or `reject`; the compiler checks exhaustiveness.
- Guards may read `msg` and services; they may not mutate `msg`.
- `reject Outcome` · `reject Outcome("message")` · `reject Outcome(Problem { ... })`.
- A rejection here unwinds through the nodes *before* this one; this node's `after` does
  not run.

### `effect`

Statements. May read and write `msg`, assign ambients, and end the message with
`respond` or `reject`. Rejecting inside `effect` is for failures discovered while doing
work; pure validation belongs in `guard`.

### `after`

Runs when the response unwinds back through this node, in reverse order of the path
actually taken. Has access to `msg` and `msg.Response`. This is how the graph still
nests for cross-cutting concerns (timing, response headers, compression) without being
linear.

`after` runs during response preparation, before the adapter commits the response.
An entered effect registers its after even if it later fails; a bypass or rejected
guard does not. Cleanup must handle absent successful responses. Streaming retains
its scope through actual completion/abort. The full failure, joining, and disposal
order is specified in [message lifecycle](contracts/message-lifecycle.md).

### `next`

Single, match, code, or terminal.

```
next    RateLimit;                             // single

next match                                     // pattern block
{
    msg.Path starts "/admin"  => AdminAuth;
    msg.Path starts "/api"    => Auth;
    msg.Origin == Timer       => Route;
    else                      => respond NotFound;
}

next                                           // code block — must `return` a node or terminal
{
    var shard = Sharding.For(msg.Tenant);
    return shard.IsLocal ? Route : Forward;
}

next    Route;                                 // Route is a built-in terminal: the receiver table
next    respond Ok;                            // terminal
next    reject Unauthorized;                   // terminal
```

Every branch must end in a node or a terminal. `else` is required when a match is not
exhaustive. In code form every `return` must name a node or terminal literally so the
compiler can still enumerate the graph.

## `provides` / `requires`

The graph's type system. A node that `provides X` makes ambient `X` available to every
node and receiver reachable *through* it. A node or receiver that `requires X` may only
be reached by paths that pass through a provider.

```csharp
middleware Auth  provides Principal { ... }
middleware Quota requires Principal { ... }

receiver Admin requires Principal { ... }
```

The compiler checks every path in the graph. A path from an entry to `Admin` that skips
`Auth` is a **compile error**, with the offending path printed. This is the single most
valuable property of the graph: *there is no route into a protected handler that forgets
auth*, on any transport.

## `pipeline`

Declares entry points per origin. An executable project explicitly selects one active
pipeline. Library projects may export reusable nodes and named pipelines; referencing
their assembly or jar never activates or merges middleware automatically.

```csharp
pipeline Main entry match
{
    Http    => Timing;
    Queue   => Dedupe;
    Stream  => Dedupe;
    Timer   => Route;
    Channel => Route;
    Signal  => Route;
}
```

An origin with receivers but no pipeline entry is an error.

### Share middleware between projects

```csharp
use middleware Shared.Logging as Logging;
use middleware Shared.Auth as Auth;

pipeline Main entry match
{
    Http => Logging -> Auth -> OrderAudit -> Route;
}
```

Reusable nodes use `next continue;` for the connection the consuming project supplies.
Arrows connect those exits explicitly; they do not override a node's existing internal
routing. `OrderAudit` can be local to the API project.

Even an application with no middleware of its own explicitly adopts a shared pipeline:

```csharp
use pipeline Shared.Default as Common;
pipeline Main = Common;
```

See [project pipeline contracts](contracts/project-pipelines.md) for exported fragments,
origin selection, whole-pipeline adoption, and checks across DLL/jar boundaries. These
are design syntax and Phase 3/6 implementation work.

## Static graph, dynamic routing

The graph is static; the routing isn't. `next match` on `msg.Path` or on
`Random.Next(100) < 5` is a runtime decision. What is fixed at compile time is the **set
of nodes and edges** — every place a message *could* go — not which one it goes to.
Non-deterministic choice is fine; a non-enumerable node set is what breaks things.

What static buys:

- Path proofs — `requires Principal` holds on every path. If edges could appear at
  runtime the compiler couldn't enumerate paths, and this is the feature that justifies
  middleware being syntax instead of a library.
- Outcome exhaustiveness, envelope refinement (`msg.Attempt` exists because every path
  here came from Queue), cycle and termination checks, dead-node warnings.
- Nodes compile to direct calls. No dispatch table, no per-hop allocation.
- Tooling can draw it.

### Late-bound destinations

Selection can come from anywhere as long as the candidates are enumerable.

```csharp
// The lookup returns a key; code maps keys to nodes. `else` is required.
next match (Routing.Lookup(Tenant))
{
    "premium"  => PremiumPipeline;
    "standard" => Route;
    else       => reject Internal("unknown route");
}

// The lookup returns a node reference; the codomain is declared. Rows are validated at load.
table TenantRoutes : Tenant -> { PremiumPipeline, Route, reject Forbidden }
    source Db refresh 30s;
next TenantRoutes[Tenant] else reject Internal;

// The lookup returns a node reference; the codomain is derived.
next any Routing.Lookup(Tenant) else reject Internal;
```

For `next any`, the compiler inverts the proof: given what is provided at this point,
which nodes are *admissible* — `requires` satisfied, outcomes mappable, no cycle. That set
is emitted with the binary and the lookup result is checked against it (once per distinct
value). A data row can't route into a handler that requires `Principal` without it; you
just find out when the row is read rather than when the code is compiled.

Feature flags, canaries, and kill switches on middleware are covered in
[switches.md](switches.md); they change which nodes are *active*, never which nodes
*exist*, so the proofs survive them.

## Terminals

| Terminal | Meaning |
|---|---|
| `Route` | Dispatch to the receiver bound to `msg.Address`. Built-in. |
| `respond [Outcome] [body]` | End the message successfully; origin maps the outcome. |
| `reject Outcome [Problem]` | End the message with a failure outcome; origin maps it. |

`respond` and `reject` inside an `effect` also terminate, and unwind from that node.

## Path refinement

Nodes can narrow the type of `msg.Body`:

```csharp
middleware ParseJson<T> from Http
    provides Body<T>
{
    effect  msg.Body = Json.Parse<T>(msg.RawBody) or reject Invalid;
    next    Route;
}
```

Downstream of `ParseJson<LoginRequest>`, `msg.Body : LoginRequest from Http`. This is
usually implicit — `Route` performs the binding for the receiver's parameter type — but
explicit parse nodes let you validate earlier.

## Checks the compiler performs

- Every `requires` is satisfied on every path. Error.
- No cycles, unless a node is marked `reentrant(max: N)`. Error.
- Every path terminates. Error.
- Unreachable nodes. Warning.
- Every `reject`/`respond` outcome on a path is mapped by every origin that path serves. Error.
- `msg` member access is valid for the origins that can reach the node. Error.

## Worked graph

```
  Http  ──► Timing ──► Auth ──► RateLimit ──► Route
                        ▲
  Queue ──► Dedupe ─────┘        Queue path must also pass Auth if any receiver
                                 it can reach requires Principal — the compiler
  Timer ──────────────────────► Route          will tell you which path forgot it
```

## Relationship to rules and triggers

- Rules fire on values inside middleware effects (`msg.Body` is provenance-tagged).
- `boundary receiver` triggers fire between `Route` and the handler.
- Middleware is the only construct that can change *where* a message goes.
