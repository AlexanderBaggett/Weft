using System.Collections.Immutable;
using System.Globalization;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.IR;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public sealed record BindResult(IrModule? Module, ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<FunctionSymbol> Functions, ImmutableArray<TypeSymbol> Types);

public sealed partial class Binder
{
    private readonly DiagnosticBag diagnostics = [];
    private readonly Dictionary<string, List<FunctionSymbol>> functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeSymbol> types = new(StringComparer.Ordinal);
    private readonly HashSet<string> namespaces = new(StringComparer.Ordinal) { "" };
    private readonly List<(FunctionSyntax Syntax, FunctionSymbol Symbol, string Namespace)> bodies = [];
    private readonly HashSet<IntrinsicSignature> intrinsics = [];
    private int nextSymbol;
    private int loopDepth;
    private SymbolScope scope = new();
    private FunctionSymbol currentFunction = null!;
    private string currentNamespace = "";

    public BindResult Bind(string name, IEnumerable<SyntaxTree> trees)
    {
        var inputs = trees.ToArray();
        foreach (var tree in inputs) diagnostics.AddRange(tree.Diagnostics);
        if (diagnostics.HasErrors) return new(null, diagnostics.ToImmutableArray(), [], []);
        foreach (var tree in inputs) CollectTypes(tree.Declarations, "");
        if (diagnostics.HasErrors) return new(null, diagnostics.ToImmutableArray(), [], types.Values.ToImmutableArray());
        foreach (var tree in inputs) Declare(tree.Declarations, "");
        ValidatePublicContracts();
        if (diagnostics.HasErrors) return new(null, diagnostics.ToImmutableArray(), functions.Values.SelectMany(group => group).ToImmutableArray(), types.Values.ToImmutableArray());
        var bound = ImmutableArray.CreateBuilder<IrFunction>();
        foreach (var (syntax, symbol, ns) in bodies)
        {
            currentFunction = symbol;
            currentNamespace = ns;
            scope = new(); initializedFields = [];
            var prefix = ImmutableArray.CreateBuilder<IrStatement>();
            if (symbol.Receiver is not null)
            {
                if (!symbol.IsConstructor) scope.Declare(symbol.Receiver);
                else
                {
                    var origin = new SourceOrigin(symbol.Location, "constructor-allocation");
                    prefix.Add(new IrVariable(symbol.Receiver, new IrAllocate(symbol.Receiver.Type, origin), origin));
                    bindingFieldInitializer = true;
                    foreach (var (field, initializer) in fields[symbol.ContainingType!])
                    {
                        if (initializer is null) continue;
                        var value = ConvertImplicit(BindExpression(initializer), field.Type);
                        prefix.Add(new IrExpressionStatement(new IrFieldWrite(field, This(origin), value, new(initializer.Location)), new(initializer.Location)));
                        initializedFields.Add(field.Id);
                    }
                    bindingFieldInitializer = false;
                    scope.Declare(symbol.Receiver);
                }
            }
            foreach (var parameter in symbol.Parameters) if (!scope.Declare(parameter)) diagnostics.Error("WF2002", $"Duplicate parameter '{parameter.Name}'.", parameter.Location);
            if (syntax.Body is null) { diagnostics.Error("WF2009", $"Function '{symbol.Name}' requires a body in an executable project.", syntax.Location); continue; }
            foreach (var modifier in syntax.Modifiers)
                if (modifier is "async" or "pure" or "idempotent" or "external") diagnostics.Error("WF2009", $"The '{modifier}' semantic pass is scheduled for Phase 2/4 and is not implemented yet.", syntax.Location);
            var body = BindBlock(syntax.Body);
            if (symbol.IsConstructor)
            {
                prefix.AddRange(body.Statements);
                if (ControlFlow.CanComplete(body))
                {
                    RequireInitialized(syntax.Location);
                    prefix.Add(new IrReturn(This(new(syntax.Location)), new(syntax.Location, "constructor-result")));
                }
                body = body with { Statements = prefix.ToImmutable() };
            }
            if (symbol.ReturnType != WeftType.Void && ControlFlow.CanComplete(body)) diagnostics.Error("WF2007", $"Not all paths in '{symbol.Name}' return '{symbol.ReturnType.Name}'.", syntax.Location);
            bound.Add(new(symbol, body, new(syntax.Location)));
        }
        var module = diagnostics.HasErrors ? null : new IrModule(name, bound.ToImmutable(), intrinsics.OrderBy(x => x.Name, StringComparer.Ordinal).ToImmutableArray(), Intrinsics.AbiVersion, types.Values.Where(t => !t.IsStatic).Select(t => new IrClass(t, fields[t.Name].Select(f => f.Symbol).ToImmutableArray())).ToImmutableArray());
        return new(module, diagnostics.ToImmutableArray(), functions.Values.SelectMany(group => group).ToImmutableArray(), types.Values.ToImmutableArray());
    }

    private static string Qualify(string ns, string name) => string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    private IrBlock BindBlock(BlockSyntax block)
    {
        var previous = scope;
        scope = new(previous);
        var statements = ImmutableArray.CreateBuilder<IrStatement>();
        var terminated = false;
        foreach (var statement in block.Statements)
        {
            if (terminated) diagnostics.Error("WF2010", "Statement is unreachable after a guaranteed control-flow exit.", statement.Location);
            var bound = BindStatement(statement);
            statements.Add(bound);
            terminated |= !ControlFlow.CanComplete(bound);
        }
        scope = previous;
        return new(statements.ToImmutable(), new(block.Location));
    }

    private IrStatement BindNestedStatement(StatementSyntax statement)
    {
        if (statement is VariableSyntax)
            diagnostics.Error("WF2017", "A variable declaration used as a branch or loop body must be enclosed in braces.", statement.Location);
        var previous = scope; scope = new(previous);
        var result = BindStatement(statement); scope = previous;
        return result;
    }

    private IrStatement BindStatement(StatementSyntax statement)
    {
        var origin = new SourceOrigin(statement.Location);
        switch (statement)
        {
            case EmptySyntax: return new IrBlock([], origin);
            case BreakSyntax:
                if (loopDepth == 0) diagnostics.Error("WF2016", "break requires an enclosing loop.", statement.Location);
                if (constructionLoops.TryPeek(out var breakLoop)) breakLoop.Breaks.Add(initializedFields.ToHashSet());
                return new IrBreak(origin);
            case ContinueSyntax:
                if (loopDepth == 0) diagnostics.Error("WF2016", "continue requires an enclosing loop.", statement.Location);
                if (constructionLoops.TryPeek(out var continueLoop)) continueLoop.Continues.Add(initializedFields.ToHashSet());
                return new IrContinue(origin);
            case BlockSyntax block: return BindBlock(block);
            case VariableSyntax variable:
                if (variable.Name == "this") diagnostics.Error("WF2012", "A local cannot be named this.", variable.Location);
                var initializer = BindExpression(variable.Initializer);
                var type = variable.Type is null ? initializer.Type : ResolveType(variable.Type, false);
                initializer = ConvertImplicit(initializer, type);
                if (type == WeftType.Void) diagnostics.Error("WF2003", "Cannot bind a void expression to a variable.", variable.Location);
                var symbol = new VariableSymbol(nextSymbol++, variable.Name, type, variable.Location);
                if (!scope.Declare(symbol)) diagnostics.Error("WF2002", $"Duplicate local '{variable.Name}'.", variable.Location);
                return new IrVariable(symbol, initializer, origin);
            case ReturnSyntax returned:
                if (currentFunction.IsConstructor)
                {
                    if (returned.Expression is not null) diagnostics.Error("WF2003", "A constructor return cannot specify a value.", returned.Location);
                    RequireInitialized(returned.Location); return new IrReturn(This(origin), origin);
                }
                var value = returned.Expression is null ? null : BindExpression(returned.Expression);
                if (value?.Type == WeftType.Void) diagnostics.Error("WF2003", "A void return cannot carry an expression; call it before returning.", statement.Location);
                if (value is not null && value.Type != WeftType.Void) value = ConvertImplicit(value, currentFunction.ReturnType);
                else Require(currentFunction.ReturnType, value?.Type ?? WeftType.Void, statement.Location);
                return new IrReturn(value, origin);
            case ExpressionStatementSyntax expression:
                var bound = BindExpression(expression.Expression);
                if (bound.Type != WeftType.Error && bound is not (IrCall or IrIntrinsic or IrAssign or IrUpdate or IrFieldWrite or IrFieldUpdate or IrSequence)) diagnostics.Error("WF2008", "Only calls, assignments, or increment/decrement operations may be expression statements.", expression.Location);
                return new IrExpressionStatement(bound, origin);
            case IfSyntax conditional:
                var condition = BindExpression(conditional.Condition); Require(WeftType.Bool, condition.Type, condition.Origin.Location);
                var before = initializedFields.ToHashSet();
                var then = BindNestedStatement(conditional.Then); var afterThen = initializedFields;
                initializedFields = before;
                var otherwise = conditional.Else is null ? null : BindNestedStatement(conditional.Else);
                if (ControlFlow.CanComplete(then))
                {
                    if (otherwise is not null && !ControlFlow.CanComplete(otherwise)) initializedFields = afterThen;
                    else initializedFields.IntersectWith(afterThen);
                }
                return new IrIf(condition, then, otherwise, origin);
            case WhileSyntax loop:
                var test = BindExpression(loop.Condition); Require(WeftType.Bool, test.Type, test.Origin.Location);
                var beforeWhile = initializedFields.ToHashSet(); var whileBody = BindLoopBody(loop.Body, out _);
                initializedFields = beforeWhile;
                return new IrWhile(test, whileBody, origin);
            case DoWhileSyntax loop:
                var beforeDo = initializedFields.ToHashSet();
                var body = BindLoopBody(loop.Body, out var doFrame);
                if (ControlFlow.CanComplete(body)) doFrame.Continues.Add(initializedFields);
                initializedFields = IntersectStates(doFrame.Continues, beforeDo);
                var doTest = BindExpression(loop.Condition); Require(WeftType.Bool, doTest.Type, doTest.Origin.Location);
                if (doFrame.Continues.Count > 0 && !ControlFlow.IsTrue(doTest)) doFrame.Breaks.Add(initializedFields);
                initializedFields = IntersectStates(doFrame.Breaks, beforeDo);
                return new IrDoWhile(body, doTest, origin);
            case ForSyntax loop:
                var previous = scope; scope = new(previous);
                var initializers = loop.Initializers.Select(BindStatement).ToImmutableArray();
                var forTest = loop.Condition is null ? null : BindExpression(loop.Condition);
                if (forTest is not null) Require(WeftType.Bool, forTest.Type, forTest.Origin.Location);
                var beforeFor = initializedFields.ToHashSet();
                var iterators = loop.Iterators.Select(expression =>
                    ((IrExpressionStatement)BindStatement(new ExpressionStatementSyntax(expression, expression.Location))).Expression).ToImmutableArray();
                initializedFields = beforeFor.ToHashSet();
                var forBody = BindLoopBody(loop.Body, out _); scope = previous; initializedFields = beforeFor;
                return new IrFor(initializers, forTest, iterators, forBody, origin);
            case EffectScopeSyntax effect:
                diagnostics.Error("WF2009", $"'{effect.Kind}' syntax is represented; its semantic/lowering pass is not implemented yet.", effect.Location);
                return new IrBlock([], origin);
            default: throw new InvalidOperationException($"Unhandled syntax: {statement.GetType().Name}");
        }
    }

    private sealed class ConstructionLoop
    {
        public List<HashSet<int>> Breaks { get; } = [];
        public List<HashSet<int>> Continues { get; } = [];
    }
    private readonly Stack<ConstructionLoop> constructionLoops = new();
    private static HashSet<int> IntersectStates(List<HashSet<int>> states, HashSet<int> fallback)
    {
        if (states.Count == 0) return fallback.ToHashSet();
        var result = states[0].ToHashSet();
        foreach (var state in states.Skip(1)) result.IntersectWith(state);
        return result;
    }
    private IrStatement BindLoopBody(StatementSyntax body, out ConstructionLoop frame)
    {
        loopDepth++; frame = new(); constructionLoops.Push(frame);
        var result = BindNestedStatement(body);
        constructionLoops.Pop(); loopDepth--;
        return result;
    }

    private IrExpression BindExpression(ExpressionSyntax syntax)
    {
        var origin = new SourceOrigin(syntax.Location);
        switch (syntax)
        {
            case LiteralSyntax literal:
                var token = literal.Token;
                if (token.Kind == TokenKind.String) return new IrConstant(token.StringValue!, WeftType.String, origin);
                if (token.Text is "true" or "false") return new IrConstant(token.Text == "true", WeftType.Bool, origin);
                if (token.Kind == TokenKind.Number)
                {
                    var digits = token.Text.Replace("_", "", StringComparison.Ordinal);
                    var longLiteral = digits.EndsWith('L') || digits.EndsWith('l');
                    if (longLiteral) digits = digits[..^1];
                    if (long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                    {
                        if (!longLiteral && number <= int.MaxValue) return new IrConstant((int)number, WeftType.Int32, origin);
                        return new IrConstant(number, WeftType.Int64, origin);
                    }
                    diagnostics.Error("WF2004", $"Invalid or unsupported numeric literal '{token.Text}'.", token.Location);
                }
                else diagnostics.Error("WF2009", $"Execution of '{token.Kind}' / '{token.Text}' requires the corresponding Phase 2 semantic pass.", token.Location);
                return Error(origin);
            case NewSyntax created: return BindNew(created);
            case MemberSyntax member:
                var fieldRead = BindField(member);
                if (fieldRead is not null) { RequireFieldInitialized(fieldRead.Field, fieldRead.Receiver, member.Location); return fieldRead; }
                return Error(origin);
            case NameSyntax { Name: "this" }:
                if (currentFunction.Receiver is null || bindingFieldInitializer)
                { diagnostics.Error("WF2020", "this requires an instance method or constructor body.", syntax.Location); return Error(origin); }
                RequireInitialized(syntax.Location); return This(origin);
            case NameSyntax name:
                var variable = scope.Lookup(name.Name);
                if (variable is not null) return new IrRead(variable, origin);
                var implicitField = BindImplicitField(name.Name, name.Location);
                if (implicitField is not null) { RequireFieldInitialized(implicitField.Field, implicitField.Receiver, name.Location); return implicitField; }
                diagnostics.Error("WF2001", $"Unknown variable '{name.Name}'.", name.Location);
                return Error(origin);
            case UnarySyntax unary:
                if (unary.Operator == "-" && unary.Operand is LiteralSyntax { Token.Kind: TokenKind.Number } negative)
                {
                    var raw = negative.Token.Text.Replace("_", "", StringComparison.Ordinal);
                    if (raw == "2147483648") return new IrConstant(int.MinValue, WeftType.Int32, origin);
                    if (raw is "9223372036854775808" or "9223372036854775808L" or "9223372036854775808l") return new IrConstant(long.MinValue, WeftType.Int64, origin);
                }
                var operand = BindExpression(unary.Operand);
                if (unary.Operator == "!") Require(WeftType.Bool, operand.Type, unary.Location);
                else if (!IsInteger(operand.Type)) diagnostics.Error("WF2003", $"Operator '{unary.Operator}' requires an integer.", unary.Location);
                return new IrUnary(unary.Operator, operand, operand.Type, origin);
            case UpdateSyntax update: return BindUpdate(update);
            case BinarySyntax { Operator: "=" or "+=" or "-=" or "*=" or "/=" or "%=" } binary: return BindAssignment(binary);
            case BinarySyntax binary:
                var left = BindExpression(binary.Left); var afterLeft = initializedFields.ToHashSet();
                var right = BindExpression(binary.Right);
                if (binary.Operator is "&&" or "||") initializedFields.IntersectWith(afterLeft);
                return BindBinary(left, binary.Operator, right, origin);
            case ConditionalSyntax conditional:
                var condition = BindExpression(conditional.Condition); Require(WeftType.Bool, condition.Type, condition.Origin.Location);
                var beforeArms = initializedFields.ToHashSet();
                var whenTrue = BindExpression(conditional.WhenTrue); var afterTrue = initializedFields;
                initializedFields = beforeArms;
                var whenFalse = BindExpression(conditional.WhenFalse); initializedFields.IntersectWith(afterTrue);
                if (IsInteger(whenTrue.Type) && IsInteger(whenFalse.Type) && whenTrue.Type != whenFalse.Type)
                {
                    whenTrue = ConvertImplicit(whenTrue, WeftType.Int64); whenFalse = ConvertImplicit(whenFalse, WeftType.Int64);
                }
                Require(whenTrue.Type, whenFalse.Type, conditional.Location);
                if (whenTrue.Type == WeftType.Void || whenFalse.Type == WeftType.Void)
                    diagnostics.Error("WF2003", "A conditional expression must produce a value in both branches.", conditional.Location);
                return new IrConditional(condition, whenTrue, whenFalse, whenTrue.Type, origin);
            case CallSyntax call: return BindCall(call);
            default:
                diagnostics.Error("WF2009", $"Expression '{syntax.GetType().Name}' is represented but not implemented yet.", syntax.Location);
                return Error(origin);
        }
    }

    private IrExpression BindBinary(IrExpression left, string op, IrExpression right, SourceOrigin origin)
    {
        if (op == "+" && (left.Type == WeftType.String || right.Type == WeftType.String))
            return new IrBinary(AsString(left), op, AsString(right), WeftType.String, origin);
        if (IsInteger(left.Type) && IsInteger(right.Type) && left.Type != right.Type)
        {
            left = ConvertImplicit(left, WeftType.Int64); right = ConvertImplicit(right, WeftType.Int64);
        }
        Require(left.Type, right.Type, origin.Location);
        if (left.Type == WeftType.Void || right.Type == WeftType.Void) diagnostics.Error("WF2003", "A void expression cannot be an operator operand.", origin.Location);
        if (op is "&&" or "||") Require(WeftType.Bool, left.Type, origin.Location);
        else if (op is not ("==" or "!=") && !IsInteger(left.Type)) diagnostics.Error("WF2003", $"Operator '{op}' requires integer operands.", origin.Location);
        var result = op is "==" or "!=" or "<" or ">" or "<=" or ">=" or "&&" or "||" ? WeftType.Bool : left.Type;
        return new IrBinary(left, op, right, result, origin);
    }

    private Visibility DeclarationVisibility(ImmutableArray<string> modifiers, Visibility fallback, bool allowPrivate, SourceLocation location)
    {
        var access = modifiers.Where(m => m is "public" or "private" or "internal").ToArray();
        if (access.Length > 1 || modifiers.Distinct(StringComparer.Ordinal).Count() != modifiers.Length)
        {
            diagnostics.Error("WF2012", "Duplicate or conflicting declaration modifiers.", location);
            return fallback;
        }
        if (!allowPrivate && access.Contains("private"))
            diagnostics.Error("WF2012", "Private declarations must belong to a class; project-level declarations use public or internal.", location);
        return access.FirstOrDefault() switch { "public" => Visibility.Public, "private" => Visibility.Private, "internal" => Visibility.Internal, _ => fallback };
    }

    private void RegisterNamespace(string name, SourceLocation location)
    {
        while (name.Length > 0)
        {
            if (types.ContainsKey(name) || functions.ContainsKey(name))
                diagnostics.Error("WF2002", $"Namespace '{name}' conflicts with a type or function.", location);
            namespaces.Add(name);
            var dot = name.LastIndexOf('.'); name = dot < 0 ? "" : name[..dot];
        }
    }

    private IReadOnlyList<FunctionSymbol> FindFunctions(string name, out bool foundName)
    {
        foundName = false;
        var ns = currentFunction.ContainingType ?? currentNamespace;
        var firstDot = name.IndexOf('.');
        var firstName = firstDot < 0 ? name : name[..firstDot];
        while (true)
        {
            var first = Qualify(ns, firstName);
            if (types.ContainsKey(first) || namespaces.Contains(first) || functions.ContainsKey(first))
            {
                // Once a qualifier resolves locally, a missing member must not fall
                // through to a same-named type/namespace in an outer scope.
                foundName = true;
                return functions.TryGetValue(Qualify(ns, name), out var found) ? found : [];
            }
            if (ns.Length == 0) return [];
            var dot = ns.LastIndexOf('.'); ns = dot < 0 ? "" : ns[..dot];
        }
    }

    private IrExpression BindCall(CallSyntax call)
    {
        var origin = new SourceOrigin(call.Location);
        var fullName = NameOf(call.Target);
        IrExpression? receiver = null;
        var explicitReceiver = false;
        IReadOnlyList<FunctionSymbol> group;
        bool foundName;
        if (call.Target is MemberSyntax member && !IsStaticQualifier(member.Target, out var qualifier))
        {
            receiver = BindExpression(member.Target); explicitReceiver = true;
            fullName = receiver.Type.Name + "." + member.Member;
            group = functions.GetValueOrDefault(fullName) ?? []; foundName = true;
        }
        else
        {
            if (call.Target is MemberSyntax qualified && IsStaticQualifier(qualified.Target, out qualifier)) fullName = qualifier + "." + qualified.Member;
            if (call.Target is NameSyntax localName && scope.Lookup(localName.Name) is { } local)
            {
                diagnostics.Error("WF2003", $"Local '{local.Name}' has non-callable type '{local.Type.Name}'.", call.Target.Location); return Error(origin);
            }
            if (call.Target is NameSyntax fieldName && currentFunction.ContainingType is { } owner &&
                fields[owner].FirstOrDefault(f => f.Symbol.Name == fieldName.Name).Symbol is { } field)
            {
                diagnostics.Error("WF2003", $"Field '{field.Name}' has non-callable type '{field.Type.Name}'.", call.Target.Location); return Error(origin);
            }
            if (call.Target is MemberSyntax) { group = functions.GetValueOrDefault(fullName) ?? []; foundName = true; }
            else group = FindFunctions(fullName, out foundName);
        }
        var arguments = call.Arguments.Select(a => BindExpression(a.Expression)).ToImmutableArray();
        if (arguments.Any(a => a.Type == WeftType.Error) || receiver?.Type == WeftType.Error) return Error(origin);
        if (!foundName && (fullName is "Print" or "Log"))
        {
            if (arguments.Length != 1) { diagnostics.Error("WF2006", $"'{fullName}' expects one argument.", call.Location); return Error(origin); }
            if (call.Arguments[0].Name is not (null or "value"))
            { diagnostics.Error("WF2014", $"'{fullName}' has no parameter named '{call.Arguments[0].Name}'.", call.Arguments[0].Location); return Error(origin); }
            intrinsics.Add(Intrinsics.Print);
            return new IrIntrinsic(Intrinsics.Print, [AsString(arguments[0])], origin);
        }
        return BindInvocation(call, fullName, group, arguments, receiver, explicitReceiver);
    }

    private IrExpression BindInvocation(CallSyntax call, string fullName, IReadOnlyList<FunctionSymbol> group,
        ImmutableArray<IrExpression> arguments, IrExpression? receiver = null, bool explicitReceiver = false)
    {
        var origin = new SourceOrigin(call.Location);
        if (group.Count == 0) { diagnostics.Error("WF2001", $"Unknown function '{fullName}'.", call.Location); return Error(origin); }
        var accessible = group.Where(f => f.Visibility != Visibility.Private || f.ContainingType == currentFunction.ContainingType).ToArray();
        if (accessible.Length == 0)
        {
            diagnostics.Add(new("WF2011", $"'{fullName}' is inaccessible from this location.", call.Location, Related: group.Select(f => f.Location).ToArray()));
            return Error(origin);
        }
        var candidates = new List<(FunctionSymbol Function, ImmutableArray<int> Order, int[] Ranks)>();
        foreach (var function in accessible)
        {
            if (!TryArgumentOrder(function, call.Arguments, out var order, out _)) continue;
            var ranks = arguments.Select((argument, i) => ConversionRank(argument.Type, function.Parameters[order[i]].Type)).ToArray();
            if (ranks.All(rank => rank >= 0)) candidates.Add((function, order, ranks));
        }
        if (candidates.Count == 0)
        {
            if (accessible.Length == 1)
            {
                var function = accessible[0];
                if (!TryArgumentOrder(function, call.Arguments, out var order, out var reason))
                    diagnostics.Error(call.Arguments.Any(a => a.Name is not null) ? "WF2014" : "WF2006", reason!, call.Location);
                else for (var i = 0; i < arguments.Length; i++) ConvertImplicit(arguments[i], function.Parameters[order[i]].Type);
            }
            else diagnostics.Add(new("WF2006", $"No overload of '{fullName}' accepts these arguments.", call.Location, Related: accessible.Select(f => f.Location).ToArray()));
            return Error(origin);
        }
        bool Better((FunctionSymbol Function, ImmutableArray<int> Order, int[] Ranks) a, (FunctionSymbol Function, ImmutableArray<int> Order, int[] Ranks) b)
        {
            if (a.Ranks.Where((rank, i) => rank > b.Ranks[i]).Any()) return false;
            if (a.Ranks.Where((rank, i) => rank < b.Ranks[i]).Any()) return true;
            return a.Function.Parameters.Length == arguments.Length && b.Function.Parameters.Length > arguments.Length;
        }
        var winners = candidates.Where(candidate => candidates.All(other => candidate.Function == other.Function || Better(candidate, other))).ToArray();
        if (winners.Length != 1)
        {
            diagnostics.Add(new("WF2015", $"Call to '{fullName}' is ambiguous; no overload is better for all arguments.", call.Location,
                Related: candidates.Select(c => c.Function.Location).ToArray()));
            return Error(origin);
        }
        var selected = winners[0];
        if (!selected.Function.IsConstructor)
        {
            if (selected.Function.Receiver is not null)
            {
                if (receiver is null && call.Target is NameSyntax && currentFunction.Receiver?.Type == selected.Function.Receiver.Type && !bindingFieldInitializer)
                { RequireInitialized(call.Location); receiver = This(origin); }
                if (receiver is null) { diagnostics.Error("WF2020", "An instance method requires an object receiver.", call.Location); return Error(origin); }
            }
            else if (explicitReceiver) { diagnostics.Error("WF2020", "A static method must be called through its class, not an object.", call.Location); return Error(origin); }
        }
        var supplied = arguments.Select((argument, i) => ConvertImplicit(argument, selected.Function.Parameters[selected.Order[i]].Type)).ToImmutableArray().ToBuilder();
        for (var i = supplied.Count; i < selected.Order.Length; i++)
        {
            var parameter = selected.Function.Parameters[selected.Order[i]];
            supplied.Add(new IrConstant(parameter.Default!.Value, parameter.Type, new(parameter.Location, "optional-argument", origin)));
        }
        return new IrCall(selected.Function, supplied.ToImmutable(), origin,
            selected.Order.Where((index, i) => index != i).Any() ? selected.Order : default, receiver);
    }

    private static bool TryArgumentOrder(FunctionSymbol function, ImmutableArray<ArgumentSyntax> arguments, out ImmutableArray<int> order, out string? error)
    {
        order = []; error = null;
        var mapping = ImmutableArray.CreateBuilder<int>();
        var used = new bool[function.Parameters.Length];
        var outOfPositionName = false;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            var parameter = i;
            if (argument.Name is not null)
            {
                parameter = -1;
                for (var j = 0; j < function.Parameters.Length; j++) if (function.Parameters[j].Name == argument.Name) { parameter = j; break; }
                if (parameter < 0) { error = $"'{function.Name}' has no parameter named '{argument.Name}'."; return false; }
                outOfPositionName |= parameter != i;
            }
            else if (outOfPositionName) { error = "Positional arguments cannot follow an out-of-position named argument."; return false; }
            if (parameter >= used.Length) { error = $"'{function.Name}' accepts at most {used.Length} arguments."; return false; }
            if (used[parameter]) { error = $"Parameter '{function.Parameters[parameter].Name}' is supplied more than once."; return false; }
            used[parameter] = true; mapping.Add(parameter);
        }
        for (var i = 0; i < used.Length; i++)
        {
            if (used[i]) continue;
            if (function.Parameters[i].Default is null) { error = $"Missing required argument '{function.Parameters[i].Name}' for '{function.Name}'."; return false; }
            mapping.Add(i);
        }
        order = mapping.ToImmutable(); return true;
    }

    private static int ConversionRank(WeftType from, WeftType to) => from == to ? 0 : from == WeftType.Int32 && to == WeftType.Int64 ? 1 : -1;
    private IrExpression ConvertImplicit(IrExpression value, WeftType type)
    {
        if (value.Type == WeftType.Int32 && type == WeftType.Int64) return new IrConvert(value, type, value.Origin);
        Require(type, value.Type, value.Origin.Location);
        return value;
    }
    private static string NameOf(ExpressionSyntax expression) => expression switch { NameSyntax n => n.Name, MemberSyntax m => NameOf(m.Target) + "." + m.Member, _ => "<indirect>" };
    private IrExpression AsString(IrExpression value)
    {
        if (value.Type == WeftType.String) return value;
        var intrinsic = value.Type.Kind switch { TypeKind.Int32 => Intrinsics.ToStringInt32, TypeKind.Int64 => Intrinsics.ToStringInt64, TypeKind.Bool => Intrinsics.ToStringBool, _ => null };
        if (intrinsic is null) { diagnostics.Error("WF2003", $"Cannot format '{value.Type.Name}' as a string.", value.Origin.Location); return Error(value.Origin); }
        intrinsics.Add(intrinsic); return new IrIntrinsic(intrinsic, [value], value.Origin);
    }
    private void Require(WeftType expected, WeftType actual, SourceLocation location)
    {
        if (expected != actual && expected != WeftType.Error && actual != WeftType.Error) diagnostics.Error("WF2003", $"Expected '{expected.Name}', found '{actual.Name}'.", location);
    }
    private static bool IsInteger(WeftType type) => type == WeftType.Int32 || type == WeftType.Int64;
    private static IrConstant Error(SourceOrigin origin) => new(0, WeftType.Error, origin);
}
