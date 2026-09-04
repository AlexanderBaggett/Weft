# Switches — flags, canaries, kill switches

Feature flags, canaries, and kill switches are one mechanism with three defaults. A
**switch** is a declared, typed, runtime-valued thing the compiler can see every state
of. It can gate any cross-cutting construct — middleware, rules, triggers, receivers,
services, caches, origins — not just an `if`.

| | default | direction | changes by | must declare |
|---|---|---|---|---|
| `flag` | off | enables new behavior | operator | retirement |
| `canary` | baseline | shifts traffic to a candidate | judge + schedule | matching contracts |
| `kill` | on | disables existing behavior | operator or tripwire | degraded behavior |

## The substrate

```csharp
flag NewCheckout : bool default off source Remote("flags") refresh 30s by Tenant
    retire 2026-12-01;
flag StrictInput : bool default off source Db by Tenant;
flag NewAuthPath : bool static default off;          // resolved at build; dead arm eliminated
```

- **Declared.** An undeclared switch name is a compile error. `IsEnabled("new-chekout")`
  is the bug this removes.
- **Snapshot per message.** Evaluated once, at first read; stable for the rest of that
  message. A flag flipping mid-request can't produce a half-old, half-new request.
- **Usable as scope.** `scope flag X` on rules, triggers, middleware, caches; `when X` on
  receiver bindings. `!flag X` for the complement.
- **Every state is verified.** A construct scoped to a switch is proved with the switch in
  each state. Path proofs, outcome exhaustiveness, and provenance checks all run per state.
- **Targeting uses ambients.** `by Tenant` means `Tenant` must be available wherever the
  switch is read — the same proof as `requires`.
- **Sources** are `Config`, `Db`, `Remote("provider")`, or `static`. `refresh` sets the
  poll cadence. `unreachable => last-known` (default) or `unreachable => <value>`.

### Combinatorics

Proofs run per switch, others at their defaults. Switches that are only meaningful
together go in a group and are proved jointly:

```csharp
switch group CheckoutRollout { NewCheckout, NewPricingRules, CheckoutV2Metrics }
```

Group size is bounded (default 6). This is the trade-off made explicit, not discovered.

---

## Feature flags

### On receivers

```csharp
receiver Checkout : scoped from Http
{
    on Http.Post("/checkout") when NewCheckout
    Response<Receipt> V2(Cart cart) => CheckoutV2.Run(cart);

    on Http.Post("/checkout") when !NewCheckout
    Response<Receipt> V1(Cart cart) => CheckoutV1.Run(cart);
}
```

Two bindings on one address with complementary conditions is legal; the compiler checks
the conditions partition — no overlap, no gap. A flagged binding with no complement
yields `NotFound` when off.

### On middleware

Off means transparent. Both states are proved.

```csharp
middleware RateLimitV2 from Http requires Principal
{
    scope   flag NewRateLimit;
    effect  Limiter2.Take(msg.Principal.Id) or reject TooMany;
    next    Route;
}

middleware Dispatch
{
    next match
    {
        VerboseTrace && msg.Path starts "/api" => TraceDump;   // flags are expressions too
        else                                   => Auth;
    }
}
```

A flagged node that `provides` an ambient is proved in the off state as well: if it was
the only provider on some path, that's a compile error.

### On rules

Off means the rule doesn't fire; the inserted call becomes guarded.

```csharp
rule StrictSanitize
{
    scope   flag StrictInput, namespace Api;             // flag AND namespace
    target  Request from Http;
    filter  string;
    effect  at Log => SanitizeStrict;                    // emitted as Log(StrictInput ? SanitizeStrict(x) : x)
}
```

Precedence is resolved per state. With `StrictInput` off, the wider unflagged
`SanitizeLoggedInput` rule applies; the compiler proves nothing reaches `Log`
unsanitized in either state.

### On triggers

The "turn on debug logging in production without a deploy" case.

```csharp
trigger DebugCrossings
{
    scope   flag VerboseTrace, namespace Orders.Internal;
    target  boundary method internal;
    effect  on enter => Log($"{point.Caller} -> {point.Callee}({point.Args})");
}
```

Compiled to one branch per call site; the flag is read once per message, so a request
is fully traced or not at all.

### Retirement

```csharp
flag NewCheckout : bool ... retire 2026-12-01;
```

After the date every use site is a warning; thirty days later, an error. `weft flags`
lists every flag, its sites, and its age. Flag debt stops being invisible.

---

## Canaries

A canary is a switch whose value is an *arm*, whose selection evolves on a schedule, and
whose judgment uses metrics the runtime already collects — every boundary is a trigger
point, so per-arm timing and outcome counts exist without instrumentation.

```csharp
canary PricingV2
{
    scope     service IPricing;                      // the slot
    baseline  PricingV1;
    candidate PricingV2;                             // identical contract, or compile error
    select    5% by Tenant sticky;                   // sticky: same tenant, same arm
    judge     over 10m:
                error_rate <= baseline + 0.5%,
                p99        <= baseline * 1.2;
    promote   auto steps 5% 25% 50% 100%;
    rollback  auto;
}
```

- **Arms must match.** Same `exposes`, `provides`/`requires`, outcomes, and — for
  transforms — signature. Both arms stay in the graph; promotion and rollback move only
  the selection, so structural proofs hold throughout.
- **`sticky by X`** requires ambient `X` on every path reaching the canary.
- **`mode shadow`** runs the candidate alongside the baseline, returns the baseline's
  result, and diffs. A shadow candidate may not make `external` calls — compile error.
  `diff_rate` is derived automatically in shadow mode.
- **Judge metrics** available: `error_rate`, `reject_rate`, `p50/p95/p99`, `throughput`,
  `overhead_*` (triggers), `diff_rate` (shadow). `baseline` in an expression means the
  baseline arm's value over the same window.

### On middleware

```csharp
canary RateLimiter
{
    scope     middleware RateLimit;
    baseline  RateLimitV1;
    candidate RateLimitV2;                           // same from / provides / requires / outcomes
    select    10% by Principal sticky;
    judge     over 15m: error_rate <= baseline + 0.1%, p99 <= baseline * 1.1;
    promote   auto steps 10% 50% 100%;
    rollback  auto;
}
```

The two nodes' `next` may differ; the compiler proves each.

### On rules

Canary a transform against real traffic before trusting it.

```csharp
canary Sanitizer
{
    scope     rule SanitizeLoggedInput;
    baseline  Sanitize;
    candidate SanitizeStrict;                        // same input provenance and output type
    mode      shadow;
    select    100%;
    judge     over 1h: diff_rate < 0.01%, error_rate <= baseline;
    promote   manual;
}
```

Transforms are pure, so shadowing them is safe by construction.

### On triggers

Migrate an observability backend.

```csharp
canary MetricsBackend
{
    scope     trigger ServiceTiming;
    baseline  on exit => Metrics.Record(point.Callee, point.Elapsed);
    candidate on exit => MetricsV2.Record(point.Callee, point.Elapsed);
    select    5%;
    judge     over 30m: overhead_p99 <= baseline * 1.2, error_rate <= baseline;
    promote   auto;
}
```

Triggers observe, so the judge is about the trigger's own cost and failures.

---

## Kill switches

A kill switch defaults on, turns something off, and must declare what the rest of the
program sees when it does.

```csharp
kill Recommendations
{
    scope   service RecommendationService;
    killed  => Fallback.Empty;                       // must satisfy the same contract
}

kill BulkUploads
{
    scope   receiver Uploads.Bulk;
    killed  => reject Unavailable("Bulk uploads are temporarily disabled");
}

kill QueueIntake
{
    scope   origin Queue;                            // stop consuming; messages stay queued
}
```

- **Degradation is mandatory and type-checked.** A killed service's callers still get a
  value; the fallback must satisfy the contract. A killed binding rejects with an
  outcome every origin it serves can map.
- **Fail direction is declared.** `unreachable => last-known` (default) or
  `unreachable => kill`.
- **Tripwires make circuit breakers.** `trip when <metric>` is an automatic kill;
  `reset after <duration>, probe <percent>` is an automatic un-kill with a half-open
  state.

```csharp
kill Pricing
{
    scope   service IPricing;
    trip    when error_rate > 50% over 1m;
    reset   after 5m, probe 1%;
    killed  => reject Unavailable;
}
```

### On middleware

Killed means transparent, and the path proofs re-run without the node.

```csharp
kill RateLimiting
{
    scope   middleware RateLimit;                    // legal: nothing downstream requires what it provides
}

kill Auth
{
    scope   middleware Auth;                         // COMPILE ERROR:
}                                                    //   Admin.Users requires Principal; no path provides it with Auth killed.

kill Auth
{
    scope   middleware Auth;
    killed  => reject Unavailable("Auth backend down");   // legal: fail closed
}
```

The compiler tells you which nodes are load-bearing. Failing closed is the only way to
make one killable.

### On rules

The validator that starts rejecting good traffic during an incident.

```csharp
kill NoteValidation
{
    scope   rule BoundedNotes;                       // `require`, `before`, `after` all stop
    trip    when reject_rate > 30% over 5m;
    reset   after 15m, probe 1%;
}
```

### On triggers

Logging that's flooding the pipeline — the kill you will actually use.

```csharp
kill DebugCrossings
{
    scope   trigger DebugCrossings;
    trip    when overhead_p99 > 2ms over 1m;         // a trigger that gets too expensive kills itself
    reset   after 10m;
}

kill AllTracing
{
    scope   trigger Tracing.*;                       // glob over a ruleset
    unreachable => last-known;
}
```

---

## Restrictions

- **`forbid` cannot be switched.** It's a compile-time proof, not a runtime effect. Any
  switch scoped to a rule containing `forbid` is an error. A runtime-toggleable block is
  `effect at Egress => require Never(_) else reject Forbidden`.
- **`provides` under a switch is re-proved per state.** A flagged or killable provider
  must not be the sole provider on any path that needs it, or must fail closed.
- **Canary arms:** on rules, transforms with identical signatures; on triggers, effects on
  the same boundary kind; on middleware, nodes with identical contracts; on services,
  implementations of the same contract.
- **`by` ambients** must be available at every read site.
- **Switch scopes** name constructs: `middleware X`, `rule X`, `trigger X`, `receiver X.Y`,
  `service X`, `origin X`, `cache X`, with `.*` globs.

## What this saves

The flag check scattered through forty files lives in one `scope`. "What breaks if I turn
this off" is answered by the compiler. Stale flags are enforced out. Canaries need no
instrumentation because boundaries already emit metrics. Kill switches can't leave the
system in an undefined state because the degraded behavior is part of the declaration.
