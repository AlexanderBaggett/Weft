# Weft

> **Working name.** Weft is the thread that runs *across* the warp. Rename freely.

A web-tier language where cross-cutting concerns are first-class and compiler-checked.
Fast, garbage-collected, C#-adjacent syntax. Not a systems language.

**Status:** Phase 1 compiler foundation complete; Phase 2 is in progress. Ordinary programs compile and run
on .NET and JVM; the cross-cutting language remains required implementation work.
See [development](docs/development.md) to build/run the foundation and
[open questions](docs/open-questions.md) for remaining design choices.

**Release direction:** the first release covers the full language design on both .NET
and JVM, including additions accepted during development. The user leads language
design; AI leads implementation and engineering. The [roadmap](docs/roadmap.md) evolves
with the design, and the [feature register](docs/feature-status.md) tracks completion.

## The idea

Most of what makes backends tedious isn't business logic. It's everything that has to
happen *around* business logic: sanitize input, log at boundaries, auth on every path,
ack messages, propagate trace ids, don't leak secrets. Today that lives in attributes,
base classes, middleware ordering, and discipline.

Weft makes it declarative and verifiable:

- Values carry **provenance** — where they came from (`string from Http`).
- **Rules** attach effects to values — at binding, at a call, or when a value crosses a boundary — and follow them through their lineage.
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

transform Sanitize(string from Http) -> string;

rule IncomingStrings
{
    target  Request from Http;
    filter  string;
    effect  => Trim;                  // at bind, eagerly
    effect  at Log => Sanitize;       // lazily, only if it is logged
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
        Log(req.User);                 // compiler emits Log(Sanitize(Trim(req.User)))
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
| [compiler](docs/compiler.md) | Shared compiler and IR, .NET/JVM backends, runtime contracts |
| [numeric types](docs/contracts/numeric-types.md) | Common C#/Java types, C# decimal behavior, and portable runtime support |
| [functions and methods](docs/contracts/functions.md) | Executable overloads, named/optional arguments, static helpers, visibility, and current limits |
| [classes and construction](docs/contracts/objects.md) | Executable fields, constructors, instance methods, aliasing, and initialization checks |
| [properties](docs/contracts/properties.md) | Auto-properties, accessors, visibility, constructor initialization, and update evaluation |
| [object initialization](docs/contracts/initialization.md) | Object/nested initializers, init accessors, required members, and non-null safety |
| [ordinary control flow](docs/contracts/control-flow.md) | Loop execution, break/continue, conditional expressions, and shared return checks |
| [sharing middleware](docs/contracts/project-pipelines.md) | Explicit node connections and pipeline adoption across projects and DLL/jar boundaries |
| [roadmap](docs/roadmap.md) | AI engineering workflow, evolving milestones, and full-release criteria |
| [feature register](docs/feature-status.md) | Complete first-release inventory and implementation tracking |
| [implementation phases](docs/phases/README.md) | Seven phase folders with task checklists, dependencies, and feature ownership |
| [development](docs/development.md) | Prerequisites, compiler components, manifest, CLI, and verification |
| [acceptance map](docs/acceptance/README.md) | Full-release programs, diagnostics, failures, and feature interactions |
| [design decisions](docs/decisions/README.md) | Accepted contracts and proposals awaiting the designer |
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

Runnable examples: [foundation](examples/foundation/main.weft),
[functions](examples/functions/Program.weft), [classes](examples/classes/Program.weft),
[properties](examples/properties/Program.weft), and [initializers](examples/initializers/Program.weft).
Full-language design sketches: [examples/orders.weft](examples/orders.weft),
[examples/input.rules](examples/input.rules), [examples/switches.weft](examples/switches.weft).
