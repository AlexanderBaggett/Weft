using System.Collections.Immutable;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.Semantics;
using Weft.Compiler.Text;

namespace Weft.Compiler.IR;

public static class IrValidator
{
    public static ImmutableArray<Diagnostic> Validate(IrModule module)
    {
        var diagnostics = new DiagnosticBag();
        var functions = new Dictionary<int, FunctionSymbol>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var function in module.Functions)
        {
            var signature = function.Symbol.Name + "(" + string.Join(",", function.Symbol.Parameters.Select(p => p.Type.Name)) + ")";
            if (!functions.TryAdd(function.Symbol.Id, function.Symbol) || !names.Add(signature)) Fail("Duplicate IR function identity or signature.", function.Origin);
            CheckType(function.Symbol.ReturnType, true, function.Origin);
        }
        foreach (var function in module.Functions)
        {
            var locals = new Dictionary<int, VariableSymbol>();
            var identities = new HashSet<int>();
            foreach (var parameter in function.Symbol.Parameters)
            {
                CheckType(parameter.Type, false, function.Origin);
                if (parameter.Default is { } value && (value.Type != parameter.Type || !ConstantMatches(value.Value, value.Type)))
                    Fail("IR parameter default does not match its declared type.", function.Origin);
                if (!locals.TryAdd(parameter.Id, parameter) || !identities.Add(parameter.Id)) Fail("Duplicate IR parameter identity.", function.Origin);
            }
            Statement(function.Body, locals, identities, function.Symbol.ReturnType);
            if (function.Symbol.ReturnType != WeftType.Void && ControlFlow.CanComplete(function.Body)) Fail("IR function can fall through without a return value.", function.Origin);
        }
        return diagnostics.ToImmutableArray();

        void Fail(string message, SourceOrigin origin) => diagnostics.Error("WF3001", message, origin.Location);
        void CheckType(WeftType type, bool allowVoid, SourceOrigin origin)
        {
            if (type != WeftType.Bool && type != WeftType.Int32 && type != WeftType.Int64 && type != WeftType.String && !(allowVoid && type == WeftType.Void))
                Fail($"Type '{type.Name}' is not in the executable IR version's portable type set.", origin);
        }
        void Statement(IrStatement statement, Dictionary<int, VariableSymbol> locals, HashSet<int> identities, WeftType returned, int loopDepth = 0)
        {
            switch (statement)
            {
                case IrBlock block:
                    var nested = new Dictionary<int, VariableSymbol>(locals);
                    var terminated = false;
                    foreach (var child in block.Statements)
                    {
                        if (terminated) Fail("Unreachable IR statement.", child.Origin);
                        Statement(child, nested, identities, returned, loopDepth);
                        terminated |= !ControlFlow.CanComplete(child);
                    }
                    break;
                case IrVariable variable:
                    CheckType(variable.Symbol.Type, false, variable.Origin);
                    Expression(variable.Initializer, locals);
                    if (variable.Symbol.Type != variable.Initializer.Type) Fail("IR variable initializer type mismatch.", variable.Origin);
                    if (!identities.Add(variable.Symbol.Id) || !locals.TryAdd(variable.Symbol.Id, variable.Symbol)) Fail("Duplicate IR local identity.", variable.Origin);
                    break;
                case IrReturn value:
                    if (value.Expression is not null)
                    {
                        Expression(value.Expression, locals);
                        if (value.Expression.Type == WeftType.Void) Fail("A void IR return cannot carry an expression.", value.Origin);
                    }
                    if ((value.Expression?.Type ?? WeftType.Void) != returned) Fail("IR return type mismatch.", value.Origin);
                    break;
                case IrExpressionStatement expression:
                    Expression(expression.Expression, locals);
                    if (expression.Expression is not (IrAssign or IrCall or IrIntrinsic or IrUpdate)) Fail("IR expression statement must be a call, assignment, or increment/decrement operation.", expression.Origin);
                    break;
                case IrIf conditional:
                    Expression(conditional.Condition, locals);
                    if (conditional.Condition.Type != WeftType.Bool) Fail("IR branch condition must be bool.", conditional.Origin);
                    Statement(conditional.Then, new(locals), identities, returned, loopDepth);
                    if (conditional.Else is not null) Statement(conditional.Else, new(locals), identities, returned, loopDepth);
                    break;
                case IrWhile loop:
                    Expression(loop.Condition, locals);
                    if (loop.Condition.Type != WeftType.Bool) Fail("IR loop condition must be bool.", loop.Origin);
                    Statement(loop.Body, new(locals), identities, returned, loopDepth + 1); break;
                case IrDoWhile loop:
                    Expression(loop.Condition, locals);
                    if (loop.Condition.Type != WeftType.Bool) Fail("IR loop condition must be bool.", loop.Origin);
                    Statement(loop.Body, new(locals), identities, returned, loopDepth + 1); break;
                case IrFor loop:
                    var forLocals = new Dictionary<int, VariableSymbol>(locals);
                    foreach (var initializer in loop.Initializers)
                    {
                        if (initializer is not (IrVariable or IrExpressionStatement)) Fail("IR for initializer must be a variable or expression statement.", initializer.Origin);
                        Statement(initializer, forLocals, identities, returned, loopDepth);
                    }
                    if (loop.Condition is not null)
                    {
                        Expression(loop.Condition, forLocals);
                        if (loop.Condition.Type != WeftType.Bool) Fail("IR loop condition must be bool.", loop.Origin);
                    }
                    foreach (var iterator in loop.Iterators)
                        Statement(new IrExpressionStatement(iterator, iterator.Origin), forLocals, identities, returned, loopDepth);
                    Statement(loop.Body, new(forLocals), identities, returned, loopDepth + 1); break;
                case IrBreak or IrContinue:
                    if (loopDepth == 0) Fail("IR loop exit requires an enclosing loop.", statement.Origin);
                    break;
                default: Fail("Unrecognized IR statement; extend both backends and the validator together.", statement.Origin); break;
            }
        }
        void Expression(IrExpression expression, Dictionary<int, VariableSymbol> locals)
        {
            CheckType(expression.Type, expression is IrCall or IrIntrinsic, expression.Origin);
            switch (expression)
            {
                case IrConstant constant:
                    if (!ConstantMatches(constant.Value, constant.Type)) Fail("IR constant value does not match its type.", constant.Origin);
                    break;
                case IrConditional conditional:
                    Expression(conditional.Condition, locals); Expression(conditional.WhenTrue, locals); Expression(conditional.WhenFalse, locals);
                    if (conditional.Condition.Type != WeftType.Bool || conditional.WhenTrue.Type != conditional.Type || conditional.WhenFalse.Type != conditional.Type)
                        Fail("IR conditional condition or branch type mismatch.", conditional.Origin);
                    break;
                case IrConvert conversion:
                    Expression(conversion.Operand, locals);
                    if (conversion.Operand.Type != WeftType.Int32 || conversion.Type != WeftType.Int64)
                        Fail("Unsupported IR numeric conversion.", conversion.Origin);
                    break;
                case IrRead read:
                    if (!locals.TryGetValue(read.Symbol.Id, out var declared) || declared != read.Symbol) Fail("IR read refers to an out-of-scope or mismatched local.", read.Origin);
                    break;
                case IrAssign assign:
                    Expression(new IrRead(assign.Symbol, assign.Origin), locals); Expression(assign.Value, locals);
                    if (assign.Type != assign.Value.Type) Fail("IR assignment type mismatch.", assign.Origin);
                    break;
                case IrUpdate update:
                    Expression(new IrRead(update.Symbol, update.Origin), locals);
                    if (!Integer(update.Type) || update.Operator is not ("++" or "--"))
                        Fail("Invalid IR increment/decrement operation.", update.Origin);
                    break;
                case IrUnary unary:
                    Expression(unary.Operand, locals);
                    if (unary.Type != unary.Operand.Type || !(unary.Operator == "!" && unary.Type == WeftType.Bool || unary.Operator is "+" or "-" && Integer(unary.Type))) Fail("Invalid IR unary operator signature.", unary.Origin);
                    break;
                case IrBinary binary:
                    Expression(binary.Left, locals); Expression(binary.Right, locals);
                    var type = binary.Left.Type;
                    var valid = binary.Operator switch
                    {
                        "+" => (Integer(type) || type == WeftType.String) && binary.Type == type,
                        "-" or "*" or "/" or "%" => Integer(type) && binary.Type == type,
                        "<" or ">" or "<=" or ">=" => Integer(type) && binary.Type == WeftType.Bool,
                        "==" or "!=" => (Integer(type) || type == WeftType.Bool || type == WeftType.String) && binary.Type == WeftType.Bool,
                        "&&" or "||" => type == WeftType.Bool && binary.Type == WeftType.Bool,
                        _ => false
                    };
                    if (binary.Left.Type != binary.Right.Type || !valid) Fail("Invalid IR binary operator signature.", binary.Origin);
                    break;
                case IrCall call:
                    if (!functions.TryGetValue(call.Function.Id, out var target) || target != call.Function) Fail("IR call targets a missing or mismatched function.", call.Origin);
                    var parameters = call.Function.Parameters.Select(p => p.Type).ToImmutableArray();
                    if (!call.ParameterOrder.IsDefaultOrEmpty)
                    {
                        if (call.ParameterOrder.Length != parameters.Length || call.ParameterOrder.Distinct().Count() != parameters.Length ||
                            call.ParameterOrder.Any(index => index < 0 || index >= parameters.Length))
                        { Fail("IR argument order must map every parameter exactly once.", call.Origin); break; }
                        parameters = call.ParameterOrder.Select(index => parameters[index]).ToImmutableArray();
                    }
                    Arguments(parameters, call.Arguments, call.Origin, locals); break;
                case IrIntrinsic intrinsic:
                    if (!module.RequiredIntrinsics.Any(r => SameSignature(r, intrinsic.Signature))) Fail("IR intrinsic is absent from the runtime requirements.", intrinsic.Origin);
                    if (!Intrinsics.Bootstrap.Any(r => SameSignature(r, intrinsic.Signature))) Fail("Unknown IR intrinsic signature; register its contract and both lowerings first.", intrinsic.Origin);
                    Arguments(intrinsic.Signature.Parameters, intrinsic.Arguments, intrinsic.Origin, locals); break;
                default: Fail("Unrecognized IR expression; extend both backends and the validator together.", expression.Origin); break;
            }
        }
        void Arguments(ImmutableArray<WeftType> expected, ImmutableArray<IrExpression> actual, SourceOrigin origin, Dictionary<int, VariableSymbol> locals)
        {
            foreach (var argument in actual) Expression(argument, locals);
            if (expected.Length != actual.Length || expected.Where((type, i) => i < actual.Length && type != actual[i].Type).Any()) Fail("IR call argument signature mismatch.", origin);
        }
    }
    private static bool SameSignature(IntrinsicSignature left, IntrinsicSignature right) => left.Name == right.Name && left.Result == right.Result && left.Parameters.SequenceEqual(right.Parameters);
    private static bool ConstantMatches(object value, WeftType type) => value is int && type == WeftType.Int32 || value is long && type == WeftType.Int64 || value is bool && type == WeftType.Bool || value is string && type == WeftType.String;
    private static bool Integer(WeftType type) => type == WeftType.Int32 || type == WeftType.Int64;
}
