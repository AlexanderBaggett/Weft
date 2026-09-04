# Receivers

A receiver is the unified controller: a component whose methods are bound to **addresses**
on one or more **origins**. One method can answer several transports. The middleware
graph, rules, and triggers apply identically whichever way a message arrives.

## Anatomy

```csharp
receiver Orders : scoped                     // lifetime — see services.md
    from Http, Queue, Timer                  // origins this receiver accepts
    requires OrderService, Clock             // dependencies (and `requires Principal` for ambients)
{
    on Http.Post("/orders"), Queue(orders.create)
    Response<Order> Create(CreateOrder cmd) => OrderService.Create(cmd);

    on Http.Get("/orders/{id}")
    Response<Order> Get(Id<Order> id) => OrderService.Find(id) or reject NotFound;

    on Queue(orders.paid)
    void Paid(OrderPaid evt) => OrderService.MarkPaid(evt.Id);

    on Timer(every: 5m)
    void Sweep() => OrderService.ExpireStale(Clock.Now);
}
```

## Bindings

`on <address>[, <address>...]` precedes a method. Each address must belong to an origin
listed in `from`. A receiver with no `from` accepts every origin its bindings mention.

The compiler generates one entry stub per (binding, origin) pair, so:

- `cmd` in `Create` is `CreateOrder from Http` on the HTTP path and `CreateOrder from Queue`
  on the queue path. Rules that match `from Http` fire only on the HTTP path.
- Provenance-specific code is monomorphized; the shared body is compiled once.

## Parameter binding

| Origin | Parameter source |
|---|---|
| Http | route params by name, then query, then body (`Body<T>`); explicit `[Query] T x`, `[Header("X-Id")] T x` |
| Queue / Stream / Channel | the typed payload |
| Timer | none, or `TimerContext` |
| Ws | `Frame<T>`, `Connection` |
| Signal | none, or `SignalContext` |

Ambients are not parameters; read them with `Principal.Current`, or by `requires Principal`
and then `Principal` directly. See [ambients.md](ambients.md).

## Return types and outcomes

| Return | Meaning |
|---|---|
| `void` | `respond` with no body |
| `T` | `respond Ok(T)` |
| `Response<T>` | stdlib sum type: `Ok(T) \| Created(T) \| NotFound \| Invalid(Problem) \| …` — each case is an outcome |
| `reject Outcome` expression | terminate early |

For each binding, the compiler checks the origin can map every outcome the method can
produce. A `Response<T>` returned from a fire-and-forget `Channel` binding is a warning
(body discarded); a method that can `reject Unauthorized` bound to an origin with no
mapping for it is an error.

## Guards

A binding may open with a `guard` — the first statement of the body, run after parameter
binding and before anything else.

```csharp
on Http.Post("/bulk") requires Principal
Response<Receipt> Bulk(File[] files)
{
    guard
    {
        if (files.Count < 3)        reject Invalid("Bulk upload needs at least 3 files", field: "files");
        if (!Principal.Can(Upload)) reject Forbidden;
        approve;
    }
    return Created(Store.Ingest(files));
}
```

Same semantics as middleware guards: exhaustive `approve`/`reject`, no mutation. Rules
(`require Validate => reject Invalid`) are the declarative, cross-cutting form; guards are
the imperative, one-off form. Both produce the same outcome and `Problem`.

## `requires` on receivers

Two kinds, same keyword:

```csharp
requires OrderService              // a service: injected
requires Principal                 // an ambient: must be provided on every path that reaches this receiver
```

The second is checked against the middleware graph — see [middleware.md](middleware.md).
A binding-level `requires` narrows it further:

```csharp
on Http.Delete("/orders/{id}") requires Role.Admin
Response<None> Delete(Id<Order> id) => ...
```

## The `Request` tag

Any model that appears as a receiver parameter is implicitly tagged `Request`, which is
what `rule` targets like `target Request from Http` select. Models used only internally
are not `Request` and are not matched.

## Lifetime

Receivers are `scoped` by default (one instance per message). `singleton` receivers are
allowed when they hold no per-message state; the compiler applies the same captive-
dependency checks as for services.

## Triggers on receivers

`target boundary receiver` fires between `Route` and the handler method — after all
middleware, with the fully bound parameters. It is the right place for request-level
audit logging.

## Naming

`receiver` is plain and doesn't collide with HTTP vocabulary. Alternatives considered:
`port` (hexagonal architecture — precise, but fights with TCP ports), `inlet`, `intake`.
