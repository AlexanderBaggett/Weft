# Overview — the design on one page

Web-tier language. Fast, GC'd, C#-adjacent syntax. Not a systems language.

**Thesis:** cross-cutting concerns should be declarative and compiler-checked, not
attributes + base classes + middleware ordering + discipline.

**Test for a language feature:** could a class or method express it? If yes, it's
stdlib. Only things the compiler must *know about* to check or rewrite programs get syntax.

---

## The four-part shape

Everything cross-cutting has: **scope · target · filter · effect**.

- `scope` — where it applies. Omitted = project. `module X`, `namespace X`, `type X`,
  `method X.Y`, `receiver X`, `receiver X.*`, `flag X`, `!flag X`.
- `target` — what it attaches to. This is what differs between constructs.
- `filter` — narrows a match. Optional.
- `effect` — what happens.

| construct | target kind | extras |
|---|---|---|
| `rule` | a value (type + provenance) | — |
| `trigger` | a point (method/service boundary) | — |
| `middleware` | the message in flight | `from`, `provides`, `requires`, `next`, `after` |

---

## Block convention

- `{ ... }` is **always a code block**.
- `match { ... }` is a **pattern block**: `pattern => result;` arms plus optional `else`.
  In declaration positions (`next`, `entry`, `outcomes`) the scrutinee is implicit.
  In code, `match (expr) { ... }` is an expression — C#'s `switch` expression, shorter name.
- Anywhere that accepts `match { }` also accepts a code block that `return`s (or, in a
  `guard`, `approve`s/`reject`s) the same kind of thing. Use `match` for the common case; drop to code when you need a hundred
  lines of logic to decide.

---

## Provenance — `from`

Values carry where they came from, as part of the type. Erased at runtime.

```csharp
string from Http
string from Http | Queue
string from Http via UserService        // crossed a service boundary
```

- Anything that produces values is an origin: `origin Http;`, every `service`, every `model`.
- Propagates through assignment, concat, interpolation, fields, containers.
- Only a `transform` discharges it: `transform Sanitize(string from Http) -> string;`
- Source-only origins (stamp, never receive): `Db, Cache, Config, Env, Clock, Random`.

Open: does `.Length` carry it? Are `List<string from Http>` and `List<string>` different types?

---

## Rules — value-targeted

Select values (type + provenance + shape), attach effects at **points** on their lineage.

```csharp
transform Sanitize(string from Http) -> string;   // the only thing that discharges provenance
validate  MaxLen(string s, int n);
sink      Egress = Log, Http.Send, Queue.Send;    // a named set of points

rule IncomingStrings
{
    target  Request from Http in { LoginRequest, SignupRequest };   // source filter lives here
    filter  string;                          // 0..n lines, AND'd
    filter  field.Name ends "Note" || { Length: > 2000 };
    effect  => Trim;                         // at bind (default): eager
    effect  at Log => Sanitize;              // lazy: only if logged; idempotent once discharged
    effect  at Egress => forbid;             // compile-time
    effect  at cross into Db.* => (v) { Audit.Record(point.Site); return v; };
}
```

Points: `bind` · `call Fn` / glob / bare name · `after Fn` · `cross into|out of X` · named
set. Site predicate on the point: `at Log where point.Caller in Api.*`.

Actions: `=> Fn` (replace if same type, observe if void; `_` = value) ·
`=> require Check else reject Outcome` · `=> forbid` · `=> (v) { ... }` (return replaces).

Filters: type (`string | string[]`, `not Id<*>`, `[Pii]`) · property pattern
(`{ Items: { Count: >= 3 } }`) · predicate (`string where Length > 50`) · field
(`field.Name ends "Password"`, `field has [Secret]`) · collection (`any`/`all`/`each of`) ·
lineage (`lineage crossed X`) · context (`when Tenant.Tier == Free`). Named:
`filter Sensitive = ...;`. Type/field/lineage resolve at compile time; patterns,
predicates, `when` emit a branch. `static rule` forbids runtime residue.

Precedence: narrower scope > narrower target > `priority N`; `forbid` always wins.
Grouping: `ruleset`, `use`, `suppress X in scope`. Rules live in `.rules` files.

---

## Triggers — point-targeted

```csharp
trigger LogCrossings
{
    scope   namespace Api.Orders;
    target  boundary method internal;       // both caller and callee in scope
    filter  args where !(arg is Secret);
    effect  on enter => Log($"{point.Name}({point.Args})");
            on throw => Log(point.Exception);
}
```

Boundaries: `method | service | receiver | transform`, modifiers `internal | external`.
`point` has `Name, Args, Result, Exception, Caller, Callee, Elapsed`. Triggers observe;
they don't modify. `point.Args` keeps provenance, so rules fire inside triggers.

---

## Middleware — a routing graph, not a pipeline

Each node decides where the message goes next (bank phone-line model).

```csharp
middleware Auth
    from Http                               // origin filter; refines `msg` to Http envelope
    provides Principal                      // guarantee to everything downstream
{
    scope   receiver Orders, Admin.*;
    filter  !msg.Path starts "/public";     // false = transparent
    effect  msg.Principal = Verify(msg.Headers["Authorization"]) or reject Unauthorized;
    after   Metrics.Count("auth");          // runs on unwind, reverse path order
    next    RateLimit;
}

middleware Dispatch
{
    next match
    {
        msg.Path starts "/admin" => AdminAuth;
        msg.Origin == Timer      => Route;  // built-in terminal: the receiver table
        else                     => reject NotFound;
    }
}

middleware ShardRouter                      // same thing, code form
{
    next
    {
        if (Tenants.IsSuspended(msg.Tenant)) return reject Forbidden;
        var shard = Sharding.For(msg.Tenant);
        Log($"routing {msg.Address} to {shard}");
        return shard.IsLocal ? Route : Forward;
    }
}

pipeline Main entry match { Http => Trace; Queue => Dedupe; Timer => Route; }
```

Code-form `next` keeps the path proofs: every `return` must name a node or terminal
literally (not a variable holding one), so the compiler can still enumerate destinations.
Non-literal destination = compile error.

**Static graph, dynamic routing.** The *set* of nodes and edges is fixed at compile time;
which edge a message takes is runtime. Late-bound destinations are fine as long as
candidates are enumerable: `next match (Lookup(x)) { "a" => A; else => reject Internal; }`,
a `table T : Tenant -> { A, B, reject Forbidden } source Db;` with `next T[Tenant] else ...`,
or `next any Lookup(x) else ...` where the compiler derives the admissible set and the
runtime checks membership.

### `guard` — validate, then approve or reject

```csharp
middleware BulkLimits from Http
{
    scope   receiver Uploads.Bulk;
    guard
    {
        if (msg.Files.Count < 3)  reject Invalid("Bulk upload needs at least 3 files", field: "files");
        if (msg.Files.TotalSize > 50mb) reject Invalid("Total size exceeds 50 MB");
        approve;
    }
    next    Route;
}

middleware TenantActive
{
    guard match
    {
        Tenants.IsSuspended(msg.Tenant) => reject Forbidden("Tenant suspended");
        else                            => approve;
    }
    next    Route;
}
```

Node order: `filter` (does this node apply?) → `guard` (may this message continue?) →
`effect` (mutate, provide) → `next`.

- Every path through a guard must end in `approve` or `reject` — compile error otherwise.
- Guards read `msg` and services but can't mutate `msg`; that's what `effect` is for.
- `reject Outcome` · `reject Outcome("message")` · `reject Outcome(Problem { ... })`. The
  origin maps the outcome; the Problem rides along (7807 body on Http, reason header on Queue).
- A guard rejection unwinds through the nodes *before* this one (their `after` runs), not this one.
- `effect` can still `reject` (`... or reject Unauthorized`) for failures found while doing
  work. Pure validation belongs in `guard`.

**The magic:** `provides` / `requires` checked over *every path*. A receiver that
`requires Principal` reachable by a path that skips `Auth` is a compile error — on any
transport. Also: no cycles, every path terminates, unreachable nodes warned, every
outcome mapped by every origin the path serves.

---

## Origins — built in, not stdlib

Each contributes: address grammar (validated at compile time), envelope (`msg` shape
under `from X`), outcome map, ambients.

Inbound: `Http, Ws, Grpc, Queue, Stream, Channel, Timer, Tcp/Udp, Signal, Watch`
Source-only: `Db, Cache, Config, Env, Clock, Random`
Custom: `origin Mqtt : adapter X { address ...; envelope {...}; outcomes match {...}; }`

`Thread` folds into `Channel` — thread handoff *is* a channel; naming it that makes
backpressure explicit.

**Outcomes are symbolic**, mapped per origin with `outcomes match { Invalid => DeadLetter; * => Nack; }`
(or a code block): `reject Unauthorized` → 401 on Http, DeadLetter on Queue. `Ok, Created, Invalid, Unauthorized, Forbidden, NotFound, Conflict,
TooMany, Internal, Timeout…`

**Typed addresses** so both ends agree:
```csharp
topic   orders.create : CreateOrder  on Queue;
channel orderEvents   : OrderEvent   (capacity: 1024, full: Backpressure);
Queue.Send(orders.create, "oops");   // compile error
```

---

## Receivers — the unified controller

```csharp
receiver Orders : scoped
    from Http, Queue, Timer
    requires OrderService
{
    on Http.Post("/orders"), Queue(orders.create) requires Principal
    Response<Order> Create(CreateOrder cmd) => OrderService.Create(cmd);
        // cmd is `from Http` on one path, `from Queue` on the other — rules specialize

    on Timer(every: 5m)
    void Sweep() => OrderService.ExpireStale();
}
```

One method, many transports. Compiler emits one stub per (binding, origin). Models used
as receiver params are implicitly tagged `Request`. Name alternatives: `port`, `inlet`.

Bindings take a `guard` too — first statement of the body, runs after parameter binding,
before anything else:

```csharp
on Http.Post("/bulk") requires Principal
Response<Receipt> Bulk(File[] files)
{
    guard
    {
        if (files.Count < 3) reject Invalid("Bulk upload needs at least 3 files", field: "files");
        if (!Principal.Can(Upload)) reject Forbidden;
        approve;
    }
    return Created(Store.Ingest(files));
}
```

Same rules as middleware guards. Declarative validation (`rule ... effect => require X else reject Invalid`)
and imperative guards produce the same outcome + Problem; use rules for the cross-cutting
case, guards for the one-off.

---

## Services

```csharp
service UserService : scoped                 // singleton | scoped | transient
    requires Db, Cache
    exposes  IUserLookup
    config   UserOptions
{ ... }
```

Also: `mode reentrant | serialized | partitioned(key)`, `health => expr`.

**The magic:** lifetime checking. Singleton capturing scoped = compile error. Scoped or
`msg` escaping the message (statics, singleton fields, detached tasks) = compile error.
Every exposed method is a provenance stamp and a trigger point.

---

## Ambients

Context that travels with the message without being a parameter. `Principal`,
`Correlation`, `Deadline`; origins add `Connection`, `Partition`, `Attempt`.

- Middleware `provides` them; receivers/services `requires` them.
- Compiler proves availability at every use — through the middleware graph *and* the call graph.
- `Deadline` is a budget; all known I/O honors it.
- Same propagation machinery as provenance.

---

## Switches — flags, canaries, kills

One substrate: declared, typed, runtime-valued, **snapshotted per message**, usable as
`scope`, and **every state verified** by the compiler. They gate any construct —
middleware, rules, triggers, receivers, services, caches, origins.

| | default | direction | changes by | must declare |
|---|---|---|---|---|
| `flag` | off | enables | operator | `retire` date |
| `canary` | baseline | shifts traffic | judge + schedule | matching contracts |
| `kill` | on | disables | operator or `trip` | `killed =>` degradation |

```csharp
flag StrictInput : bool default off source Db by Tenant retire 2026-12-01;

middleware RateLimitV2 from Http requires Principal
{ scope flag NewRateLimit; ... }                       // off → transparent; both states proved

rule StrictSanitize
{ scope flag StrictInput; target Request from Http; filter string; effect at Log => SanitizeStrict; }

trigger DebugCrossings
{ scope flag VerboseTrace; target boundary method internal; effect on enter => Log(...); }

on Http.Post("/checkout") when NewCheckout  Response<Receipt> V2(Cart c) => ...;
on Http.Post("/checkout") when !NewCheckout Response<Receipt> V1(Cart c) => ...;   // must partition
```

```csharp
canary Sanitizer
{
    scope     rule SanitizeLoggedInput;
    baseline  Sanitize;  candidate SanitizeStrict;    // same signature, or error
    mode      shadow;                                 // run both, use baseline, diff; no `external` calls
    select    100%;
    judge     over 1h: diff_rate < 0.01%, error_rate <= baseline;
    promote   manual;
}
// also: scope middleware X (two nodes, same contract) · scope trigger X (two effects)
//       scope service IFoo (two impls) · select N% by Tenant sticky · promote auto steps 5% 25% 100%
```

```csharp
kill Auth { scope middleware Auth; }                  // COMPILE ERROR: Admin requires Principal
kill Auth { scope middleware Auth; killed => reject Unavailable("Auth down"); }   // fail closed: ok

kill NoteValidation { scope rule BoundedNotes; trip when reject_rate > 30% over 5m; reset after 15m, probe 1%; }
kill DebugCrossings { scope trigger DebugCrossings; trip when overhead_p99 > 2ms over 1m; reset after 10m; }
kill Pricing        { scope service IPricing; trip when error_rate > 50% over 1m; reset after 5m; killed => reject Unavailable; }
kill QueueIntake    { scope origin Queue; }           // stop consuming; messages stay queued
```

- Judge/trip metrics come free from boundary triggers: `error_rate, reject_rate, p99,
  overhead_p99, diff_rate`. `trip`/`reset` = circuit breaker.
- **`forbid` can't be switched** — it's a compile-time proof.
- `provides` under a switch is re-proved per state. `by X` needs ambient `X` at every read.
- Proofs run per switch with others at default; `switch group { ... }` proves jointly (bounded).

---

## Caching — with invalidation the compiler checks

Cache decisions live at a read boundary; correctness depends on every write anywhere.
The compiler can see both.

```csharp
cache OrderById
{
    scope   project;
    target  boundary service OrderService.Find;      // what gets memoized
    key     args[0];                                  // Id<Order>; Tenant added automatically if required
    ttl     5m;
    effect  invalidate on Db.Write(Order), Db.Delete(Order), Stream(orders) by .Id;
}
```

- **Stale-cache proof:** every write site in the project for `Order` must appear in
  `invalidate on`, or a warning names the write site.
- **Key adequacy:** if the target `requires Tenant`, `Tenant` must be in the key.
  Cross-tenant cache leaks become a compile error.
- **Provenance survives:** a cached `Order` is still `from OrderService`.
- **Honest gap:** out-of-process writes the compiler can't see. Typed topics/streams cover
  the visible ones; the rest is documented, not silent.

Also applies at `Http.Send` for response caching (ETag derived from key + version).

---

## Effect scopes (tentative)

```csharp
transaction { Db.Write(o); Email.Send(r); }   // error: `external` call inside transaction
deadline 200ms { ... }
retry(3) { Inventory.Reserve(x); }            // error unless Reserve is `idempotent`
saga { step A compensate A'; step B; }        // error: B has no compensation
```

Call classifications: `idempotent`, `external`, `pure`. Declared or inferred? Undecided.

---

## Stdlib, not language

`Id<T>`, `Secret<T>`, `Response<T>`, `Problem`, `Page<T>`, `Message<T>`, `Instant`,
`Duration`, refinement types (`type Email = string where IsEmail`). `Secret<T>` ships
with `at Egress => forbid` and `at Persist => forbid` rules attached.

---

## What the compiler does with all this

Weft's cross-cutting constructs are **compile-time**. Effects become inserted calls,
inserted checks, or errors. There is no runtime interception, no reflection, no proxy
objects. The costs are paid at build time; the generated code is what you would have
written by hand if you never got tired.

Consequences:

- Every rule that can never fire is a warning.
- Every path through the middleware graph is enumerated and checked.
- Provenance is erased after checking; `string from Http` is a `string` at runtime.

---

## Source layout

```
*.weft      — models, services, receivers, middleware, pipelines, ordinary code
*.rules     — rules and triggers; may set a file-level scope
weft.toml   — project manifest: modules, origin adapters, rulesets in use
```

Rules files are discovered by the project, not imported by source files. A rule's
`scope` decides where it applies; its file location does not.

---

## Vocabulary

- **Provenance** — the set of origins a value has passed through.
- **Point** — a place on a value's lineage where a rule effect fires: `bind`, a call, a boundary crossing.
- **Sink** — a named set of points, e.g. `Egress`.
- **Boundary** — a method/service crossing (enter/exit/throw) at which triggers fire.
- **Envelope** — the shape of `msg` for a given origin.
- **Outcome** — a symbolic result (`Unauthorized`, `Invalid`, …) each origin maps to a transport-specific action.
- **Address** — a location within an origin: a route, a topic, a channel name, a schedule.

---

## Open questions

Tracked in [open-questions.md](open-questions.md).
