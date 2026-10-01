# Grammar sketch

EBNF-ish language design, with the reconciled declaration/block inventory below.
Ordinary expressions/statements follow the C#-adjacent contracts recorded in
[decision 0002](decisions/0002-ordinary-and-portable-contracts.md). `?` optional,
`*` zero or more, `+` one or more, `|` alternation. This describes the complete release;
the executable foundation subset is identified at the end.

```ebnf
compilation-unit  = { declaration } ;

declaration       = origin-decl | model-decl | service-decl | receiver-decl
                  | sink-decl | transform-decl | validate-decl | filter-decl
                  | rule-decl | trigger-decl | ruleset-decl | suppress-decl
                  | middleware-decl | pipeline-decl
                  | ambient-decl | address-decl | table-decl
                  | flag-decl | canary-decl | kill-decl | switch-group-decl
                  | scope-directive | use-directive
                  | cache-decl | outcome-decl
                  | namespace-decl | extern-decl
                  | class-decl | record-decl | interface-decl | type-decl
                  | function-decl ;                            (* ordinary code *)

(* ---------- provenance ---------- *)

provenance        = "from" origin-pattern ;
origin-pattern    = origin-term { "|" origin-term } [ "only" ] ;
origin-term       = [ "?" ] ident { "via" ident } | "*" ;
typed             = type [ provenance ] ;                       (* e.g. string from Http *)

(* ---------- origins ---------- *)

origin-decl       = "origin" ident [ ":" "adapter" ident ] ( ";" | origin-body ) ;
origin-body       = "{" { origin-member } "}" ;
origin-member     = "address"  address-grammar ";"
                  | "envelope" "{" { field } "}"
                  | "outcomes" [ "match" ] "{" { outcome-map } "}"
                  | "provides" ident-list ";"
                  | "carries"  ident-list ";" ;
outcome-map       = ( "respond" | outcome | "*" ) "=>" action ";" ;

address-decl      = ( "topic" | "channel" ) qualified-ident ":" type
                    "on" ident [ "(" arg-list ")" ] ";"
                  | "channel" ident ":" type [ "(" arg-list ")" ] ";" ;

(* ---------- models & services ---------- *)

model-decl        = "model" ident [ provenance ] "{" { field } "}" ;
field             = type ident ";" ;

service-decl      = "service" ident [ ":" lifetime ] { service-clause } "{" { member } "}" ;
lifetime          = "singleton" | "scoped" | "transient" ;
service-clause    = "requires" ident-list
                  | "exposes"  ident-list
                  | "config"   ident
                  | "mode"     ( "reentrant" | "serialized" | "partitioned" "(" ident ")" ) ;

receiver-decl     = "receiver" ident [ ":" lifetime ] { receiver-clause } "{" { binding } "}" ;
receiver-clause   = "from" ident-list | "requires" ident-list ;
binding           = "on" address { "," address } [ "requires" ident-list ] [ "when" expression ] method-decl ;
address           = ident "." ident [ "(" arg-list ")" ] [ "." ident ]   (* Http.Post("/x"), Ws("/l").Frame *)
                  | ident "(" arg-list ")" ;                             (* Queue(t), Timer(every: 5m) *)

(* ---------- rule vocabulary ---------- *)

transform-decl    = "transform" ident "(" typed [ ident ] ")" "->" type ( ";" | block ) ;
validate-decl     = "validate"  ident "(" param-list ")" ";" ;
sink-decl         = "sink" ident "=" qualified-ident { "," qualified-ident } ";" ;   (* a named set of points *)
filter-decl       = "filter" ident "=" filter-expr ";" ;                            (* a named, reusable filter *)

(* ---------- the four-part shape ---------- *)

scope-clause      = "scope" scope-expr ";" ;
scope-expr        = scope-term { "," scope-term } ;
scope-term        = "project" [ ident ]
                  | "namespace" qualified-ident
                  | "type"      qualified-ident
                  | "method"    qualified-ident
                  | "receiver"  qualified-ident [ ".*" ]
                  | qualified-ident [ ".*" ]                 (* inherited scope kind in a list *)
                  | [ "!" ] "flag" ident ;
scope-directive   = scope-clause ;                               (* file-level default *)

priority-clause   = "priority" integer ";" ;

(* ---------- rules ---------- *)

rule-decl         = [ "static" ] "rule" ident "{"
                    [ scope-clause ] rule-target { filter-clause } [ priority-clause ]
                    effect-clause { effect-clause } "}" ;

rule-target       = "target" ( typed | "*" [ provenance ] ) [ "in" "{" ident-list "}" ] ";"
                  | "target" qualified-ident [ provenance ] ";" ;           (* Model.Field *)

filter-clause     = "filter" filter-expr ";" ;                   (* several lines AND together *)
filter-expr       = filter-term { ( "||" | "&&" ) filter-term } ;
filter-term       = [ "not" | "!" ] filter-atom | "(" filter-expr ")" ;
filter-atom       = type-filter
                  | property-pattern
                  | [ type ] "where" expression                   (* value's members in scope; `value` = whole *)
                  | "field" "." ident comparison                  (* Name, Path, Type, Index, Optional *)
                  | "field" "has" attribute
                  | type "[" "]" ( "any" | "all" ) ( property-pattern | "where" expression )
                  | "each" "of" type "[" "]"
                  | "lineage" lineage-pred
                  | "when" expression                             (* ambients, flags, msg *)
                  | ident ;                                       (* a named filter *)
type-filter       = type { "|" type } | attribute ;              (* type may use `*` wildcards: Secret<*> *)
property-pattern  = "{" prop-sub { "," prop-sub } "}" ;
prop-sub          = ident ":" pattern ;
pattern           = constant | relational constant | "null" | "not" pattern
                  | pattern ( "or" | "and" ) pattern | type | property-pattern ;
lineage-pred      = "crossed" qualified-ident | "transformed" "by" ident | "hops" comparison ;

effect-clause     = "effect" [ "at" point-expr [ "where" expression ] ] "=>" action ";" ;
point-expr        = point { "," point } ;
point             = "bind"
                  | "call" ( qualified-ident | glob )
                  | qualified-ident                               (* shorthand for call *)
                  | "after" qualified-ident
                  | "cross" [ ( "into" | "out" "of" ) ( qualified-ident | glob ) ]
                  | ident ;                                       (* a sink set *)
action            = qualified-ident | call                        (* shorthand function or explicit arguments; `_` = value *)
                  | "require" call "else" ( "reject" outcome [ expression ] | "throw" type )
                  | "forbid"
                  | "(" ident ")" block ;                          (* return replaces; none observes; reject allowed *)

ruleset-decl      = "ruleset" ident "{" { filter-decl | rule-decl | trigger-decl } "}" ;
suppress-decl     = "suppress" [ "ruleset" ] ident "in" scope-expr ";" ;
use-directive     = "use" "ruleset" ident ";"
                  | "use" ( "middleware" | "pipeline" ) qualified-ident "as" ident ";" ;

(* ---------- triggers ---------- *)

trigger-decl      = "trigger" ident "{" [ scope-clause ] trigger-target [ trigger-filter ]
                    [ priority-clause ] "effect" trigger-effect { trigger-effect } "}" ;
trigger-filter    = "filter" expression ";" ;                    (* over point / args *)
trigger-target    = "target" "boundary" boundary-kind [ boundary-mod ] [ qualified-ident ] ";" ;
boundary-kind     = "method" | "service" | "receiver" | "transform" ;
boundary-mod      = "internal" | "external" ;
trigger-effect    = "on" ( "enter" | "exit" | "throw" ) "=>" ( expression | block ) ";" ;

(* ---------- middleware ---------- *)

middleware-decl   = [ visibility ] "middleware" ident [ generic-params ] { mw-clause }
                    "{" [ scope-clause ] [ "filter" expression ";" ] [ "guard" ( "match" match-block | block ) ]
                        [ "effect" block-or-stmts ]
                        [ "after" block-or-stmts ] next-clause "}" ;
mw-clause         = "from"     ident-list
                  | "provides" ident-list
                  | "requires" ident-list
                  | "reentrant" "(" "max" ":" integer ")" ;
match-block       = "{" { expression "=>" verdict-or-target ";" } [ "else" "=>" verdict-or-target ";" ] "}" ;
verdict-or-target = "approve" | "reject" outcome [ expression ] | next-target ;
next-clause       = "next" ( next-target ";" | "match" [ "(" expression ")" ] match-block | block ) ;
                                                                    (* block must return a next-target *)
next-target       = qualified-ident                              (* node or pipeline origin entry *)
                  | "any" expression "else" terminal
                  | ident "[" expression "]" "else" terminal        (* table lookup *)
                  | "Route"
                  | "continue"                                 (* declared pipeline connection *)
                  | "respond" [ outcome ] [ expression ]
                  | "reject"  outcome [ expression ] ;

pipeline-decl     = [ visibility ] "pipeline" ident
                    ( "entry" [ "match" ] "{" { ident "=>" pipeline-path ";" } "}"
                    | "=" qualified-ident ";" ) ;
pipeline-path     = next-target { "->" next-target } ;
visibility        = "public" | "internal" | "private" ;
(* Each arrow binds a declared continuation; concrete internal edges are not replaced.
   References and assembly/jar packaging live in weft.toml, not nested source projects. *)

(* ---------- ambients ---------- *)

ambient-decl      = "ambient" ident [ ":" type ] ";" ;

(* ---------- tables ---------- *)

table-decl        = "table" ident ":" type "->" "{" next-target { "," next-target } "}"
                    [ "source" ident ] [ "refresh" duration ] ";" ;

(* ---------- switches ---------- *)

flag-decl         = "flag" ident ":" type [ "static" ] [ "default" expression ]
                    [ "source" source ] [ "refresh" duration ] [ "by" ident ]
                    [ "retire" date ] [ "unreachable" "=>" expression ] ";" ;
source            = "Config" | "Db" | "Remote" "(" string ")" ;

switch-scope      = ( "middleware" | "rule" | "trigger" | "receiver" | "service" | "origin" | "cache" )
                    qualified-ident [ ".*" ] ;

canary-decl       = "canary" ident "{"
                      "scope"     switch-scope ";"
                      "baseline"  arm ";"
                      "candidate" arm ";"
                    [ "mode"      ( "split" | "shadow" ) ";" ]
                      "select"    percent [ "by" ident [ "sticky" ] ] ";"
                    [ "judge"     "over" duration ":" judge-expr { "," judge-expr } ";" ]
                    [ "promote"   ( "manual" | "auto" [ "steps" percent { percent } ] ) ";" ]
                    [ "rollback"  ( "manual" | "auto" ) ";" ]
                    "}" ;
arm               = qualified-ident | trigger-effect ;          (* a node/service/transform, or an inline effect *)
judge-expr        = metric ( "<=" | "<" | ">=" | ">" ) expression ;
metric            = "error_rate" | "reject_rate" | "p50" | "p95" | "p99" | "throughput"
                  | "overhead_p50" | "overhead_p99" | "diff_rate" ;

kill-decl         = "kill" ident "{"
                      "scope"  switch-scope ";"
                    [ "killed" "=>" ( expression | "reject" outcome [ expression ] ) ";" ]
                    [ "trip"   "when" judge-expr "over" duration ";" ]
                    [ "reset"  "after" duration [ "," "probe" percent ] ";" ]
                    [ "default" ( "on" | "off" ) ";" ]
                    [ "source" source ] [ "refresh" duration ]
                    [ "unreachable" "=>" ( "last-known" | "kill" ) ";" ]
                    "}" ;

switch-group-decl = "switch" "group" ident "{" ident-list "}" ;

(* ---------- misc ---------- *)

namespace-decl   = "namespace" qualified-ident ( ";" | "{" { declaration } "}" ) ;
extern-decl      = "extern" ( "dotnet" | "jvm" ) extern-binding ;
outcome-decl     = "outcome" ident [ "(" param-list ")" ] ";" ;
type-decl        = "type" ident "=" type ";" ;

cache-decl       = "cache" ident "{"
                   [ scope-clause ]
                   "target" ( "boundary" "service" qualified-ident | "Http.Send" ) ";"
                   { filter-clause } [ "key" expression ";" ] [ "ttl" duration ";" ]
                   "effect" cache-effect { cache-effect } "}" ;
cache-effect     = "invalidate" "on" call { "," call } "by" expression ";"
                 | "ignore" call ";" ;

block            = "{" { statement } "}" ;
block-or-stmts   = block | statement { statement } ;
statement        = ordinary-statement | effect-scope | terminal ";"
                 | "approve" ";" ;
terminal         = "Route" | "respond" [ outcome ] [ expression ]
                 | "reject" outcome [ expression ] ;
effect-scope     = "transaction" block
                 | "deadline" duration block
                 | "retry" "(" expression [ "," "backoff" ":" expression ] ")" block
                 | "saga" "{" { saga-step } "}" ;
saga-step        = "step" expression ( "compensate" expression | "final" ) ";" ;
classification   = "pure" | "idempotent" | "external" ;

(* Ordinary class/record/interface/member/expression forms are completed against the
   ordinary-language contract, not delegated to Roslyn or javac for Weft semantics.
   extern-binding specifies paired host bodies plus one shared Weft contract. *)

outcome           = ident ;
ident-list        = ident { "," ident } ;
qualified-ident   = ident { "." ident } ;
```

## Reserved words (beyond C#'s)

```
origin model service receiver sink transform validate static
rule trigger ruleset suppress middleware pipeline ambient topic channel
scope target filter effect at after require forbid bind call cross into each lineage where when
from via only provides requires exposes config mode carries
on next respond reject entry boundary internal external
singleton scoped transient idempotent external pure
transaction deadline retry saga step compensate
cache key ttl invalidate ignore table any
flag canary kill baseline candidate select judge promote rollback killed trip reset
retire when sticky shadow static unreachable
```

Contextual where possible (`from`, `on`, `after`, `mode`) to keep C# code portable.

## Reconciliations and contextual validity

- Both `pipeline Main entry match { ... }` and the shorter `entry { ... }` retain the
  examples' meaning. `outcomes match` and `next match (subject)` are included explicitly.
- A transform vocabulary signature may omit the input parameter name, as the existing
  examples do. Suppression/use/file scope and declarations inside rulesets are retained.
- `cache` and transaction/deadline/retry/saga blocks are part of the inventory. Member
  classification, transactional service participation, compensation finality, and
  guard/next/after contexts are checked by their owning semantic passes.
- Rule and trigger declarations are allowed in both `.rules` and `.weft`; extension
  does not change precedence or visibility. How an application connects middleware
  from different projects is explicit and user-controlled under decision 0002 section C.
  The complete grammar represents use/extern/outcome forms
  before those passes are executable; their detailed public-contract syntax remains
  a tracked design task for the owning phase.
- Custom address grammar is typed named parameters plus refinements per decision 0002.
  Origin callbacks use generated adapter contracts. Typed topics/channels remain
  separate from HTTP route declarations.
- `byte[]` uses the unsigned 8-bit ordinary type under the accepted C# numeric direction.
  `LengthPrefix<u32>` in origin sketches denotes a wire-format descriptor whose value
  width corresponds to `uint32`; byte order and framing stay explicit adapter concerns.
  The earlier four-number-type restriction is superseded by the
  [numeric contract](contracts/numeric-types.md). Library descriptor names remain
  Phase 2/5 work.
- Rate/statistical predicates belong to switch judges/tripwires, not value filters.
  `guard` paths require approve/reject; `next` paths require a node/terminal; ordinary
  `match` is an expression. Parsing a balanced block cannot establish these proofs.

## Current executable grammar

The parser gives ordinary functions, classes/models/records, fields, constructors, and methods typed
syntax nodes, including property accessors and initializers. Other declared
construct kinds retain their header and balanced token-group body plus the complete
source text. Contextual keywords remain identifier tokens; declaration and block
contexts decide their meaning. The binder emits WF2009 for unimplemented semantic
passes, so balanced sketches never appear to have passed policy checking.

```ebnf
foundation-unit = { namespace-decl | foundation-function | foundation-class | foundation-record | structural-declaration } ;
foundation-class = { modifier } ( "class" | "model" ) ident foundation-type-body ;
foundation-record = { visibility } "record" [ "class" ] ident [ "(" [ parameter { "," parameter } ] ")" ]
                    ( ";" | foundation-type-body [ ";" ] ) ;
foundation-type-body = "{" { foundation-function | foundation-field | foundation-property | constructor | static-constructor } "}" ;
static-constructor = "static" ident "(" ")" ( foundation-block | "=>" expression ";" ) ;
foundation-field = { modifier } type ident [ "=" expression ] ";" ;
foundation-property = { visibility | "required" | "static" } type ident
                      ( "=>" expression ";" | "{" accessor { accessor } "}" [ "=" expression ";" ] ) ;
accessor        = [ visibility ] ( "get" | "set" | "init" ) ( ";" | foundation-block | "=>" expression ";" ) ;
constructor     = { visibility } ident "(" [ parameter { "," parameter } ] ")"
                  [ ":" ( "this" | "base" ) "(" [ argument { "," argument } ] ")" ]
                  ( foundation-block | "=>" expression ";" ) ;
(* The constructor name must match its class. this(...) delegates; base() is root-only for now.
   Static classes allow static fields/properties/methods and a static constructor. *)
foundation-function = { modifier } type ident "(" [ parameter { "," parameter } ] ")"
                      ( foundation-block | "=>" expression ";" | ";" ) ;
parameter       = type ident [ "=" constant-expression ] ;
argument        = [ ident ":" ] expression ;
foundation-block = "{" { foundation-statement } "}" ;
foundation-statement = foundation-block
                     | ( "var" | type ) ident "=" expression ";"
                     | "return" [ expression ] ";"
                     | "if" "(" expression ")" foundation-statement [ "else" foundation-statement ]
                     | "while" "(" expression ")" foundation-statement
                     | "do" foundation-statement "while" "(" expression ")" ";"
                     | "for" "(" [ for-initializer ] ";" [ expression ] ";" [ statement-expressions ] ")" foundation-statement
                     | "break" ";" | "continue" ";" | ";"
                     | expression ";" | structural-effect-scope ;
for-initializer = ( "var" ident "=" expression )
                | ( type ident "=" expression { "," ident "=" expression } )
                | statement-expressions ;
statement-expressions = statement-expression { "," statement-expression } ;
statement-expression = assignment-expression | call-expression | update-expression | new-expression ;
new-expression  = "new" type ( "(" [ argument { "," argument } ] ")" [ object-initializer ] | object-initializer ) ;
object-initializer = "{" [ member-initializer { "," member-initializer } [ "," ] ] "}" ;
member-initializer = ident "=" ( expression | object-initializer ) ;
update-expression = ( "++" | "--" ) expression | expression ( "++" | "--" ) ;
with-expression = expression "with" "{" [ ident "=" expression { "," ident "=" expression } [ "," ] ] "}" ;
expression      = literal | qualified-ident | "this" | new-expression | with-expression | "(" expression ")"
                | expression "." ident
                | ( "!" | "-" | "+" ) expression | update-expression
                | expression binary-op expression
                | expression "?" expression ":" expression
                | expression "(" [ argument { "," argument } ] ")" ;
binary-op       = "=" | "+=" | "-=" | "*=" | "/=" | "%=" | "||" | "&&" | "==" | "!=" | "<" | ">" | "<=" | ">="
                | "+" | "-" | "*" | "/" | "%" ;
```

Precedence from lowest to highest is simple/compound assignment (right associative), conditional (right
associative), OR, AND, equality,
comparison, addition/subtraction, multiplication/division/remainder, with, unary/prefix update,
member/call/postfix update.
Currently executable types are void (return only), bool, int32/int, int64/long, string,
and ordinary class/model/record references. See [models and records](contracts/data-types.md)
for positional initialization, equality, and copying.
Numeric literals are decimal signed integer magnitudes with optional L suffix and
underscores; minimum signed values are accepted through unary negation. The parser
also represents generic/array/nullable type syntax; those forms and interpolation
execution remain required Phase 2 work. This checkpoint is not first-release scope.

Functions, constructors, and static/instance methods support overloads,
public/internal/private checks, named and optional arguments, and int32-to-int64
widening. [Object initialization](contracts/initialization.md) adds ordered assignments,
nested existing objects, required members, and init-only accessors. The
[object contract](contracts/objects.md) covers fields, construction, and
non-null initialization. [Property accessors](contracts/properties.md) preserve ordinary
read/write evaluation and use source-level statement checks. See the
[function contract](contracts/functions.md) and [control-flow contract](contracts/control-flow.md)
for execution, scope, return checks, and current limitations. Project
pipeline syntax is accepted; its graph execution remains Phase 3/6 work.
