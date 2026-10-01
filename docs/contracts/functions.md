# Functions and methods

This is the first executable Phase 2 extension, under the accepted C# ordinary-code
direction. It runs on .NET and JVM. The [functions example](../../examples/functions/Program.weft)
uses a helper class in another source file of the same project; separately built
project references remain tracked work.

## Declarations and visibility

Top-level Weft functions belong to their namespace and default to `internal`; `public`
makes the intended project API explicit. Static classes can contain static methods,
which default to `private` as in C#. Calls can use another namespace's qualified name
or a method in the current class. A private method is accessible only inside its
declaring class. Project-level `private`, conflicting modifiers, and instance methods
inside static classes are errors.

All function signatures are collected before bodies are checked, so forward calls,
recursion, and calls between source files work. Local variables and instance fields
shadow callable/type names. Once a qualified name resolves to a nearer type or namespace, a missing member
does not cause lookup to continue through an unrelated outer declaration.

Method symbols retain visibility, declaring type, parameter names/types/defaults,
return type, and declaration location. This is shared compiler metadata; serialized
contracts and external assembly exports are later project-packaging work.

## Arguments and overloads

```csharp
public static class OrderMath
{
    public static long Total(int units, long unitPrice, long shipping = 0)
        => units * unitPrice + shipping;
}

// unitPrice is evaluated first; shipping receives its declared default.
OrderMath.Total(unitPrice: Price(), units: Count());
```

Named arguments evaluate once, left to right in written order. An in-position named
argument can precede positional arguments. An out-of-position named argument cannot.
Missing required arguments, unknown names, and duplicate assignments to one parameter
are diagnosed before code generation. [C# named and optional arguments](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments)

Overloads differ by parameter types or count. Parameter names, return types, and defaults
do not create distinct signatures. Identity conversions beat int-to-long widening; a
candidate must be no worse for every supplied argument and better for at least one.
For otherwise equal candidates, one that needs no omitted defaults beats one that does.
Unresolved ties are errors with both declarations identified, never source-order choices.
Defaults do not participate in ranking the supplied arguments.
[C# overload resolution](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#1264-overload-resolution)

The compiler currently executes `bool`, `int`/`int32`, `long`/`int64`, and `string`
parameters/results, ordinary class references, and void returns. Int-to-long widening also works in assignments,
initializers, returns, and mixed integer expressions. Other conversions remain in
P02-004/008. Optional defaults currently support bool, int32, int64, and string literals
and constant expressions, with checked integer arithmetic; class references, overflow,
zero division, and function calls are invalid defaults. Required parameters must precede optional ones.

## Shared code generation and checks

Calls retain argument evaluation order and an explicit mapping to parameter positions.
Both backends emit small static adapters when reordering is necessary: arguments are
evaluated at the call site, then their values are passed in parameter order. This does
not allocate an argument array or capture locals in a closure. Defaults retain the
parameter declaration and originating call in their source-origin chain.

The IR validator checks signatures, conversions, default values, and complete argument
mappings. The configured entry selects a parameterless void/int function or static
method even if other overloads were declared first. Local conformance programs check
observable traces and results on each backend.

Ordinary classes, fields, constructors, and instance methods are now executable; see
the [object contract](objects.md) for reference semantics, initialization, and receiver
evaluation. [Property accessors](properties.md) and [models/records](data-types.md) use these method rules.
Generic methods, imports, separate-project
references, and complete numeric/nullable conversions remain
required work in P02-001/003/004/006/008 and Phase 6. P02-001 remains open until its
remaining declaration and public-contract work is complete.
