# Ambients

An ambient is a value that travels with a message without being passed as a parameter:
who the caller is, what the trace id is, when the deadline hits. Languages without this
feature thread a parameter through every signature or reach for thread-locals. Weft
threads it in the compiler and **proves availability** at every use.

## Declaration

```csharp
ambient Principal;
ambient Correlation;
ambient Deadline;
ambient Tenant;
```

Built-in ambients: `Principal`, `Correlation`, `Deadline`. Origins add their own
(`Connection`, `Partition`, `Attempt`).

## Providing

Middleware provides ambients; the graph proves where they are available.

```csharp
middleware Auth from Http provides Principal
{
    effect  msg.Principal = Verify(...) or reject Unauthorized;
    next    Route;
}

middleware Trace provides Correlation
{
    effect  msg.Correlation = msg.Headers["X-Correlation"] ?? Correlation.New();
    next    Auth;
}
```

`provides X` is a promise the effect must fulfil: an effect that can fall through
without assigning `msg.X` is an error.

## Requiring and reading

```csharp
receiver Admin requires Principal
{
    on Http.Get("/admin/users")
    Response<User[]> Users() => Principal.Is(Role.Admin) ? Store.All() : reject Forbidden;
}

service AuditLog requires Correlation
{
    void Write(string line) => Sink.Write($"{Correlation} {line}");
}
```

Inside a construct that `requires X`, `X` is in scope as an expression. Elsewhere,
`X.Current` reads it and returns `Option<X>`; the compiler will not let you read a bare
ambient it cannot prove is present.

## Availability proof

For each use of a required ambient, the compiler checks that every path from a pipeline
entry to that use passes through a provider. Paths run through the middleware graph
*and* the call graph: a service that `requires Principal` can only be called from code
that itself has `Principal` available. This is checked, not hoped.

## Propagation across origins

Ambients cross transports when the sending side and origin agree:

```csharp
Queue.Send(orders.create, cmd);          // Correlation is automatically attached as a header
                                          // if the topic's origin declares `carries Correlation`
```

Built-in inbound origins declare `carries Correlation, Deadline` where the transport has
a natural place for it (HTTP headers, message headers). `Principal` is deliberately
*not* carried by default — re-authenticate on the other side, or opt in explicitly.

## Capture rules

Ambients are message-scoped. Capturing one in a `singleton`, a static, or a detached
background task is an error — the same lifetime rule as for `scoped` services.

## Deadline

`Deadline` is special: it is a budget, not a value. Every I/O call the compiler knows
about (origin sends, `Db`, `Cache`, outbound `Http`) honors it automatically. An
`effect-scope` can narrow it:

```csharp
deadline 200ms
{
    var price = Pricing.Quote(item);      // fails with Timeout if the budget is exceeded
}
```

See [effect-scopes.md](effect-scopes.md).

## Relationship to provenance

Provenance and ambients ride the same propagation machinery — tags on values, checked
by the compiler, erased at runtime. Provenance describes *where a value came from*;
ambients describe *the context a message is in*. Both feed rules:

```csharp
rule TenantIsolation
{
    target  Query from Db;
    effect  => require Tenant.Matches(_) else reject Forbidden;
}
```
