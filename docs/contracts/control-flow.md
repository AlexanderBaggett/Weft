# Ordinary control flow

This Phase 2 checkpoint implements `if`/`else`, `while`, `do`/`while`, `for`,
`break`, `continue`, empty statements, and conditional expressions on .NET and JVM.
The execution order follows the accepted C# direction. Full matching, exception
cleanup, collection iteration, and remaining expression forms stay in P02-002/006/007.

## Loops and scopes

```csharp
for (var i = 0; i < 5; i = i + 1)
{
    if (i == 1) continue;
    if (i == 4) break;
    Print(i);
}
```

The initializer runs once. Before each iteration the condition is tested. After the
body finishes or executes `continue`, iterators run left to right, then the condition
is tested again. `break` exits without evaluating the iterators or condition again.
An omitted condition is true. Initializer and iterator expression lists may contain
calls and assignments. Typed initializers may declare multiple initialized variables;
`var` declares one variable. A loop's initializer locals are visible to its condition,
body, and iterators, but not after the loop. Body locals do not leak into its header.

`while` tests before the body; `do` runs the body at least once and tests afterward.
`continue` in a `do` loop goes to its condition. `break` and `continue` target the
nearest enclosing loop. A jump outside a loop is WF2016. A bare local declaration
used as a branch or loop body requires braces (WF2017), matching C# embedded statements.
Empty statements allow `while (Test()) ;` and `for (...) ;`.

See the [C# statement specification](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/statements#139-iteration-statements)
for iteration order and declaration scopes. Increment/decrement, compound assignment,
uninitialized declarations, and `foreach` remain tracked ordinary-language work;
use `i = i + 1` in current executable programs.

## Conditional expressions

`condition ? whenTrue : whenFalse` evaluates the condition once, then exactly one arm.
The condition must be bool and both arms must produce compatible values. Current
value types use identity conversion or int32-to-int64 widening. Conditional expressions
associate to the right, below `||` and above assignment. Named calls nested in an arm
keep written argument order. Optional parameter defaults can use conditional constant
expressions; all three operands must be valid constants, including the unselected arm.

See the [C# conditional operator](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/conditional-operator).
Nullable, generic, reference, and target-typed conditional conversions join the full
conversion work in P02-004/006/008.

## Shared flow checks and backend behavior

[ControlFlow.cs](../../src/Weft.Compiler/IR/ControlFlow.cs) summarizes normal completion,
return, break, and continue. A nested loop consumes its own breaks and continues.
The binder and IR validator use the same summary, so a branch ending in break on one
arm and continue on the other cannot accidentally allow following statements.
A value-returning function cannot reach its end without a return (WF2007). Statements
after a guaranteed exit are diagnosed as WF2010, preserving Weft's existing diagnostic
policy. A literal-true or conditionless loop with no reachable break has no normal exit.
An unconditional return from a `do` body also satisfies the function return check.

Current flow analysis is conservative for constant expressions other than literal
loop conditions; it does not yet fold branch predicates or arbitrary constant
expressions to prove missing returns. Full constant reachability remains P02-002/008.

Both backends emit native loops, preserving continue behavior without rewriting it
into a jump past the iterator. A surrounding generated block scopes for-initializer
locals. Iterators that cannot be reached because the body always returns or breaks
are still bound and validated but omitted from emitted code. Java's different
constant-false reachability rules are handled in the backend; they do not reject a
valid Weft loop body. Named-call discovery traverses every header, body, and conditional
arm. Generated nodes retain Weft source locations.

The executable conformance cases `for-loop-order`, `nested-loop-exits`, `do-loop-order`,
`conditional-expressions`, and `loop-return-paths` each assert specified output on both
runtimes. [ControlFlowTests.cs](../../tests/Weft.Tests/ControlFlowTests.cs) checks invalid
conditions, scope leaks, invalid headers, illegal jumps, unreachable statements, missing
returns, and malformed IR. Hosted CI remains deferred to P06-030.
