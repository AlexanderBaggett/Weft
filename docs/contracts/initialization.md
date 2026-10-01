# Object initializers, init accessors, and required members

This executable Phase 2 checkpoint extends ordinary construction on .NET and JVM.
It follows the accepted C# ordinary-code direction and Weft's non-null reference
contract. See [object initializers](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#128173-object-initializers),
[init accessors](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init),
and [required members](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required).
The runnable [initializers example](../../examples/initializers/Program.weft) demonstrates
an immutable identity with mutable ordinary state.

## Construction syntax and evaluation

```csharp
class Order
{
    public required string Customer { get; init; }
    public int Quantity { get; set; }
}

var order = new Order { Customer = "Ada", Quantity = 3 };
```

Both `new Type(args) { ... }` and `new Type { ... }` are supported. Parentheses may be
omitted only when an initializer is present. Empty initializers and a trailing comma
are allowed. Named/optional constructor arguments keep their normal written-order
behavior. Constructor arguments, allocation, field/property initializers, and the
constructor body run before the object initializer's assignments.

Assignments execute once in source order against a compiler-owned reference. Right-hand
expressions use the enclosing lexical scope; they do not gain an implicit reference
to the new object. `this` still means the enclosing method's receiver. Assigning the
whole creation expression to an existing variable updates that variable only after
all member initializers finish. A failure cannot publish the partially initialized
object through that assignment.

Targets must name accessible fields or properties. A name cannot be repeated in one
initializer, including when one occurrence is nested. Accessor visibility, readonly
fields, type conversions, and missing setters retain the ordinary assignment checks.

A nested member initializer updates an existing reference:

```csharp
var holder = new Holder { Point = { X = 2, Y = 3 } };
```

It does not replace `Point`. Each nested assignment evaluates its full receiver path,
so a `Point` getter runs once for `X` and once for `Y`. An empty nested initializer
performs no getter call. A readonly reference field or getter-only property may expose
such an object for mutation, but its init-only members cannot be reassigned: the child
is already constructed. Nested initializers require a class reference. Collection
and indexer initializer forms join their collection/indexer implementation.

## Init accessors

`init` replaces `set`; a property cannot declare both. Automatic, block-bodied, and
expression-bodied init accessors use the same value/void contract as setters. An
init-only property can be assigned in an object initializer, through the current
`this` in its constructor, or through the current `this` in another init accessor.
Ordinary methods and writes through a different object cannot do so. An initializer's
right-hand expression gets no special permission to modify some other init-only object.

An init accessor may assign readonly fields declared by its own class through `this`.
It does not gain permission to assign getter-only auto-properties or another object's
readonly fields. Getter-only auto-properties remain assignable in their own constructor.
Private/internal accessor restrictions still apply during initialization.

## Required members and non-null construction

`required` may annotate an ordinary field or a property with a set/init accessor.
Every `new` expression must explicitly assign each required member in its object
initializer, even if a field initializer or constructor already assigns it. Nested
mutation of a required reference does not satisfy that assignment. Required members
must be writable and at least as accessible as their class; required readonly fields,
getter-only properties, and insufficiently accessible setters are errors.

A required reference field or auto-property may defer its first non-null assignment
until the object initializer. Other non-null fields and auto-properties must still be
initialized by the constructor. Constructor reads of unassigned required storage and
escapes of `this` remain errors; only the compiler-generated constructor result may
cross into the checked initializer. Assignment to a required value in a constructor
can satisfy its internal read/escape checks, but does not remove the caller's explicit
initializer obligation.

Weft additionally protects incomplete reference storage while initializer assignments
run. Automatic accessors and direct field writes can populate it without executing
arbitrary code. A custom getter/setter/init accessor on the new object may run only
after its pending required reference storage is filled. Reading that storage through
a nested initializer is also rejected. This is conservative: the compiler currently
recognizes declaration initializers and prior object-initializer assignments here,
not constructor/helper-method summaries. Reorder safe automatic assignments before
custom accessor calls. This restriction follows Weft's non-null guarantee rather than
allowing a custom accessor to observe a host null value.

## Representation, diagnostics, and verification

The shared IR uses a sequence with an explicit `Initializing` identity. Its first
binding must construct or [copy](data-types.md) that object and its result must return the same object.
Constructors must first allocate their receiver or obtain it through a validated
[constructor chain](constructors.md), then return only that receiver, never an existing
argument object. Subsequent member assignments execute in order. The
validator checks this structure,
rejects reassignment of that identity, and permits init calls only through it or the
current receiver of a constructor/init accessor. Init functions have explicit markers
and void single-value signatures. Readonly writes retain owner/receiver checks.
Both existing sequence emitters execute this contract without host reflection or
backend-specific source semantics.

WF2025 reports an init write outside construction, WF2026 missing required assignments,
WF2027 invalid required declarations, and WF2028 duplicate/invalid nested initializers.
WF2022 continues to cover incomplete non-null storage. The
[initialization tests](../../tests/Weft.Tests/InitializationTests.cs) include invalid
source and malformed initialization IR; seven executable conformance cases cover order,
required references, custom init bodies, nested/recursively empty getters, enclosing
scope, and safe custom accessor timing on both runtimes.

[Constructor chaining](constructors.md), [ordinary models/records](data-types.md),
and [static initialization](static-members.md) are executable.
Inheritance/interfaces, collection/indexer
initializers, remaining record features, and broader nullable/generic/closure integration remain
required work. Constructor contracts that explicitly satisfy required members instead
of requiring caller assignments are not implemented. No host attribute is accepted
as an unchecked assertion of Weft initialization safety. P02-003 remains open.
