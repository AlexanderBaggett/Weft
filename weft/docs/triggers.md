# Triggers

A trigger attaches an effect to **points** — boundary crossings — rather than to values.
"Log everything passed between methods in this namespace" is a trigger. Triggers share
the four-part shape with rules and live in `.rules` files; the separate keyword exists
for reader intent and because the effect receives a *point*, not a value.

## Anatomy

```csharp
trigger LogCrossings
{
    scope   namespace Api.Orders;
    target  boundary method internal;
    filter  args where !(arg is Secret);
    effect  on enter => Log($"{point.Name}({point.Args})");
              on exit  => Log($"{point.Name} -> {point.Result}");
              on throw => Log(point.Exception);
}
```

## Target

```
target  boundary method;                     // every method call in scope
target  boundary method internal;            // caller AND callee both in scope
target  boundary method external;            // caller in scope, callee outside (or vice versa)
target  boundary service;                    // public methods of services
target  boundary service OrderService;       // one service
target  boundary receiver;                   // receiver bindings (after middleware, before handler)
target  boundary transform;                  // every transform call — audit sanitization
```

`internal` is the interesting one: it selects calls where both ends are inside the
scope, which is what "log what gets passed between methods in this module" means.

## Filter

Predicates over the point's arguments, result, or metadata.

```
filter  args where !(arg is Secret);
filter  point.Name starts "Try";
filter  point.Result is Response<*>;
filter  args.Count > 0;
```

## The `point` object

Available inside trigger effects.

| Member | Type | Available on |
|---|---|---|
| `point.Name` | `string` | all |
| `point.Args` | `object[]` (typed per-arg when statically known) | all |
| `point.Result` | return type of the callee | `exit` |
| `point.Exception` | `Exception` | `throw` |
| `point.Caller` | `MethodRef` | all |
| `point.Callee` | `MethodRef` | all |
| `point.Elapsed` | `Duration` | `exit`, `throw` |
| `point.Ambient<T>()` | `T` | all — read an ambient at the crossing |

`point.Args` preserves provenance: an argument that is `string from Http` remains so
inside the trigger, which means value-rules fire on it if the trigger passes it to a sink.

## Effects

```
on enter => expr;
on exit  => expr;
on throw => expr;
on enter => { statements }         // block form
```

Effects run synchronously at the crossing. They cannot alter arguments or results —
triggers observe. If you want to change what crosses a boundary, that is a rule
(`before`/`replace`) or middleware.

## Common triggers

```csharp
trigger Timing
{
    target  boundary service;
    effect  on exit => Metrics.Record(point.Callee, point.Elapsed);
}

trigger AuditTransforms
{
    scope   project;
    target  boundary transform;
    effect  on enter => Audit.Log($"{point.Name} on {point.Args[0].Provenance}");
}

trigger TraceEverything
{
    scope   namespace Api.Orders;
    target  boundary method internal;
    effect  on enter => Trace.Span(point.Name, point.Ambient<Correlation>());
}
```

## Cost

Triggers are compiled into the call site. A trigger on `boundary method` in a hot
namespace is a real cost, and the compiler says so: a trigger whose scope covers more
than N call sites emits an informational diagnostic with the count.

## Triggers under switches

`scope flag VerboseTrace` is the "turn on debug logging in prod without a deploy" case.
A `kill` with `trip when overhead_p99 > 2ms` makes a trigger that gets too expensive
disable itself. See [switches.md](switches.md).

## Precedence

Triggers don't conflict — multiple triggers at one point all run, in this order:
narrower scope first, then declaration order. `priority N` overrides.
