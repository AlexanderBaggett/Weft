# Constructor chaining

Ordinary constructors can delegate to another constructor in the same class using
`: this(...)`. This executable Phase 2 checkpoint runs on .NET and JVM and follows
the accepted C# ordinary-code direction. See the
[C# constructor specification](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/classes#1511-instance-constructors).

```csharp
class Order
{
    public string Customer { get; }
    public int Quantity { get; }

    public Order(string customer) : this(customer, quantity: 1) { }
    public Order(string customer, int quantity)
    {
        Customer = customer;
        Quantity = quantity;
    }
}
```

The runnable [constructors example](../../examples/constructors/Program.weft) adds a
private constructor, readonly state, and calculated totals.

## Calls, scope, and execution

Constructor selection uses the existing overload, named/optional argument, visibility,
and conversion rules. Initializer arguments evaluate left to right in written order.
They can use and mutate the current constructor's parameters, call static/free
functions, and access other objects. They cannot use the object being constructed:
`this`, its instance members, and implicit instance calls are unavailable.

Every constructor in the chain receives the same object. Only the constructor without
a `this(...)` initializer runs the field and auto-property initializers. It runs its
body next; the delegating bodies then execute from inner to outer. An early `return;`
ends that constructor body and lets its caller continue. Object initializers run after
the selected outer constructor finishes. Readonly fields, getter-only auto-properties,
and init accessors retain their constructor assignment permissions.

The current root class can spell its implicit root initialization as `: base()`.
`base(...)` with arguments still requires the inheritance implementation. A constructor
chain cannot call itself directly or indirectly. WF2029 reports the cycle with source
locations and constructor signatures; WF1105 identifies initializer syntax used on a
non-constructor or with an invalid initializer kind.

## Non-null and required state

The compiler binds chain targets before their callers. Each constructor records the
fields definitely assigned on every normal exit, including explicit early returns.
A delegating constructor begins its body with those facts. This lets its body safely
read a reference initialized by the delegated constructor or make a method call after
all non-null storage is initialized. A field assigned on only some returning paths
cannot establish that guarantee. A non-returning constructor has no normal-exit
obligations; it cannot make a partially initialized value observable to its caller.

`required` retains the caller's explicit object-initializer obligation. Assigning a
required member in a delegated constructor permits safe reads inside the outer body
when every path proves the assignment; it does not remove the required assignment at
`new`. The existing conservative ordering of custom object-initializer accessors is
unchanged. See [object initialization](initialization.md).

## Shared representation and verification

The terminal constructor's factory allocates the host reference object. A delegating
factory initializes its receiver from a call to another constructor factory, then
executes its own body and returns that receiver. Field initializers are emitted only
in terminal factories. Existing named-argument adapters preserve the original
argument ordering on both targets; no extra object or copied state is introduced.

[ConstructorGraph](../../src/Weft.Compiler/Semantics/ConstructorGraph.cs) checks the
single-target delegation graph iteratively and orders its targets before callers.
Both binding and IR validation use it. The IR validator accepts receiver initialization
only from direct allocation or a registered constructor of the same class. Every
chain must terminate without a delegation cycle, every constructor must return its
receiver, and reassignment of that receiver remains invalid. This preserves the fresh
object invariant needed to authorize init-only writes, including after a chain.

[ConstructorChainTests](../../tests/Weft.Tests/ConstructorChainTests.cs) covers invalid
chains, unavailable instance state, initialization summaries, cycle diagnostics, missing
bodies, and malformed IR. Five executable conformance cases independently specify
field/body/argument order, parameter mutation, optional/named calls, object identity,
readonly writes, required/non-null state, and nested object-initializer arguments.

Inheritance/base-argument binding, static initialization, models/records, and broader
object/type integration remain required work in P02-003/004/011 and later phases.
This checkpoint does not complete the ordinary object model.
