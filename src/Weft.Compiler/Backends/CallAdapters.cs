using System.Collections.Immutable;
using Weft.Compiler.IR;

namespace Weft.Compiler.Backends;

// A named call evaluates arguments in source order before a static adapter puts the
// resulting values in parameter order. No closure or runtime argument array is needed.
public static class CallAdapters
{
    public static string Name(IrCall call) => call.ParameterOrder.IsDefaultOrEmpty
        ? $"f_{call.Function.Id}" : $"c_{call.Function.Id}_{string.Join("_", call.ParameterOrder)}";

    public static ImmutableArray<IrCall> Collect(IrModule module)
    {
        var calls = new Dictionary<string, IrCall>(StringComparer.Ordinal);
        foreach (var function in module.Functions) Statement(function.Body);
        return calls.Values.ToImmutableArray();

        void Statement(IrStatement statement)
        {
            switch (statement)
            {
                case IrBlock block: foreach (var child in block.Statements) Statement(child); break;
                case IrVariable variable: Expression(variable.Initializer); break;
                case IrReturn { Expression: not null } returned: Expression(returned.Expression); break;
                case IrExpressionStatement expression: Expression(expression.Expression); break;
                case IrIf branch:
                    Expression(branch.Condition); Statement(branch.Then);
                    if (branch.Else is not null) Statement(branch.Else); break;
                case IrWhile loop: Expression(loop.Condition); Statement(loop.Body); break;
                case IrDoWhile loop: Statement(loop.Body); Expression(loop.Condition); break;
                case IrFor loop:
                    foreach (var initializer in loop.Initializers) Statement(initializer);
                    if (loop.Condition is not null) Expression(loop.Condition);
                    foreach (var iterator in loop.Iterators) Expression(iterator);
                    Statement(loop.Body); break;
            }
        }
        void Expression(IrExpression expression)
        {
            switch (expression)
            {
                case IrCall call:
                    if (!call.ParameterOrder.IsDefaultOrEmpty) calls.TryAdd(Name(call), call);
                    foreach (var argument in call.Arguments) Expression(argument); break;
                case IrIntrinsic intrinsic: foreach (var argument in intrinsic.Arguments) Expression(argument); break;
                case IrConditional conditional:
                    Expression(conditional.Condition); Expression(conditional.WhenTrue); Expression(conditional.WhenFalse); break;
                case IrAssign assign: Expression(assign.Value); break;
                case IrConvert convert: Expression(convert.Operand); break;
                case IrUnary unary: Expression(unary.Operand); break;
                case IrBinary binary: Expression(binary.Left); Expression(binary.Right); break;
            }
        }
    }
}
