# Ordinary classes and construction

This executable Phase 2 checkpoint implements ordinary classes, instance fields,
constructors, and instance methods on .NET and JVM. It follows the reference/aliasing
and C# evaluation direction accepted in [decision 0002](../decisions/0002-ordinary-and-portable-contracts.md).
The [classes example](../../examples/classes/Program.weft) runs on both targets.
[Instance properties](properties.md) are also executable. Models, records, and the other
object features listed below remain required
first-release work in P02-001/003/004/006/011.

## Declarations and values

```csharp
public class Order
{
    public readonly string Customer;
    private long subtotal;

    public Order(string customer, long subtotal = 0)
    {
        Customer = customer;
        this.subtotal = subtotal;
    }

    public long Add(int units, long unitPrice)
    {
        subtotal += units * unitPrice;
        return subtotal;
    }
}
```

A class defaults to `internal`. Fields, methods, and explicit constructors default to
`private`; `public` and `internal` are also supported. A class with no declared
constructor receives a public parameterless constructor. Static methods may belong
to an ordinary class or a static class. An ordinary instance method requires an object;
a static method must be selected through the class or called unqualified within it.
Static classes cannot be instantiated or used as value types.

All class names are available before members and bodies are bound, including across
source files. Class values can be fields, locals, parameters, and results. Assignment
and argument passing copy a reference: mutation through an alias affects the same
object. `==` and `!=` compare object identity. Different classes are not implicitly
convertible, and inheritance conversions are not implemented yet.

Private members are accessible within their declaring class. Locals and parameters
hide fields; `this.Name` explicitly selects a field. Fields hide outer functions and
the fallback `Print`/`Log` helpers, so trying to call a non-callable field is an error.
Two fields cannot share a name, and a field and method cannot share a name. A public
function, or public member of a public class, cannot expose an internal class in its
parameter, result, or field types. Separate-project exports remain Phase 6 work.

## Construction and non-null fields

`new Order(customer: "Ada", subtotal: 50)` uses the ordinary overload, named-argument,
optional-argument, and conversion rules. Supplied arguments evaluate once in their
written order, before allocation and field initialization. Each object then runs its
field initializers in declaration order, followed by the selected constructor body.
Initializers run once for each construction. They can call static/free functions and
access already constructed objects, but cannot read `this`, this object's instance
fields/methods, or constructor parameters.

Uninitialized integer and bool fields start at zero and false. String and class fields
are non-null: every normally completing constructor must assign them, either in a
field initializer or its body, unless the member is explicitly `required` and assigned
by the caller's checked [object initializer](initialization.md). Reading such a field before assignment is rejected.
The compiler also requires every non-null field to be assigned before `this` is passed,
returned, stored in a local, or used to call an instance method. Accessing an individual
field through `this` remains possible while construction is in progress.

Initialization checks merge branches and short-circuit paths. A loop that may execute
zero times cannot establish an assignment made only in its body. A do-loop accounts
for paths through break and continue. The checks are deliberately conservative; they
do not infer an assignment from a helper method or arbitrary constant branch predicate.
These restrictions prevent a partly initialized object from escaping while nullability
and broader flow analysis are developed. The current diagnostic is WF2022.

A constructor may use `return;` after satisfying initialization, but cannot return an
expression. A `readonly` field can be assigned by its initializer and through the
current `this` in its own constructor or an init accessor declared by that class.
Writes through other objects or ordinary methods fail
with WF2021. Fields are otherwise mutable.

## Calls and mutation order

An instance call evaluates its receiver once, then arguments left to right in written
order. Reordering named arguments changes their destination parameter, not evaluation
order. This applies to private calls, overloads, and calls on newly constructed objects.

Field assignment evaluates the target object before the right-hand side. Compound
assignment also reads the old field value before evaluating that side. For example,
`GetOrder().Total += ChangeTotal()` calls `GetOrder` once and retains its original
object and old field value even if `ChangeTotal` changes them. Prefix/postfix field
updates preserve their usual new-value/old-value result. Arithmetic uses the same
portable overflow, division, and remainder behavior as local updates; string `+=`
uses the same string concatenation. See [control flow and updates](control-flow.md).

## Representation and verification

Shared IR records class/field identities and explicit method receivers. Each constructor
lowers to a factory function that allocates the object, runs initialization, and returns
it. Both backends emit a host reference object and direct calls with an explicit receiver;
this checkpoint introduces no dynamic dispatch or reflection.

`IrFieldRead`, `IrFieldWrite`, and `IrFieldUpdate` retain the field and receiver.
`IrSequence` introduces scoped temporary bindings that evaluate before its result;
it preserves the target and old value during compound field assignment. It must contain
at least one binding. Both emitters use typed static helpers, with no closure or runtime
argument array. The [IR validator](../../src/Weft.Compiler/IR/IrValidator.cs) checks
registered class/field identities, owners, receiver signatures, writes, and temporary
scope before emission. Constructor initialization is checked during source binding.

[ClassBindingTests.cs](../../tests/Weft.Tests/ClassBindingTests.cs) covers invalid
access, modifiers, construction, reference initialization, public contracts, name hiding,
and malformed IR. The five `class-*` executable conformance cases independently specify
construction order, aliasing/identity, method dispatch, field evaluation, and reference
initialization on both runtimes. The `void-return` case protects early and final bare
returns while traversing function bodies for generated helpers.

## Remaining object work

[Object/nested initializers, required members, and init accessors](initialization.md)
are executable. Constructor chaining, models, records and
record value equality/copying, inheritance/interfaces, nested and generic types,
nullable references, static fields/properties and type initialization,
indexers, and callable members remain required work. See [properties](properties.md)
for currently executable accessor forms.
Current emitted classes are an implementation detail, not a separately consumable
assembly/JAR API; public packaging and metadata are tracked in Phase 6. This checkpoint
does not mark P02-003 or the full ordinary language complete.
