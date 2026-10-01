# Models, records, and copies

Ordinary models and records execute on both backends. This extends P02-003/011/032;
provenance-aware model declarations and remaining ordinary type features retain their
original Phase 2/3 tasks. Reference layout, identity/value equality, and shallow copying
follow [decision 0002](../decisions/0002-ordinary-and-portable-contracts.md).

## Models

```weft
model Customer {
    string Name;
    int Orders;
}
var customer = new Customer { Name = "Ada", Orders = 2 };
var updated = customer with { Orders = 3 };
```

A model is a nominal reference type with identity equality. Assignment aliases the
same object. Fields in the compact model form default to public and retain declared
mutability. A reference field without an initializer, unless readonly, implicitly
requires an object-initializer assignment; primitive fields keep their ordinary zero
or false defaults. These engineering rules let the compact model syntax initialize
non-null data without manufacturing null defaults. Explicit visibility, readonly and
required checks still apply. Constructors, methods, properties, and declaration
initializers use the existing ordinary object rules. Explicit properties retain their
ordinary visibility and initialization requirements.

Model `with` is a fieldwise shallow copy with source-ordered updates, as used by the
original order-update design sketch. The copy has a new identity; referenced members
remain shared. Model origin stamping, `from` restrictions, receiver binding, generated
serializers, and provenance preservation remain required Phase 2/3 integration.
`model M from Http { ... }` remains represented and diagnosed as unimplemented; plain
model execution is not evidence that provenance checking has run.

## Records and positional construction

```weft
public record Line(string Name, long Price = 25L, int Quantity = 1) {
    public long Total => Price * Quantity;
    public Line() : this("default") {}
}
var line = new Line(Quantity: 3, Name: "Ada");
var revised = line with { Quantity = 4 };
```

`record R { ... }`, `record R;`, and `record [class] R(parameters)` declare reference
records. Positional parameters generate public get/init properties and a public
constructor, retaining parameter names/defaults and ordinary overload/evaluation rules.
Generated properties initialize before user-declared field/property initializers.
Primary parameters are available in those initializers; `this` and implicit instance
access remain unavailable there. A body member matching a positional parameter must
be a readable field/property of the same type. It replaces the generated property and
must provide its own initialization where required. Other constructors require a `this(...)` initializer, except record copy constructors.
That initializer may target the primary, another ordinary, or a copy constructor.

Record assignment aliases the original record. `==`, `!=`, and the generated typed
`Equals(other)` compare stored data: declared instance fields, including private
fields and automatic-property backing storage. Computed/custom getters do not run.
Strings and nested records compare by value; class/model members compare by identity.
Types must match under the current nominal conversion rules. `GetHashCode()` uses the
same stored members, so equal records have equal hashes. Hash numbers are runtime
implementation details and are not promised to match between .NET and JVM. Mutable
record state can change equality and hashes; it is not frozen by record syntax.

## Copy execution and initialization

A `with` expression evaluates its receiver once, creates the copy, then evaluates
member values and assignments left to right in the enclosing lexical scope. The
original record's own storage is unchanged unless a value expression explicitly
mutates it. Existing reference members stay aliased. To copy a referenced record too,
use `value with { Child = value.Child with { Name = "new" } }`. Nested existing-object
initializer syntax such as `Child = { ... }` is not a `with` member value.

The default copy assigns all stored fields directly. It calls no property accessors,
constructors, or declaration initializers. Readonly/getter-only storage is copied but
cannot be replaced by the initializer. Set/init accessor visibility remains enforced;
init permission belongs only to the copy. Required assignments already satisfied by
the source need not be repeated. Copying does not grant init permission to the source
or a nested shared object. A standalone `with` is not an expression statement.

An explicit record constructor with one parameter of the same record type is a copy
constructor. `with` invokes it even if private. It allocates a fresh receiver, runs its
body without declaration initializers, and must initialize every non-null field on
all normal returns, including required reference storage. It cannot delegate through
`this(...)` or have an optional source parameter. The copy constructor can choose
custom copying behavior. The subsequent `with` assignments still run afterward.
Ordinary `new R(source)` calls obey constructor visibility and required-member rules.
A record without a primary/ordinary constructor still receives a default constructor
when its only explicit constructor is a copy constructor.

These ordinary record rules follow the C# [record reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
and [with reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression),
alongside Weft's stricter non-null guarantees. Model copying is the Weft extension
already illustrated by the order-update sketch.

## Shared representation and evidence

Type symbols retain class/model/record kind. `IrCopy` evaluates an existing value and
creates a new object. `IrClass.CopyConstructor`, when present, identifies the checked
record copy constructor. `IrObjectHash` is the record hash operation. Shared traversal
visits their operands so calls and sequence temporaries remain discoverable. An
initializing `IrSequence` may begin with construction or copying and must yield that
same fresh identity; arbitrary existing objects cannot acquire init-write permission.

Both emitters generate record equality/hash methods and copy helpers from stored-field
metadata, without reflection. Source-level record methods remain ordinary typed calls.
The validator checks data-kind metadata, copy constructor registration/signatures,
copy/hash receiver types, initialization authority, and local identity preservation.

[RecordBindingTests.cs](../../tests/Weft.Tests/RecordBindingTests.cs) covers invalid
source and malformed IR. The `model-values`, `record-positional`, `record-equality`,
`record-copy-order`, `record-primary-initializers`, `record-empty-and-nested-copy`, and
`record-copy-constructor` conformance programs specify expected behavior independently
for both runtimes. The [data example](../../examples/data/Program.weft) is runnable.

Custom equality/hash overrides are diagnosed rather than ignored. Their integration,
source-callable synthesized copy constructors, record display/deconstruction,
inheritance/interfaces, nested/generic/nullable types,
collection equality integration, model provenance, and external project metadata
remain required work. This checkpoint does not complete P02-003 or reduce release scope.
