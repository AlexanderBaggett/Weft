# 0002: Ordinary language and portable contracts

**State:** accepted direction, 2026-09-06: C# ordinary-value behavior wherever possible,
broader numeric support, and explicit middleware connections between projects. Hosted
CI is deferred to Phase 6 at the designer's request. Engineering details and initial
syntax are distinguished below; recording a contract does not mean its Phase 2+
implementation is complete.

## A. Ordinary language

Existing commitments from [compiler.md](../compiler.md): UTF-16 strings, ordinal
comparison by default, unchecked exceptions, no user-defined
value layout, record value equality, no runtime reflection, and compile-time generics.

The designer selected ordinary values and C# evaluation wherever possible. The rules
below apply that direction alongside the existing Weft commitments. New ordinary-code
edge cases use the corresponding C# rule; departures must have a stated Weft reason.

- Evaluate receivers, operands, arguments, and initializers left to right, exactly once.
  `&&`, `||`, conditional expressions, and null coalescing short-circuit. Assignment
  evaluates its destination before its right side. Compiler insertion preserves this
  order by introducing temporaries where necessary.
- Locals and parameters hold values; object values are references. Assignment and
  argument passing copy a reference, never the object. Fields retain declared
  mutability. Class and model equality is identity by default; records compare their
  declared data members by value and use reference layout. Record `with` makes a
  shallow copy. Mutable referenced members remain aliased. Arrays/collections compare
  by identity unless an explicit sequence comparer is requested. String equality
  compares UTF-16 content. Generated hashes must agree with equality, but numeric hash
  values and hash-table iteration order are not portable observations.
- Captured mutable locals refer to the same lifted cell. Captures preserve provenance,
  ambient requirements, and lifetime constraints. Escaping a scoped capture is an error.
  Callable contracts carry parameter/result types and effects; indirect policy point
  identities must be declared or proven, never guessed from a runtime delegate.
- Generic parameters are invariant unless a supported interface/callable variance
  declaration proves safety. Compile-time constraints govern calls. No runtime type
  arguments, `typeof(T)`, or overloads distinguishable only after erasure. Nominal
  type tests remain possible when they do not inspect erased generic arguments.
- Non-null is the default for references; nullable values require an explicit `?`.
  Flow checks refine successful null tests. Arrays and mutable collection element
  contracts remain invariant; writes cannot invalidate a proven element refinement.
- Numeric promotions, literals, conversions, and checked/unchecked behavior follow C#.
  Runtime integer arithmetic is unchecked by default; constant-expression overflow is
  checked by default. Signed minimum divided by -1 throws, including in an unchecked
  context; minimum remainder -1 is zero. Division by zero throws. Division truncates
  toward zero and remainder has the dividend's sign. This supersedes the earlier
  proposal to wrap minimum divided by -1. The complete numeric inventory and backend
  mappings are in the [numeric contract](../contracts/numeric-types.md). Full promotion,
  constant evaluation, and checked syntax remain Phase 2 work.
- Exceptions are unchecked and preserve the original throw origin. `finally` runs on
  return, rejection, failure, and cancellation; accepted message-scope cleanup rules
  govern multiple failures. Host wrapper exceptions are unwrapped at runtime edges.
- Visibility is checked before lowering. Projects expose explicit contracts; internal
  inference must not silently widen those contracts. Namespaces qualify names and do
  not themselves create runtime objects or lifetime scopes.

## B. Numeric support, decimal128, and Money

**Designer direction:** support the common C# and Java numeric types, using runtime
support where a platform has no direct counterpart. This replaces the earlier
four-type restriction and prohibition on unsigned integers.

Weft `decimal` follows C# `decimal`. Java `BigDecimal` is the implementation building
block, with a Weft wrapper enforcing the C# contract; it is not a drop-in semantic
equivalent. Keep `decimal`, arbitrary-precision `BigDecimal`, and the previously
planned `decimal128` distinct. See the [numeric contract](../contracts/numeric-types.md)
for the complete mapping and acceptance requirements.

Existing commitment: `decimal128` has specified precision/rounding on both platforms;
`Money` combines it with a currency and uses half-even rounding. The native C# decimal
type cannot represent this extended type's full range and precision.

Engineering selection for the existing extended type: retain finite decimal arithmetic
with **34 significant decimal digits**,
half-even rounding after each arithmetic operation, and an explicitly specified
decimal128 exponent range. A finite value is `(-1)^sign × coefficient × 10^q`, with
coefficient 0 through `10^34 - 1` and q from -6176 through 6111. The smallest positive
subnormal is `1e-6176`, smallest normal is `1e-6143`, and largest finite magnitude is
`(10^34 - 1) × 10^6111`. These bounds follow the
[decimal encoding specification](https://speleotrove.com/decimal/dbspec.html).
Division by zero and overflow throw portable arithmetic
errors. Underflow rounds into subnormal values or zero. No user-visible NaN or infinity;
zero has one observable sign. Arithmetic equality ignores representational trailing
zeros. Parsing, formatting, hashing, conversions, quantization, and wire serialization
must use the same canonical contract. See the independent
[numeric vectors](../acceptance/decimal-vectors.json) before Phase 2 implementation.
Canonical textual values remove insignificant coefficient zeros, use one digit before
the decimal point with a signed base-10 exponent, and serialize zero as `0`; this wire
form is distinct from optional user-facing culture/formatting APIs.

Java's MathContext.DECIMAL128 supplies precision and rounding, not this entire range,
special-value, or representation contract. Both runtime implementations must enforce
the additional Weft rules. [Java MathContext](https://docs.oracle.com/en/java/javase/21/docs/api/java.base/java/math/MathContext.html)

`Money` rejects mixed-currency arithmetic unless an explicit conversion is supplied.
Currency quantization is explicit; intermediate arithmetic retains decimal128 precision.
Parsing and serialization never depend on process culture. Both backends use the
portable runtime type, including the .NET backend.

This applies the finite error behavior familiar from C# decimal to the previously
planned extended precision. It is an implementation selection under the designer's
direction, not a claim that the designer separately selected an IEEE profile.
`decimal128` is not advertised as the complete IEEE interchange format or special-value
API. Any later revision remains a tracked design change with corresponding vectors.

## C. Sharing middleware between projects — accepted

The designer clarified that this is about separately built projects and assembly/DLL
boundaries. Use “project” in developer-facing language. A project can contain only
shared middleware pipelines; an API project can supply additional middleware.

The consuming developer must explicitly select where and how to combine the nodes.
Even when it has no local middleware, the application needs a file explicitly saying
to use the other project's pipeline. Referencing a project never activates middleware
or merges origin entries. Library projects may export multiple pipeline alternatives;
the application explicitly chooses its active pipeline and connections.

The [project pipeline contract](../contracts/project-pipelines.md) supplies initial
syntax: `use middleware` selects named nodes, `->` connects declared `continue` exits,
`use pipeline` selects a shared pipeline, and `pipeline Main = Common;` explicitly
adopts a complete shared pipeline. Existing internal connections are never silently
rewritten. The contract covers DLL/jar metadata, visibility, origin selection, partial
reuse, and checks over both local and shared paths. The designer subsequently accepted
this syntax on 2026-09-06 (“Syntax looks good, proceed”). Further syntax changes follow
normal language-design iteration.

## D. Separate existing requirements and engineering resolutions

These issues were previously bundled under “composition.” They do not require the
designer to decide middleware connections in order to proceed:

- Permit rules and triggers in both `.rules` and `.weft` files. The manifest discovers
  both. `.rules` is the organizational convention, while explicit/file-default scope
  controls application. File extension never changes precedence or visibility.
- Dynamic routing is allowed, with a statically closed admissible node set and explicit
  fallback. Proofs cover all admissible paths and switch states. Mutable configuration
  is read through a per-message snapshot where it affects a routing decision.
- Custom address declarations use typed named parameters with range/enum refinements;
  they do not embed a general parser generator. `msg.Origin` is runtime-readable, while
  compile-time origin refinement determines which envelope fields are accessible.
- A portable external binding supplies both platform bodies and one Weft signature,
  including nullability, provenance, effects, cancellation, and ownership. Import/export
  stubs check non-null results/arguments and translate exceptions. An opaque platform
  handle cannot enter user-visible IR or evade escape checks. Missing counterpart,
  erased-signature collision, or absent required metadata is a compile error.

## Engineering selections

.NET SDK 10 / net10.0 and Java source release 21 are the foundation baseline. Local
verification has exercised JDK 21 and the development JDK 26. Preview JVM features are
not required. Hosted CI moves from P01-021 to P06-030 at the designer's request on
2026-09-06. Keep local verification focused on real compiler/runtime behavior; defer
workflow maintenance and release gates until the integration phase.
The bootstrap manifest's `[source-groups]` entries discover local source files. The
earlier `[modules]` name is removed to avoid implying separate assembly support.
Full project references, public contracts, bindings, and package resolution remain
required Phase 2/3/6 work. Source namespace declarations do not create another project.

The accepted [Task and lifecycle contract](0001-phase-1-semantics.md) already governs
both backends. It does not depend on selecting a host structured-concurrency API.
