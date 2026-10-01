# Static members and type initialization

Static fields, properties, and constructors execute on .NET and JVM. They extend
P02-003/011/032. This includes ordinary classes, models and records, and static classes.
Exception and task integration still owns the remaining cross-runtime initialization
work described below; the full-release scope is unchanged.

## Storage and access

```weft
class Counter {
    public static int Value;
    public static readonly string Name;
    public static int Next { get; set; } = 100;
    public static string Label { get; } = "counter";
    static Counter() { Name = "shared"; Label = "ready"; }
    public static int Allocate() => Next++;
}
```

Static data has one storage location per declared type in an executing program.
Access it through its type (`Counter.Next`) or unqualified within that type. Selecting
it through an object is an error. Selecting an instance member through a type is also
an error. Ordinary lexical name hiding and member/accessor visibility still apply.
Static classes require their fields, properties, and methods to be static and cannot
be used as values or constructed. Static fields and properties cannot be `required`;
a static property cannot declare an init accessor.

Automatic and custom get/set accessors use the existing property rules. A getter-only
auto-property can be assigned in its declaration or its declaring type's static
constructor. The same authority applies to static readonly fields. An instance
constructor, another type's static constructor, or an ordinary helper does not gain
that authority. A reference stored in a readonly field remains an ordinary reference;
readonly prevents replacing the field, not mutating its referenced object.

Assignments and updates retain ordinary evaluation order. A static property compound
assignment gets the old value before its right side, invokes the setter once, and
returns the supplied value even if the setter modifies its parameter or stored state.
Static field compounds likewise read the old value before right-side effects. Prefix,
postfix, integer wrapping, and string concatenation use the existing contracts.

## Once-only initialization

Each type has a compiler-generated initialization function. Static fields start with
zero/false/null storage. Declaration initializers execute in declaration order, then
the explicit static constructor body runs. A static constructor has only the static
modifier, no parameters, and no `this`/`base` initializer. It cannot be invoked directly
or configured as the program entry. At most one may be declared per type.

For deterministic behavior on both targets, Weft initializes a type on its first active
use: a static field read/write, static property/method call, or instance construction.
Receiver/argument evaluation precedes entry to a method or constructor; a simple
static assignment evaluates its right side before the store triggers initialization.
Compound assignments first read their destination, which can initialize the type
before the right side runs. The initialization body itself is not an ordinary call.
Unused types need not initialize. This selects a consistent point even for types
where C# without an explicit static constructor permits earlier initialization.

Constructing an instance of the same type inside one of its static initializers is
supported. Reentry on the initializing thread does not rerun the initializer. Primitive
fields may expose their zero/false values during a cycle, following ordinary C#
behavior. Static state is excluded from instance construction checks, record equality,
hashing, and copying. Making an instance or a shallow copy does not reset shared state.

The emitters use an explicit native .NET type constructor or Java static initializer
and a method-entry activation call. Native initialization serializes concurrent first
use, publishes completed state, and remembers failure so a failed initializer is not
retried. These rules use [C# static constructors](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-constructors)
and [JLS initialization](https://docs.oracle.com/javase/specs/jls/se21/html/jls-12.html#jls-12.4)
as their platform basis. Native wrapper exception identities and cross-thread cycles
are not yet the completed portable Weft exception/concurrency contract.

## Non-null storage

Every non-null static reference must be populated on every normal return from its
type initializer. Direct reads in that initializer before assignment are diagnosed.
The check includes auto-property backing fields and follows the shared branch/loop/
return analysis. Helper-method/custom-setter initialization summaries are not inferred
at this checkpoint; assignments must be visible to the initializer's analysis.

Indirect reentry can evade a local proof: a field initializer may call a helper that
reads the same field, or activate another type that reads back into it. Every static
reference read therefore checks for uninitialized storage at runtime and fails with
`Static field 'Type.Member' was read before initialization.` rather than exposing a
null value under a non-null type. Auto-property backing fields currently use their
compiler storage names in this message. Successful ordinary reads return the original
reference without copying. This check enforces Weft's non-null contract where C# or
Java can otherwise observe a temporary null during initialization.

## Representation, diagnostics, and evidence

Field/property symbols retain `IsStatic`. A null receiver in field or setter IR denotes
static access; an instance access requires a matching receiver. `IrClass` includes
static storage owners as well as instance types, and registers its `TypeInitializer`.
Initializer functions carry an explicit flag, a void/no-parameter signature, and no
receiver. They are emitted only as native type initialization, never ordinary calls.
The validator checks registration, function owners, receiver shape, readonly authority,
and static types' exclusion from the value type set. Shared traversal still visits
all value expressions and discovers nested calls/temporaries.

WF2032 diagnoses invalid static declarations/constructors. WF2033 diagnoses incomplete
static reference initialization. WF2020 covers incorrect instance/static access;
existing member visibility, readonly, and accessor diagnostics remain applicable.

[StaticBindingTests.cs](../../tests/Weft.Tests/StaticBindingTests.cs) covers invalid
source and malformed IR. Seven `static-*` conformance programs specify first-use order,
fields/properties, construction, recursion/singletons, record/model integration,
updates, and reference flow on both backends. [StaticRuntimeTests.cs](../../tests/Weft.Tests/StaticRuntimeTests.cs)
uses host harnesses around the actual generated code until Weft task/exception syntax
is available. Events/latches hold initialization across concurrent callers without
sleep/timing assumptions; another case verifies indirect reference failure and no
retry. See the runnable [static example](../../examples/statics/Program.weft).

P02-036 remains required for uniform exception wrappers/rethrows and cross-thread
cyclic initialization as exception/task/lock support becomes executable. Inheritance,
generic type initialization, const fields, and analysis of helper writes remain within
their ordinary-language tasks. This checkpoint does not complete P02-003 or Phase 2.
