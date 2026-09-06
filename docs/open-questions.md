# Open questions

Decisions not yet made, roughly in order of how much they constrain everything else.

## Provenance

1. **Resolved — scalar derivations:** retain provenance until explicit transform,
   including `req.User.Length` and encode/decode flows.
2. **Resolved — containers:** compiler refinements include element provenance,
   separately from the nominal type.
3. **Resolved — signatures:** infer within a project; exposed contracts carry explicit
   provenance/effect summaries. Unknown external/indirect flows need contracts.
4. **Resolved — `via`:** keep roots and crossed-service sets, separate transform lineage,
   and a conservative hop summary if hop predicates are used. See accepted
   [decision 0001](decisions/0001-phase-1-semantics.md).

## Rules

5. **Points that aren't direct calls.** If `Log` is called through an interface or
   delegate, can the compiler still fire `at Log`? Probably: point identity is part of
   the contract type, so `at ILogger.Log` works and a delegate typed `Action<string>`
   does not.
6. **Structural filter matching.** A property pattern with no preceding type filter
   matches any field that has the named members. Is that too clever? Alternative: require
   a type filter first.
7. **Rules on return values.** `target` selects inputs today. Do we want
   `target Order from OrderService` at `Http.Send` — i.e. redaction on the way out?
   Yes, and the current design already allows it; verify the examples read well.

## Middleware

8. **Graph size.** Path enumeration is exponential in branching depth. Real graphs are
   shallow; still need a bound and a diagnostic.
9. **Resolved — configurable routing.** The possible connections are fixed per build;
   routing may read configuration through a message snapshot. Checks cover every
   allowed target and configuration state. See decision 0002 section D.
10. **Resolved — middleware across projects.** The consuming project explicitly selects
    and connects shared nodes or adopts a shared pipeline in a source file. References
    never activate or merge pipelines, even when the API has no middleware of its own.
    See [project pipeline contracts](contracts/project-pipelines.md).

## Origins

11. **Resolved — custom address declarations.** Typed named parameters with range/enum
    refinements; adapters implement the protocol parsing. See decision 0002 section D.
12. **Resolved — origins as values.** `msg.Origin` is runtime-readable; the compiler's
    origin checks determine which envelope members are accessible.

## Services and lifetimes

13. **Lifetime of closures.** A `scoped` service passes a lambda to a `singleton`. The
    lambda captures `this`. Error? Yes — but the diagnostic must be excellent or this
    will be the most-hated check in the language.
14. **`transient` value.** Is `transient` pulling its weight, or is it `scoped` with a
    different name for stateless things?

## Ambients

15. **Ambient assignment outside middleware.** Can a service `provides Tenant`? If yes,
    availability proofs must walk the call graph, not just the middleware graph. Leaning
    yes, with the call-graph walk — the proof machinery needs it anyway for `requires`.

## Effect scopes

16. Everything in [effect-scopes.md](effect-scopes.md). Whether classifications are
    declared or inferred is the big one.

## Caching

17. How to express "deliberately not invalidated by X" without silencing the whole
    stale-cache check. Current answer: per-sink, per-field `ignore`.

## Switches

18. Switch-group size bound. Six is a guess.
19. Are judge metrics a closed set, or can a trigger define one?
20. Shadow canaries on middleware — a shadow node that `provides` something can't
    actually provide it. Probably: shadow is legal only on nodes with no `provides`.
21. Where do switch *values* live for tests? Per-scope overrides in `weft.toml`, like
    `Clock`/`Random` substitution.

## Syntax

22. **`on` vs attributes.** `on Http.Post("/x")` was chosen over `[Http.Post("/x")]`
    because bindings are not metadata — they generate entry stubs. Confirm this holds
    up when a method has four bindings.
23. **Rules files vs inline.** Should `rule` be allowed in `.weft` files? Leaning no:
    rules apply by scope, and finding them is easier when they're in one place.
24. **Name.** "Weft" is a working name.

## Foundation decision records

[Decision 0001](decisions/0001-phase-1-semantics.md) also settles lazy replacement,
precedence/forbids, response preparation/unwind, child joining, and Task combinators.
[Decision 0002](decisions/0002-ordinary-and-portable-contracts.md) records the accepted
C# ordinary-value default, broader numeric support, and explicit middleware connections
between projects. Hosted CI is deferred to Phase 6; it is not an outstanding permission
request. The foundational decisions are resolved. Open details
needed before later feature implementations include the transport-completion hook,
cleanup I/O budget spelling, graph-proof limits, shadow-provider semantics, and effect
classification/transaction/stream-retry policy. They remain in release scope.
