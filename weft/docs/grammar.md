# Grammar sketch

EBNF-ish, declarations only. Expressions, statements, and types follow C# closely and
are omitted. `?` optional, `*` zero or more, `+` one or more, `|` alternation.

```ebnf
compilation-unit  = { declaration } ;

declaration       = origin-decl | model-decl | service-decl | receiver-decl
                  | sink-decl | transform-decl | validate-decl
                  | rule-decl | trigger-decl | ruleset-decl | suppress-decl
                  | middleware-decl | pipeline-decl
                  | ambient-decl | address-decl | table-decl
                  | flag-decl | canary-decl | kill-decl | switch-group-decl
                  | scope-directive | use-directive
                  | class-decl | function-decl ;               (* ordinary code *)

(* ---------- provenance ---------- *)

provenance        = "from" origin-pattern ;
origin-pattern    = origin-term { "|" origin-term } [ "only" ] ;
origin-term       = ident { "via" ident } | "*" ;
typed             = type [ provenance ] ;                       (* e.g. string from Http *)

(* ---------- origins ---------- *)

origin-decl       = "origin" ident [ ":" "adapter" ident ] ( ";" | origin-body ) ;
origin-body       = "{" { origin-member } "}" ;
origin-member     = "address"  address-grammar ";"
                  | "envelope" "{" { field } "}"
                  | "outcomes" "{" { outcome-map } "}"
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

sink-decl         = "sink"      qualified-ident "(" param-list ")" ";" ;
transform-decl    = "transform" ident "(" typed ident ")" "->" type ";" ;
validate-decl     = "validate"  ident "(" param-list ")" ";" ;

(* ---------- the four-part shape ---------- *)

scope-clause      = "scope" scope-expr ";" ;
scope-expr        = scope-term { "," scope-term } ;
scope-term        = "project"
                  | "module"    ident
                  | "namespace" qualified-ident
                  | "type"      qualified-ident
                  | "method"    qualified-ident
                  | "receiver"  qualified-ident [ ".*" ]
                  | [ "!" ] "flag" ident ;
scope-directive   = scope-clause ;                               (* file-level default *)

filter-clause     = "filter" expression ";" ;
priority-clause   = "priority" integer ";" ;

(* ---------- rules ---------- *)

rule-decl         = "rule" ident "{" [ scope-clause ] rule-target [ filter-clause ]
                    [ priority-clause ] "effect" rule-effect { rule-effect } "}" ;
rule-target       = "target" typed [ "in" "{" ident-list "}" ] ";"
                  | "target" qualified-ident [ provenance ] ";" ;           (* Model.Field *)
rule-effect       = "before"  qualified-ident "=>" ident ";"
                  | "after"   qualified-ident "=>" ident ";"
                  | "require" call "=>" ( "reject" outcome | "throw" type ) ";"
                  | "forbid"  qualified-ident ";"
                  | "replace" qualified-ident "=>" ident ";" ;

ruleset-decl      = "ruleset" ident "{" { rule-decl | trigger-decl } "}" ;
suppress-decl     = "suppress" [ "ruleset" ] ident "in" scope-expr ";" ;
use-directive     = "use" "ruleset" ident ";" ;

(* ---------- triggers ---------- *)

trigger-decl      = "trigger" ident "{" [ scope-clause ] trigger-target [ filter-clause ]
                    [ priority-clause ] "effect" trigger-effect { trigger-effect } "}" ;
trigger-target    = "target" "boundary" boundary-kind [ boundary-mod ] [ qualified-ident ] ";" ;
boundary-kind     = "method" | "service" | "receiver" | "transform" ;
boundary-mod      = "internal" | "external" ;
trigger-effect    = "on" ( "enter" | "exit" | "throw" ) "=>" ( expression | block ) ";" ;

(* ---------- middleware ---------- *)

middleware-decl   = "middleware" ident [ generic-params ] { mw-clause }
                    "{" [ scope-clause ] [ filter-clause ] [ "effect" block-or-stmts ]
                        [ "after" block-or-stmts ] next-clause "}" ;
mw-clause         = "from"     ident-list
                  | "provides" ident-list
                  | "requires" ident-list
                  | "reentrant" "(" "max" ":" integer ")" ;
next-clause       = "next" ( next-target ";" | "{" { expression "=>" next-target ";" } [ "else" "=>" next-target ";" ] "}" ) ;
next-target       = ident                                        (* another node *)
                  | "any" expression "else" terminal
                  | ident "[" expression "]" "else" terminal        (* table lookup *)
                  | "Route"
                  | "respond" [ outcome ] [ expression ]
                  | "reject"  outcome [ expression ] ;

pipeline-decl     = "pipeline" ident "entry" "{" { ident "=>" ident ";" } "}" ;

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

outcome           = ident ;
ident-list        = ident { "," ident } ;
qualified-ident   = ident { "." ident } ;
```

## Reserved words (beyond C#'s)

```
origin model service receiver sink transform validate
rule trigger ruleset suppress middleware pipeline ambient topic channel
scope target filter effect before after require forbid replace
from via only provides requires exposes config mode carries
on next respond reject entry boundary internal external
singleton scoped transient idempotent external pure
transaction deadline retry saga step compensate
cache key ttl invalidate ignore table any
flag canary kill baseline candidate select judge promote rollback killed trip reset
retire when sticky shadow static unreachable
```

Contextual where possible (`from`, `on`, `after`, `mode`) to keep C# code portable.
