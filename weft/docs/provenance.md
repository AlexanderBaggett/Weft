# Provenance

Provenance is a set of origins attached to a value's type, recording where the value
came from and what it has passed through. It is the mechanism that lets a rule say
*"strings that came from HTTP must be sanitized before they are logged"* and have the
compiler enforce it everywhere, regardless of how many methods sit between the request
and the log call.

## Syntax

```csharp
string from Http                    // came from Http
string from Http | Queue            // came from either
string from Http via UserService    // entered via Http, then passed through UserService
string                              // empty provenance — matches no `from` pattern
```

Provenance is part of the type. `string from Http` is assignable to `string`?
**No.** A tagged value cannot be silently untagged; only a `transform` discharges a tag.
The reverse is fine: `string` is assignable to a parameter declared `string from Http`
only if the parameter is written `string from ?Http` (optional provenance). Most code
never writes provenance in signatures; it is inferred.

## What is an origin

Anything that produces values:

```csharp
origin Http;                         // primitive — declared because nothing else defines it
origin Queue;

service UserService { ... }          // every service is implicitly an origin
model  Order from Db { ... }         // every model is implicitly an origin (and here, sourced from Db)
```

Built-in origins are listed in [origins.md](origins.md).

## Stamping

A value acquires an origin when it:

1. Is bound by a receiver from an inbound origin (`LoginRequest from Http`).
2. Is produced by a source-only origin (`Db.Query(...)` → `from Db`).
3. Crosses a service boundary — every public method of a service stamps its return
   values and out-parameters with that service.
4. Is a field of a model declared `from X` and is read through that model.

## Propagation

| Operation | Result provenance |
|---|---|
| Assignment, parameter passing, return | unchanged |
| `a + b`, `$"{a}{b}"` (strings) | union of `a` and `b` |
| `a.Field` | provenance of `a` (fields inherit) |
| `a.Length`, `a.Count`, other scalar derivations | **open** — see [open-questions.md](open-questions.md) |
| `List<T from X>` | element provenance is preserved; the list itself is untagged |
| `T from X` → `T from X via S` | on crossing service `S` |
| Lambda capture | captured values keep their provenance inside the lambda |

Propagation is flow-insensitive within a method body and sound: if the compiler cannot
prove a value is untagged, it is tagged.

## Discharge

Only a `transform` removes an origin:

```csharp
transform Sanitize(string from Http) -> string;          // discharges Http
transform Trust(string from Http | Queue) -> string;      // discharges either
transform Parse(string from Http) -> int;                 // type change also discharges
```

`Sanitize(x)` where `x : string from Http via UserService` yields `string via UserService`?
**No** — `via` chains are provenance detail, not separate tags. Discharging `Http`
discharges the whole chain rooted at it.

A transform is a promise. The compiler does not verify that `Sanitize` actually
sanitizes; it verifies that nothing reaches a sink without passing through it.

## Matching

Rules match provenance with `from` patterns:

| Pattern | Matches a value whose provenance… |
|---|---|
| `from Http` | contains `Http` |
| `from Http \| Queue` | contains either |
| `from Http via UserService` | contains `Http` and the chain passes through `UserService` |
| `from Http only` | is exactly `{Http}` |
| `from *` | is non-empty |

## Source-only origins

Some origins never receive messages; they exist only to stamp values so rules can
reason about them:

```
Db, Cache, Config, Env, Clock, Random
```

```csharp
rule NoSecretsInLogs
{
    target  string from Env;
    effect  forbid Log;
}
```

## Erasure

Provenance exists at compile time only. After checking, `string from Http` is a
`string`. There is no runtime cost and no runtime reflection over provenance.

## Examples

```csharp
on Http.Post("/login")
Response<Session> Login(LoginRequest req)    // req : LoginRequest from Http
{
    Log(req.User);                           // → Log(Sanitize(req.User))   (rule fires)
    var u = Sanitize(req.User);              // u : string
    Log(u);                                  // untouched
    var msg = $"user={req.User}";            // msg : string from Http
    Log(msg);                                // → Log(Sanitize(msg))
    var found = UserService.Find(req.User);  // found : User from Http via UserService
}
```
