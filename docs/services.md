# Services

A service is a component with a **lifetime**, **dependencies**, a **contract**, and a
**boundary**. Dependency injection is built in; there is no container to configure and
no constructor plumbing.

## Anatomy

```csharp
service UserService : scoped                 // lifetime
    requires Db, Cache                       // dependencies, resolved by the runtime
    exposes  IUserLookup                     // contract other code binds to
    config   UserOptions                     // bound from config, validated at startup
    mode     reentrant                       // concurrency mode (default)
{
    User Find(Id<User> id) => Cache.GetOr(id, () => Db.Query(...));

    health   => Db.Ping();                   // optional health probe
}
```

## Lifetimes

| Lifetime | Instances | Typical use |
|---|---|---|
| `singleton` | one per process | caches, clients, config-derived state |
| `scoped` | one per message (request, queue delivery, timer fire) | units of work, per-request context |
| `transient` | one per resolution | stateless helpers, cheap objects |

## Lifetime checking

This is where services earn language status. The compiler enforces:

1. **No captive dependencies.** A `singleton` may not `require` a `scoped` or `transient`
   service, and may not capture one in a field, closure, or static. Error.
2. **No escaping scopes.** A `scoped` instance may not be stored anywhere that outlives
   the message — statics, singleton fields, channels without `scoped` payload types,
   background tasks not tied to the message. Error.
3. **Message objects don't escape.** `msg` and receiver parameters carry the message's
   scope; storing them in a singleton is an error for the same reason.

These are the bugs that ASP.NET DI catches at runtime, on the first request, if you're
lucky. Weft catches them at compile time, always.

## Dependencies

`requires` lists services (and ambients — same keyword, see [ambients.md](ambients.md)).
Resolution is by type; `exposes` makes a service the implementation for a contract:

```csharp
service SqlUserStore : singleton exposes IUserStore { ... }
service TestUserStore : singleton exposes IUserStore { ... }

// weft.toml
[bind]
IUserStore = "SqlUserStore"
[bind.test]
IUserStore = "TestUserStore"
```

A contract with no bound implementation in the active profile is an error. Two
implementations bound in one profile is an error.

## Contracts

`exposes I` means: `I`'s members are the service's public boundary. Members not in any
exposed contract are internal to the service. A service with no `exposes` exposes all
its public members.

## The boundary

Every exposed method is:

- A **provenance stamp**: return values are `from ThisService` (or `via ThisService` if
  already tagged).
- A **trigger point**: `target boundary service` fires here.
- An **ambient checkpoint**: ambients the service `requires` are verified available at
  every call site.

## Config

`config T` binds `T` from the configuration sources listed in `weft.toml`, validates it
at startup (all `validate` rules targeting `T from Config` run), and exposes it as
`Config` inside the service. Values from config are `from Config`; secrets in config are
`Secret<T> from Config` and are subject to whatever rules target that.

## Concurrency mode

| Mode | Meaning |
|---|---|
| `reentrant` | default; caller is responsible for shared state |
| `serialized` | calls are queued; one at a time |
| `partitioned(key)` | serialized per key, parallel across keys |

## Health

`health => expr` declares a probe. The runtime aggregates probes; `Signal.Start` waits
for all `singleton` probes to pass before the pipeline opens.
