# Rules

A rule selects **values** — by type, provenance, and shape — and attaches one or more
**effects** to them. Each effect names a **point** on the value's lineage where it fires
and an **action** that runs there. The action can be a function, a validation, a
compile-time prohibition, or a code block. Rules live in `.rules` files and apply by
`scope`, not by import.

## Anatomy

```csharp
rule Name
{
    scope   project;                              // where in the program      (default: project)
    target  Request from Http;                    // what values, with source filter
    filter  string;                               // narrow — 0..n lines, AND'd
    priority 0;                                   // tie-break                 (default: 0)
    effect  [at <point> [where <site>]] => <action>;   // 1..n
}
```

The original example, three ways:

```csharp
rule IncomingStrings
{
    target  Request from Http in { LoginRequest, SignupRequest };
    filter  string;
    effect  => Trim;                              // at bind: eagerly, every string
    effect  at Log => Sanitize;                   // lazily: only if it is logged
    effect  at cross into Db.* => (v)             // arbitrary code at a lineage point
    {
        Audit.Record(point.Site, v.Provenance);
        return v;                                 // return replaces; omit to observe
    }
}
```

## Target

Selects values by type and provenance. The provenance clause is the *source filter*.

```
target  Request from Http;                        // any model tagged Request, from Http
target  Request from Http in { LoginRequest, SignupRequest };
target  Request from Http | Queue;
target  string from Env;                          // bare type
target  * from Http;                              // anything from Http
target  LoginRequest.Pass;                        // one field
target  Order from OrderService;                  // values coming *out* of a service
```

`Request` is the implicit tag on every model that appears as a receiver parameter.

## Filter

Narrows the target. Filters are about the **value** — its type, contents, the field it
sits in, where it has been, and the context around it. Site selection (which call site)
belongs on `at ... where`, not here.

### Kinds

```csharp
// type
filter string;
filter string | string[];
filter not Id<*>;
filter Secret<*> | [Pii];                          // type or attribute

// property pattern — C# semantics: nesting, relational, or/and/not, type patterns
filter { Length: > 50 };
filter { Items: { Count: >= 3 }, Note: not null };
filter { Address: { Country: "US" or "CA" } };

// predicate — the value's members are in scope; `value` names the whole thing
filter string where Length > 50 || IsNullOrWhiteSpace;
filter int where value in 1..100;
filter string where matches @"^\d{3}-\d{4}$";

// field — the slot, not the contents
filter field.Name ends "Password" || field has [Sensitive];
filter field.Path matches "**.Address.*";
filter field.Optional;
filter field.Index == 0;                           // for method arguments

// collections — quantify, or retarget the effect to elements
filter string[] any { Length: > 50 };
filter LineItem[] all { Price: >= 0 };
filter each of string[];                           // effects now apply per element

// lineage — where it has been, beyond `from`
filter lineage crossed OrderService;
filter lineage transformed by Trim;
filter lineage hops > 2;

// context — runtime state around the value
filter when Principal.Role != Admin;
filter when Tenant.Tier == Free;
```

### Composition

- Several `filter` lines AND together. `||`, `&&`, and `!`/`not` work inside a line.
- A property pattern or predicate after a type filter is scoped to that type. Without
  one, the compiler matches structurally — any target field that *has* the named members.
- Named filters are declared at file or ruleset level and reused:

```csharp
filter Sensitive = field.Name ends "Password" || field has [Secret] || Secret<*>;
filter FreeText  = string && (field.Name ends "Note" || field.Name ends "Comment");

rule NoSecretsOut
{
    target  Request from Http;
    filter  Sensitive;
    effect  at Egress => forbid;
}
```

### Static vs runtime

| Filter kind | Resolved | Cost |
|---|---|---|
| type, attribute, field, lineage | compile time | none — selects sites |
| property pattern, predicate, `when` | runtime | one branch per site |

Mixed rules are fine: static filters narrow the sites first, runtime filters guard what
remains. `weft rules --cost` reports the runtime residue per rule. A `static rule` fails
the build if it has any runtime residue — use it on hot paths.

### Not filters

Rate and statistical conditions (`rate > 100/s`) are tripwires — see `kill ... trip when`
in [switches.md](switches.md). Conditions on *other* values' runtime state are joins and
belong in code.

## Points — `at`

Where on the value's lineage the effect fires. Omitted means `bind`.

| point | fires when the value… |
|---|---|
| `bind` | acquires this provenance: bound from an origin, returned by a service, read from `Db` |
| `call Fn` · `call Ns.*` · bare `Fn` | is passed to that function (or any matching the glob) |
| `after Fn` | comes back out of that call — `point.Result` is available |
| `cross into X` · `cross out of X` · `cross` | crosses a service boundary |
| a named set | see below |

`at` may carry a site predicate:

```csharp
effect  at Log where point.Caller in Api.Admin.*  => Sanitize;
effect  at call Db.* where point.ArgIndex == 0    => Escape;
```

`point` exposes `Site`, `Caller`, `Callee`, `ArgIndex`, `Result` (after), and the
value's `Provenance`.

### Named point sets — `sink`

Any function is a valid point; `sink` just names groups of them.

```csharp
sink Egress  = Log, Http.Send, Queue.Send, Stream.Send;
sink Persist = Db.Write, Db.Update, Db.Delete;
```

Built-in: `Egress` (every origin's send plus `Log`) and `Persist` (every `Db` write).
Origins contribute to them; custom origins declare which set their sends join.

## Actions

| Form | Meaning |
|---|---|
| `=> Fn` | If `Fn` returns the value's type, the result **replaces** the value. If it returns `void`, it **observes**. `_` stands for the value in an argument list; if absent, the value is passed first: `=> Truncate(_, 2000)`. |
| `=> require Check else reject Outcome` | Run `Check`; on failure raise the outcome (with optional message or `Problem`). `else throw E` also allowed. |
| `=> forbid` | Compile error if a matching value can reach this point. Only meaningful at `call`/`cross`/set points. |
| `=> (v) { ... }` | Code block. `return x` replaces; no return observes; `reject` allowed; `point` is in scope. |

Provenance is discharged **only** by a function declared `transform`. A block or a plain
function that replaces the value leaves its provenance intact.

```csharp
transform Sanitize(string from Http | Queue) -> string;   // discharges
validate  MaxLen(string s, int n);                        // boolean
```

## Idempotence of transforms

`at Log => Sanitize` fires only while the value's provenance still matches the target.
The result of `Sanitize` is discharged and no longer matches. A lazy insertion replaces
only the selected argument use: two later `Log(req.User)` calls can each need sanitation
because the original field remains tagged. A separately bound clean value does not.
An eager `=> Sanitize` at bind replaces the bound value before the receiver sees it,
making later lazy rules for the same roots no-ops. This follows the accepted
[replacement contract](decisions/0001-phase-1-semantics.md#b-rule-replacement-and-precedence).

## Precedence

When more than one rule matches the same value at the same point:

1. Narrower **scope** wins (`method` > `type` > `namespace` > `project`).
2. Then narrower **target** (`LoginRequest.Pass` > `LoginRequest` > `Request`).
3. Then `priority N` (higher wins).
4. Otherwise both apply, in declaration order, with a warning.

`forbid` always wins.

Precedence applies to the whole matching rule at that value/point; broader validators
and observers do not implicitly compose when a more specific rule wins. Runtime filters
can make the narrower rule ineligible, in which case the broader fallback applies.
Matching prohibitions are checked against incoming provenance regardless of specificity.
Generated calls participate in the same checks, and recursive insertion is an error.

## Rules under switches

`scope flag X` makes a rule conditional at runtime; `kill` turns it off during an
incident; `canary` A/B-tests its transform. See [switches.md](switches.md). `forbid`
cannot be switched — it is a compile-time proof.

## Grouping and suppression

```csharp
ruleset InputHygiene
{
    filter FreeText = ...;
    rule SanitizeLoggedInput { ... }
    rule LongFreeText        { ... }
}

use ruleset InputHygiene;
suppress SanitizeLoggedInput in namespace Tests;
suppress ruleset InputHygiene in project Fixtures;
```

## Diagnostics

- **Unreachable rule** — the target never occurs in scope. Warning.
- **Dead filter** — a filter line that can never be true given earlier lines. Warning.
- **Ambiguous precedence** — two rules tie at the same point. Warning.
- **Forbidden reach** — a `forbid` matched. Error, with the path from origin to point.
- **Bad replace** — an action's return type doesn't match the value's type. Error.
- **Runtime residue in `static rule`** — Error, listing the offending filter lines.

## Worked example

```csharp
rule LongFreeText
{
    target  Request from Http;
    filter  string;
    filter  field.Name ends "Note" || field.Name ends "Comment";
    filter  { Length: > 2000 };
    filter  when Tenant.Tier == Free;
    effect  => Truncate(_, 2000);
    effect  at Log where point.Caller in Api.* => (v) { Metrics.Count("long_note"); };
}
```

Static filters select every `Note`/`Comment` string field on every `Request` from HTTP;
the pattern and `when` become one branch at each of those sites.

## Relationship to other constructs

- **Triggers** fire at points regardless of value; rules fire on values regardless of
  point. A trigger that passes a tagged value to `Log` is itself a `call Log` point.
- **Middleware** effects are subject to rules — `msg.Body` is provenance-tagged.
- Rules don't know about routes or topics. That's middleware.
