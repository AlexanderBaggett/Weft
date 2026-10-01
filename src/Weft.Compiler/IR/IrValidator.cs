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
        var classes = new Dictionary<string, IrClass>(StringComparer.Ordinal);
        var fields = new Dictionary<int, FieldSymbol>();
        foreach (var type in module.Classes.IsDefault ? [] : module.Classes)
        {
            var origin = new SourceOrigin(type.Symbol.Location);
            if (!Enum.IsDefined(type.Symbol.Kind) || type.Symbol.IsStatic || !classes.TryAdd(type.Symbol.Name, type)) Fail("Invalid or duplicate IR class.", origin);
            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in type.Fields)
                if (field.Owner != new WeftType(TypeKind.Nominal, type.Symbol.Name) || !fields.TryAdd(field.Id, field) || !fieldNames.Add(field.Name))
                    Fail("Invalid or duplicate IR field identity, name, or owner.", new(field.Location));
        }
        foreach (var field in fields.Values) CheckType(field.Type, false, new(field.Location));
        FunctionSymbol? currentFunction = null;
        HashSet<int> functionIdentities = [];
        HashSet<int> initializing = [];
        var constructorTargets = new Dictionary<int, FunctionSymbol>();
        foreach (var function in module.Functions)
        {
            var signature = function.Symbol.Name + "(" + string.Join(",", function.Symbol.Parameters.Select(p => p.Type.Name)) + ")";
            if (!functions.TryAdd(function.Symbol.Id, function.Symbol) || !names.Add(signature)) Fail("Duplicate IR function identity or signature.", function.Origin);
            CheckType(function.Symbol.ReturnType, true, function.Origin);
        }
        foreach (var function in module.Functions)
        {
            currentFunction = function.Symbol;
            if (currentFunction.IsCopyConstructor && (!currentFunction.IsConstructor || currentFunction.Receiver is null ||
                currentFunction.Parameters.Length != 1 || currentFunction.Parameters[0].Type != currentFunction.Receiver.Type ||
                !classes.TryGetValue(currentFunction.ContainingType ?? "", out var copyOwner) ||
                copyOwner.Symbol.Kind != DataKind.Record || copyOwner.CopyConstructor != currentFunction))
                Fail("Invalid IR record copy constructor signature or owner.", function.Origin);
            if (currentFunction.IsInitAccessor && (currentFunction.IsConstructor || currentFunction.Receiver is null ||
                currentFunction.ReturnType != WeftType.Void || currentFunction.Parameters.Length != 1))
                Fail("Invalid IR init accessor signature.", function.Origin);
            if (currentFunction.IsConstructor)
            {
                if (currentFunction.Receiver is null || function.Body.Statements.IsDefaultOrEmpty ||
                    function.Body.Statements[0] is not IrVariable allocation || allocation.Symbol != currentFunction.Receiver)
                    Fail("IR constructor must begin by initializing its receiver.", function.Origin);
                else if (allocation.Initializer is IrCall { Function.IsConstructor: true } delegated &&
                    delegated.Type == currentFunction.Receiver.Type && delegated.Function.ContainingType == currentFunction.ContainingType)
                    constructorTargets[currentFunction.Id] = delegated.Function;
                else if (allocation.Initializer is not IrAllocate memory || memory.Type != currentFunction.Receiver.Type)
                    Fail("IR constructor receiver must come from allocation or a same-class constructor chain.", function.Origin);
            }
            var locals = new Dictionary<int, VariableSymbol>();
            var identities = new HashSet<int>(); functionIdentities = identities;
            if (function.Symbol.Receiver is { } receiver)
            {
                CheckType(receiver.Type, false, function.Origin);
                if (receiver.Type.Kind != TypeKind.Nominal || receiver.Type.Name != function.Symbol.ContainingType ||
                    function.Symbol.IsConstructor && function.Symbol.ReturnType != receiver.Type)
                    Fail("Invalid IR receiver or constructor result type.", function.Origin);
                if (!function.Symbol.IsConstructor) { locals.Add(receiver.Id, receiver); identities.Add(receiver.Id); }
            }
            else if (function.Symbol.IsConstructor) Fail("IR constructor requires a receiver local.", function.Origin);
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
        foreach (var type in classes.Values)
            if (type.CopyConstructor is { } copyConstructor && (type.Symbol.Kind != DataKind.Record || !copyConstructor.IsCopyConstructor ||
                copyConstructor.ContainingType != type.Symbol.Name || !functions.TryGetValue(copyConstructor.Id, out var declaredCopy) || declaredCopy != copyConstructor))
                Fail("Invalid or unregistered IR copy constructor.", new(type.Symbol.Location));
        ConstructorGraph.Order(functions.Values.Where(f => f.IsConstructor), constructorTargets, cycle =>
            Fail("Circular IR constructor chain: " + string.Join(" -> ", cycle.Select(ConstructorGraph.Signature)) + ".", new(cycle[0].Location)));
        return diagnostics.ToImmutableArray();

        void Fail(string message, SourceOrigin origin) => diagnostics.Error("WF3001", message, origin.Location);
        void CheckType(WeftType type, bool allowVoid, SourceOrigin origin)
        {
            if (type != WeftType.Bool && type != WeftType.Int32 && type != WeftType.Int64 && type != WeftType.String && !(type.Kind == TypeKind.Nominal && type.Arguments.IsDefaultOrEmpty && classes.ContainsKey(type.Name)) && !(allowVoid && type == WeftType.Void))
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
                    if (currentFunction?.IsConstructor == true && (value.Expression is not IrRead result || result.Symbol != currentFunction.Receiver))
                        Fail("IR constructor must return its allocated receiver.", value.Origin);
                    if (value.Expression is not null)
                    {
                        Expression(value.Expression, locals);
                        if (value.Expression.Type == WeftType.Void) Fail("A void IR return cannot carry an expression.", value.Origin);
                    }
                    if ((value.Expression?.Type ?? WeftType.Void) != returned) Fail("IR return type mismatch.", value.Origin);
                    break;
                case IrExpressionStatement expression:
                    Expression(expression.Expression, locals);
                    if (expression.Expression is not (IrAssign or IrCall or IrIntrinsic or IrUpdate or IrFieldWrite or IrFieldUpdate or IrSequence or IrSetterCall)) Fail("IR expression statement must be a call, assignment, or increment/decrement operation.", expression.Origin);
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
                case IrSetterCall setter:
                    Expression(new IrCall(setter.Setter, [setter.Value], setter.Origin, Receiver: setter.Receiver), locals);
                    if (setter.Setter.ReturnType != WeftType.Void || setter.Setter.Parameters.Length != 1 || setter.Setter.Receiver is null)
                        Fail("Invalid IR setter signature.", setter.Origin);
                    break;
                case IrCopy copy:
                    Expression(copy.Receiver, locals);
                    if (!classes.TryGetValue(copy.Type.Name, out var copiedType) || copiedType.Symbol.Kind is not (DataKind.Record or DataKind.Model))
                        Fail("IR copy requires a record or model.", copy.Origin);
                    break;
                case IrObjectHash hash:
                    Expression(hash.Receiver, locals);
                    if (!classes.TryGetValue(hash.Receiver.Type.Name, out var hashedType) || hashedType.Symbol.Kind != DataKind.Record)
                        Fail("IR record hash requires a record receiver.", hash.Origin);
                    break;
                case IrAllocate allocated:
                    if (allocated.Type.Kind != TypeKind.Nominal || currentFunction?.IsConstructor != true || currentFunction.Receiver?.Type != allocated.Type)
                        Fail("Raw IR allocation is restricted to the matching constructor.", allocated.Origin);
                    break;
                case IrFieldRead read:
                    Field(read.Field, read.Receiver, read.Origin, locals); break;
                case IrFieldWrite write:
                    Field(write.Field, write.Receiver, write.Origin, locals); Expression(write.Value, locals);
                    if (write.Value.Type != write.Type) Fail("IR field assignment type mismatch.", write.Origin);
                    Writable(write.Field, write.Receiver, write.Origin); break;
                case IrFieldUpdate update:
                    Field(update.Field, update.Receiver, update.Origin, locals); Writable(update.Field, update.Receiver, update.Origin);
                    if (!Integer(update.Type) || update.Operator is not ("++" or "--")) Fail("Invalid IR field update.", update.Origin);
                    break;
                case IrSequence sequence:
                    if (sequence.Bindings.IsDefaultOrEmpty)
                    { Fail("IR sequence must contain at least one binding.", sequence.Origin); break; }
                    var sequenceLocals = new Dictionary<int, VariableSymbol>(locals);
                    var first = sequence.Bindings[0];
                    if (sequence.Initializing is { } instance && (first.Symbol != instance ||
                        first.Initializer is not (IrCall { Function.IsConstructor: true } or IrCopy) || first.Initializer.Type != instance.Type ||
                        sequence.Value is not IrRead result || result.Symbol != instance))
                        Fail("IR initializer must start with construction/copy and yield that same object.", sequence.Origin);
                    Statement(first, sequenceLocals, functionIdentities, WeftType.Void);
                    var added = sequence.Initializing is { } fresh && initializing.Add(fresh.Id);
                    foreach (var binding in sequence.Bindings.Skip(1)) Statement(binding, sequenceLocals, functionIdentities, WeftType.Void);
                    Expression(sequence.Value, sequenceLocals);
                    if (added) initializing.Remove(sequence.Initializing!.Id);
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
                    if (initializing.Contains(assign.Symbol.Id) || assign.Symbol == currentFunction?.Receiver)
                        Fail("IR cannot reassign this or an initializing object identity.", assign.Origin);
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
                        "==" or "!=" => (Integer(type) || type == WeftType.Bool || type == WeftType.String || type.Kind == TypeKind.Nominal) && binary.Type == WeftType.Bool,
                        "&&" or "||" => type == WeftType.Bool && binary.Type == WeftType.Bool,
                        _ => false
                    };
                    if (binary.Left.Type != binary.Right.Type || !valid) Fail("Invalid IR binary operator signature.", binary.Origin);
                    break;
                case IrCall call:
                    if (!functions.TryGetValue(call.Function.Id, out var target) || target != call.Function) Fail("IR call targets a missing or mismatched function.", call.Origin);
                    if (call.Function.IsInitAccessor && !(call.Receiver is IrRead initReceiver &&
                        (initializing.Contains(initReceiver.Symbol.Id) || initReceiver.Symbol == currentFunction?.Receiver &&
                            (currentFunction.IsConstructor || currentFunction.IsInitAccessor))))
                        Fail("IR init accessor call is outside object initialization.", call.Origin);
                    var needsReceiver = call.Function.Receiver is not null && !call.Function.IsConstructor;
                    if (call.Receiver is not null) Expression(call.Receiver, locals);
                    if (needsReceiver != (call.Receiver is not null) || needsReceiver && call.Receiver?.Type != call.Function.Receiver!.Type)
                        Fail("IR call receiver is missing, unexpected, or has the wrong type.", call.Origin);
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
        void Field(FieldSymbol field, IrExpression receiver, SourceOrigin origin, Dictionary<int, VariableSymbol> locals)
        {
            Expression(receiver, locals);
            if (!fields.TryGetValue(field.Id, out var registered) || registered != field || receiver.Type != field.Owner)
                Fail("IR field reference has an unknown field or mismatched owner.", origin);
        }
        void Writable(FieldSymbol field, IrExpression receiver, SourceOrigin origin)
        {
            if (field.ReadOnly && !((currentFunction?.IsConstructor == true || currentFunction?.IsInitAccessor == true) && receiver is IrRead read && read.Symbol == currentFunction.Receiver))
                Fail("IR readonly field write is outside its constructor or init accessor.", origin);
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
