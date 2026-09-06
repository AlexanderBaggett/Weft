# 0001: Phase 1 semantic choices

**State:** accepted by the designer on 2026-09-05. The designer accepted conservative
propagation, replacement and precedence, and lifecycle and Task semantics in A–C.

## A. Provenance and lineage

Accepted: provenance is a compiler refinement over nominal types, including container
element refinements. Assigning or aliasing a value never silently removes its origins.
All data derivations, including scalar projections, retain source provenance unless an
explicit transform discharges it. Thus `req.User.Length` retains Http; encoding then
decoding cannot launder a tag by changing nominal types.

Keep root origins and the set of crossed services per root. Record transform identities
separately for lineage queries. If hop-count predicates remain, track a conservative
hop-count summary rather than claiming that a service set preserves path length.
Inference works within a project; exposed contracts carry explicit provenance/effect
summaries. Unknown external and indirect flows need a contract instead of assumed safety.

This resolves the open scalar/container/signature/via questions without introducing a
runtime provenance object. Implicit scalar discharge is not part of this contract.

## B. Rule replacement and precedence

Accepted: a replacement at a call site changes that argument use only. With
`effect at Log => Sanitize`, `Log(req.User)` receives the transformed result, while a
later use of `req.User` retains its original value and provenance. Eager bind effects
replace the value before the receiver sees it. An explicit `var clean = Sanitize(x)`
produces a separately discharged value. Two distinct unsanitized uses can each require
transformation; the same already-discharged value does not.

At one value/point, use the documented narrower-scope, narrower-target, higher-priority
precedence. A more specific matching rule replaces a broader matching rule at that point;
tied rules run in stable declaration order with a diagnostic. Matching prohibitions
are always enforced, including when a less specific prohibition overlaps a transform.
Explicit/eager transforms that already changed the incoming provenance affect matching.
Runtime filters choose which rule matches on that execution; they do not erase the
fallback behavior when a narrower filter is false. Scope lists union structural scopes
and intersect flag predicates with that structural selection.

Generated calls participate in policy and ambient/effect checks. Insertion records
already-applied rule/site identities and checks for recursive policy expansion; it must
report a cycle instead of omitting a required rule or expanding forever.

Broader validators/observers do not compose implicitly when a more specific rule wins.
Matching prohibitions remain enforced as described above.

## C. Message lifecycle and structured tasks

Accepted: select the receiver/binding before receiver-scoped middleware, execute its
graph, obtain a response description, run eligible `after` blocks in reverse path order,
then commit the response through the origin adapter. A filter bypass or guard rejection
does not register that node's `after`; entering its effect does. Registered cleanup runs
on success, rejection, failure, and cancellation. Define response-independent cleanup
behavior when no successful response exists. Cleanup failures preserve the original
failure with associated cleanup failures instead of discarding either.

A message scope owns spawned tasks. Normal completion joins them; failure/deadline/abort
requests cancellation then joins them. Services are disposed only after owned tasks
have stopped. Cancellation is cooperative; an uncooperative external operation keeps
its resources owned until completion and produces an operational diagnostic. Never
dispose a service while an owned task can still access it.

Accepted task combinators preserve the familiar Task contracts on both backends:
`WhenAll` joins all inputs and reports failure after all terminate; `WhenAny` returns
the first completed input without canceling the rest, which remain owned by the message
scope. Scope failure is responsible for sibling cancellation. Completion ties have no
specified winner; deterministic tests control completion ordering when it matters.

Streaming responses commit headers after response preparation/unwind and retain their
stream/connection scope until completion or abort. Post-commit failure cannot replace
the response with a fresh status; the adapter closes/aborts using its protocol contract.
Observation of actual transport completion is separate from the response-preparation
meaning of `after` and needs a named language/library hook before implementation.

## Independent engineering work

Source spans, diagnostics, full declaration representation, shared symbol/type/IR
infrastructure, ordinary executable programs, both source emitters, manifest handling,
runtime version checks, and the conformance runner do not depend on acceptance of A–C.
Their completion is recorded separately in the Phase 1 tracker.
