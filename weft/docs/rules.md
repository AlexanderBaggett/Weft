# Rules

A rule attaches an effect to **values**, selected by type and provenance, and fires when
those values reach a **sink**. Rules live in `.rules` files and apply by `scope`, not by
import.

## Anatomy

```csharp
rule SanitizeLoggedInput
{
    scope   project;                     // where — default: project
    target  Request from Http;           // which values — type + provenance
    filter  string;                      // narrow to fields/values of this type
    effect  before Log => Sanitize;      // what happens, and at which sink
}
```

## The vocabulary rules bind to

Rules don't fire on arbitrary calls. They fire at declared points:

```csharp
sink      Log(string msg);                            // a place rules fire
sink      Db.Write(string sql);
sink      Http.Send(string body);

transform Sanitize(string from Http) -> string;       // discharges a provenance
transform Redact(string from Env)   -> string;

validate  NotEmpty(string s);                         // boolean, no discharge
validate  MaxLen(string s, int n);
```

Any function can be promoted to a sink with the `sink` modifier at its declaration.
Sinks are ordinary functions otherwise. `transform` and `validate` are likewise
ordinary functions with an extra compile-time contract.

## Target

Selects values by type and provenance.

```
target  Request from Http;                            // any model tagged Request, from Http
target  Request from Http in { LoginRequest, SignupRequest };
target  Request from Http | Queue;
target  string from Env;                              // bare type
target  * from Http;                                  // any value from Http
target  LoginRequest.Pass;                            // a specific field
```

`Request` is a model tag (see [receivers.md](receivers.md)) — the set of models that
appear as receiver parameters.

## Filter

Narrows a matched target. Type predicates, field predicates, or both.

```
filter  string;                                       // only string-typed values/fields
filter  string | string[];
filter  field.Name ends "Password";
filter  string where !(field is Secret);
```

Filter is optional. When the target is a model, the filter selects *which fields* the
effect applies to; when the target is a scalar, the filter is a predicate on the value.

## Effect kinds

| Form | Meaning |
|---|---|
| `before Sink => Transform` | Insert `Transform` on the value before it reaches `Sink`, if its provenance still matches. |
| `after Sink => Fn` | Call `Fn(value)` after `Sink` returns. Observation only. |
| `require Validate => reject Outcome` | At ingress (receiver binding), run `Validate`; on failure, raise `Outcome`. |
| `require Validate => throw E` | As above, but throw. |
| `forbid Sink` | Compile error if a matching value can reach `Sink`. The explicit-over-implicit option. |
| `replace Sink => Fn` | Call `Fn` instead of `Sink`. Use sparingly. |

Multiple effects may appear in one rule; they apply independently.

```csharp
rule PasswordHygiene
{
    target  LoginRequest.Pass from Http;
    effect  forbid Log;
              forbid Http.Send;
              require MaxLen(_, 128) => reject Invalid;
}
```

## Idempotence of `before`

`before Log => Sanitize` fires only while the value's provenance still matches the
target. Once `Sanitize` runs — inserted or hand-written — the `Http` tag is discharged
and the rule no longer matches. A value is therefore never sanitized twice, and code
that already sanitizes is left alone.

## Precedence

When more than one rule matches the same value at the same sink:

1. Narrower **scope** wins (`method` > `type` > `namespace` > `module` > `project`).
2. Then narrower **target** (`LoginRequest.Pass` > `LoginRequest` > `Request`).
3. Then explicit `priority N` (higher wins; default 0).
4. Otherwise both apply, in declaration order, and the compiler emits a warning.

`forbid` always wins over `before`/`after` — a forbidden reach is an error regardless of
what other rules would have inserted.

## Rules under switches

`scope flag X` makes a rule conditional at runtime; `kill` turns it off during an
incident; `canary` A/B-tests its transform. See [switches.md](switches.md). One
restriction: `forbid` is a compile-time proof and cannot be switched.

## Grouping and suppression

```csharp
ruleset InputHygiene
{
    rule SanitizeLoggedInput { ... }
    rule PasswordHygiene     { ... }
}

use ruleset InputHygiene;                          // in weft.toml or a rules file
suppress SanitizeLoggedInput in namespace Tests;   // turn a rule off in a scope
suppress ruleset InputHygiene in module Fixtures;
```

## Diagnostics

The compiler reports:

- **Unreachable rule** — the target never occurs in the scope. Warning.
- **Ambiguous precedence** — two rules tie at the same sink. Warning.
- **Forbidden reach** — a `forbid` matched. Error, with the full path from origin to sink.
- **Missing transform** — a `before` effect names a transform whose input type does not
  accept the target's provenance. Error.

## Relationship to other constructs

- A **trigger** that passes values to `Log` is itself a place where value-rules fire.
- **Middleware** effects are subject to rules too — `msg.Body` is `from Http`.
- Rules fire on values, not on messages; they don't know about routes or topics.
  That's middleware's job.
