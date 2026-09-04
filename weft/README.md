# Weft

> **Working name.** Weft is the thread that runs *across* the warp. Rename freely.

A web-tier language where cross-cutting concerns are first-class and compiler-checked.
Fast, garbage-collected, C#-adjacent syntax. Not a systems language.

**Status:** design sketch. No compiler yet. See [docs/open-questions.md](docs/open-questions.md).

## The idea

Most of what makes backends tedious isn't business logic. It's everything that has to
happen *around* business logic: sanitize input, log at boundaries, auth on every path,
ack messages, propagate trace ids, don't leak secrets. Today that lives in attributes,
base classes, middleware ordering, and discipline.

Weft makes it declarative and verifiable:

- Values carry **provenance** — where they came from (`string from Http`).
- **Rules** fire when a value with a given provenance reaches a **sink** (`Log`, `Db.Write`).
- **Triggers** fire at method boundaries.
- **Middleware** is a routing graph, not a list. The compiler proves properties over every path.
- **Receivers** replace controllers and bind to any **origin** — HTTP, queues, streams, timers, sockets — with the same machinery.
- **Services** have lifetimes the compiler enforces.
- **Caches** declare their invalidation, and the compiler finds the writes you forgot.
- **Switches** — feature flags, canaries, kill switches — gate any of the above, and every state is verified.

All of it shares one four-part shape: **scope · target · filter · effect**.

## Taste

```csharp
model LoginRequest from Http { string User; string Pass; }

sink      Log(string msg);
transform Sanitize(string from Http) -> string;

rule SanitizeLoggedInput
{
    target  Request from Http;
    filter  string;
    effect  before Log => Sanitize;
}

middleware Auth from Http provides Principal
{
    scope   receiver Accounts, Admin.*;
    effect  msg.Principal = Verify(msg.Headers["Authorization"]) or reject Unauthorized;
    next    Route;
}

receiver Accounts : scoped from Http, Queue requires UserService
{
    on Http.Post("/login"), Queue(accounts.login)
    Response<Session> Login(LoginRequest req)
    {
        Log(req.User);                 // compiler emits Log(Sanitize(req.User))
        return UserService.Login(req);
    }
}
```

## What earns a language feature

A construct is language-level only if a class or method could not express it — meaning
the compiler has to *know* about it to check or rewrite programs. Provenance, rules,
triggers, middleware graphs, origins, receivers, service lifetimes, ambients, caches, and
switches qualify.
`Id<T>`, `Response<T>`, `Page<T>` and friends do not; they're standard library.

## Documentation

| Doc | What it covers |
|---|---|
| [overview](docs/overview.md) | **Start here.** The whole design on one page — one sketch per construct |
| [provenance](docs/provenance.md) | Origins as type tags, propagation, discharge |
| [rules](docs/rules.md) | Value-targeted rules: sinks, transforms, effects, precedence |
| [triggers](docs/triggers.md) | Point-targeted rules: method and service boundaries |
| [middleware](docs/middleware.md) | The routing graph, `provides`/`requires`, path proofs |
| [origins](docs/origins.md) | Built-in inbound and source-only origins, typed addresses |
| [receivers](docs/receivers.md) | Transport-agnostic handlers |
| [services](docs/services.md) | Lifetimes, dependencies, contracts, lifetime checking |
| [ambients](docs/ambients.md) | Implicitly-threaded request context |
| [caching](docs/caching.md) | Memoization with compiler-checked invalidation |
| [switches](docs/switches.md) | Feature flags, canaries, kill switches — on any construct |
| [effect-scopes](docs/effect-scopes.md) | `transaction`, `deadline`, `retry` (tentative) |
| [grammar](docs/grammar.md) | EBNF sketch |
| [open-questions](docs/open-questions.md) | Decisions not yet made |

Worked examples: [examples/orders.weft](examples/orders.weft),
[examples/input.rules](examples/input.rules), [examples/switches.weft](examples/switches.weft).
