# Properties and accessors

Instance properties now execute on both .NET and JVM. They follow the accepted C#
ordinary-code direction: [C# property specification](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/classes#157-properties).
This checkpoint extends P02-003/011/032; it does not complete the object model.
See the runnable [properties example](../../examples/properties/Program.weft).

## Supported forms

```csharp
class Inventory
{
    public string Sku { get; }
    public int Stock { get; private set; } = 5;
    private int requested;

    public int Requested
    {
        get => requested;
        set { requested = value < 0 ? 0 : value; }
    }

    public string Description => Sku + ":" + Stock;
    public Inventory(string sku) { Sku = sku; }
}
```

A property may have a getter, a setter, or both. [Init accessors](initialization.md)
provide construction-only assignment as an alternative to a setter. Accessor bodies support ordinary
statements, including early returns. A getter returns the property type; a setter
returns void and has an implicit parameter named `value` of that type. Both accessors
can have expression bodies. A property expression body is a getter. A custom setter
without a getter is supported for writes; reading or updating it is an error.

Auto-properties use semicolon accessors and compiler-owned storage. They require a
getter and may also have a setter. A getter-only auto-property can be assigned in its
own constructor or initializer. Other getter-only properties cannot be assigned.
Mixing an automatic accessor with a custom body is not yet supported; both bodies
must be automatic or both explicit. C# field-backed custom accessor syntax remains
required follow-on work if included in the ordinary-language specification.

Properties default to private and support public/internal/private visibility. One
accessor of a get/set property can specify a more restrictive visibility, such as
`private set`. Access is checked separately for reads and writes. A compound update
requires both accessors. Duplicate property names, field/method conflicts, and invalid
accessor declarations are diagnosed before emission. Property names hide outer
functions and builtin helpers just as field names do.

A property can have any currently executable value type, including class references.
A public property of a public class cannot expose an internal class. Symbols retain
the declaring type, visibility, accessor functions, and optional backing field for
later compiler analysis. These are compile-time descriptions, not runtime reflection.

## Initialization

Auto-property initializers run in declaration order alongside field initializers,
before the constructor body. Initializers can use already constructed objects, but
cannot access the new object's `this`, fields, properties, or instance methods.

Non-null string/class auto-properties must be assigned on every completing constructor
path, just like [non-null fields](objects.md), unless `required` explicitly defers the
assignment to a checked [object initializer](initialization.md). Reading one before assignment or letting
`this` escape early is an error. Constructor accesses to this object's auto-properties
use their backing storage directly. Custom accessors are ordinary method calls: they
cannot be used to bypass the existing initialization/escape check. Inferring field
initialization through helper methods or custom setters remains flow-analysis work.

## Evaluation and assignment results

For a read, the receiver evaluates once and then the getter executes. For simple
assignment, the receiver evaluates first, followed by the right-hand value and the
setter. There is no getter call. Compound assignment evaluates the receiver, getter,
right-hand side, then setter, once each in that order. The original receiver and old
value remain captured even if the right-hand side changes the variable holding the
object or changes the property itself.

The assignment expression returns the supplied value. It does not read the property
back after the setter: `Print(item.Requested = -3)` prints `-3` even if the setter
stores zero. Reassigning the setter's `value` parameter does not change that result.
Prefix update returns the computed new value; postfix update returns the old getter
value. Neither performs an extra read. Integer updates preserve the existing unchecked
arithmetic and portable division/remainder behavior; string `+=` preserves old text
before its right-hand effects. Short-circuit and conditional branches still skip
unselected accesses.

A property read alone is not a legal statement or for-loop iterator. This is checked
against source syntax even though a getter lowers to a function call. Calls,
construction, assignment, and increment/decrement remain valid statement expressions.

## Shared lowering and evidence

Getters and setters lower to ordinary functions with explicit receivers. `IrSetterCall`
calls a void setter and yields its argument value. Both backends emit a typed static
helper that retains that value while the setter executes, without a closure or an
argument array. Shared IR sequences capture receivers and old/new values where needed.
Setter signatures, receivers, values, identities, and nested expressions are validated
before either emitter runs. Generated helpers carry property source locations.

[PropertyBindingTests.cs](../../tests/Weft.Tests/PropertyBindingTests.cs) checks invalid
reads/writes/accessibility, construction, public types, name hiding, accessor forms,
statement expressions, metadata, and malformed setter IR. The five `property-*`
conformance programs specify outputs independently for each runtime, covering
initialization order, aliases, clamping setters, early returns, nested updates,
receiver reassignment, named arguments, loops, short-circuiting, and signed limits.

WF2023 identifies a missing required accessor; WF2024 identifies invalid accessor
structure or accessibility. Existing WF2011, WF2019, and WF2022 cover access, public
contracts, and non-null construction. [Object initializers and init accessors](initialization.md)
are now executable. Static properties/type initialization, indexers, inheritance/
interfaces, and complete generic/nullable types remain required work under their
original tasks. Unsupported static properties currently receive WF2009.
