# Open questions

Decisions not yet made, roughly in order of how much they constrain everything else.

## Provenance

1. **Scalar derivations.** Does `req.User.Length` carry `from Http`? Probably not for
   `int`, probably yes for `Substring`. Need a rule: provenance propagates through
   operations that return the same *kind* of data (string→string, bytes→bytes) and
   drops through projections to a different kind. Verify this doesn't create a laundering
   hole (`Encode` → `Decode`).
2. **Containers.** `List<string from Http>` vs `List<string>` — are they distinct types,
   or is element provenance a refinement the compiler tracks separately from the nominal
   type? The latter is friendlier; the former is simpler to implement.
3. **Inference at signatures.** Is provenance inferred across method boundaries within a
   module (whole-program) or must it be annotated at every public signature? Leaning:
   inferred within a module, required at `exposes` contracts.
4. **`via` fidelity.** How deep is the chain kept? Full chains are expensive and rarely
   matched on. Proposal: keep the root origin and the *set* of services crossed, not the
   ordered path.

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
9. **Hot reload.** Is the graph static per build, or can `next` branch on config? If
   config, path proofs must consider all config values — probably restrict branching to
   `msg`.
10. **Per-module pipelines.** How do two modules each declaring `pipeline` merge? Proposal:
    they don't — one `pipeline` per project; modules contribute nodes, not entries.

## Origins

11. **Custom address grammars.** What is the language for `address topic: string, qos: 0..2`?
    Parameter lists with refinement types is probably enough; anything richer becomes
    a parser-generator.
12. **Origins as values.** Can code branch on `msg.Origin` at runtime, or is it
    compile-time only (via `from` refinement)? Leaning: runtime-readable but refinement
    is the preferred style.

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
