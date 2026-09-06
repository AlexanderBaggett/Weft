using System.Collections.Immutable;
using System.Globalization;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.IR;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public sealed record BindResult(IrModule? Module, ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<FunctionSymbol> Functions, ImmutableArray<TypeSymbol> Types);

public sealed class Binder
{
    private readonly DiagnosticBag diagnostics = [];
    private readonly Dictionary<string, List<FunctionSymbol>> functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeSymbol> types = new(StringComparer.Ordinal);
    private readonly HashSet<string> namespaces = new(StringComparer.Ordinal) { "" };
    private readonly List<(FunctionSyntax Syntax, FunctionSymbol Symbol, string Namespace)> bodies = [];
    private readonly HashSet<IntrinsicSignature> intrinsics = [];
    private int nextSymbol;
    private SymbolScope scope = new();
    private FunctionSymbol currentFunction = null!;
    private string currentNamespace = "";

    public BindResult Bind(string name, IEnumerable<SyntaxTree> trees)
    {
        var inputs = trees.ToArray();
        foreach (var tree in inputs) diagnostics.AddRange(tree.Diagnostics);
        if (diagnostics.HasErrors) return new(null, diagnostics.ToImmutableArray(), [], []);
        foreach (var tree in inputs) Declare(tree.Declarations, "");
        if (diagnostics.HasErrors) return new(null, diagnostics.ToImmutableArray(), functions.Values.SelectMany(group => group).ToImmutableArray(), types.Values.ToImmutableArray());
        var bound = ImmutableArray.CreateBuilder<IrFunction>();
        foreach (var (syntax, symbol, ns) in bodies)
        {
            currentFunction = symbol;
            currentNamespace = ns;
            scope = new();
            foreach (var parameter in symbol.Parameters) if (!scope.Declare(parameter)) diagnostics.Error("WF2002", $"Duplicate parameter '{parameter.Name}'.", parameter.Location);
            if (syntax.Body is null) { diagnostics.Error("WF2009", $"Function '{symbol.Name}' requires a body in an executable project.", syntax.Location); continue; }
            foreach (var modifier in syntax.Modifiers)
                if (modifier is "async" or "pure" or "idempotent" or "external") diagnostics.Error("WF2009", $"The '{modifier}' semantic pass is scheduled for Phase 2/4 and is not implemented yet.", syntax.Location);
            var body = BindBlock(syntax.Body);
            if (symbol.ReturnType != WeftType.Void && !AlwaysReturns(body)) diagnostics.Error("WF2007", $"Not all paths in '{symbol.Name}' return '{symbol.ReturnType.Name}'.", syntax.Location);
            bound.Add(new(symbol, body, new(syntax.Location)));
        }
        var module = diagnostics.HasErrors ? null : new IrModule(name, bound.ToImmutable(), intrinsics.OrderBy(x => x.Name, StringComparer.Ordinal).ToImmutableArray(), Intrinsics.AbiVersion);
        return new(module, diagnostics.ToImmutableArray(), functions.Values.SelectMany(group => group).ToImmutableArray(), types.Values.ToImmutableArray());
    }

    private static string Qualify(string ns, string name) => string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    private void Declare(ImmutableArray<DeclarationSyntax> declarations, string ns, string? owner = null)
    {
        foreach (var declaration in declarations)
        {
            if (declaration is NamespaceSyntax space)
            {
                if (owner is not null) { diagnostics.Error("WF2012", "A namespace cannot be declared inside a class.", space.Location); continue; }
                var fullNamespace = Qualify(ns, space.Name);
                RegisterNamespace(fullNamespace, space.Location);
                if (space.FileScoped) ns = fullNamespace;
                else Declare(space.Members, fullNamespace);
                continue;
            }
            if (declaration is StaticClassSyntax helper)
            {
                if (owner is not null) { diagnostics.Error("WF2009", "Nested type binding is not implemented yet.", helper.Location); continue; }
                var typeVisibility = DeclarationVisibility(helper.Modifiers, Visibility.Internal, false, helper.Location);
                foreach (var modifier in helper.Modifiers.Where(m => m is not ("public" or "internal" or "private" or "static")))
                    diagnostics.Error("WF2012", $"Modifier '{modifier}' is invalid on a static class.", helper.Location);
                var typeName = Qualify(ns, helper.Name);
                var type = new TypeSymbol(typeName, helper.Location, typeVisibility, true);
                if (!types.TryAdd(typeName, type) || functions.ContainsKey(typeName) || namespaces.Contains(typeName)) diagnostics.Error("WF2002", $"Duplicate declaration '{typeName}'.", helper.Location);
                Declare(helper.Members, ns, typeName);
                continue;
            }
            if (declaration is ConstructSyntax construct)
            {
                if (construct.Kind is ConstructKind.Model or ConstructKind.Class or ConstructKind.Record or ConstructKind.Interface)
                {
                    var type = new TypeSymbol(Qualify(ns, construct.Name), construct.Location);
                    if (!types.TryAdd(type.Name, type)) diagnostics.Error("WF2002", $"Duplicate type '{type.Name}'.", type.Location);
                }
                diagnostics.Error("WF2009", $"'{construct.Kind}' syntax is represented, but its semantic/lowering pass is not implemented yet. This remains required release work.", construct.Location);
                continue;
            }
            if (declaration is not FunctionSyntax function) continue;
            var visibility = DeclarationVisibility(function.Modifiers,
                owner is null ? Visibility.Internal : Visibility.Private, owner is not null, function.Location);
            if (owner is not null && !function.Modifiers.Contains("static"))
                diagnostics.Error("WF2012", "A method in a static class must be declared static.", function.Location);
            var parameters = ImmutableArray.CreateBuilder<VariableSymbol>();
            var optionalSeen = false;
            foreach (var parameter in function.Parameters)
            {
                var type = ResolveType(parameter.Type, false);
                ConstantValue? defaultValue = null;
                if (parameter.Default is not null)
                {
                    optionalSeen = true;
                    defaultValue = ConstantEvaluator.Evaluate(parameter.Default);
                    if (defaultValue is null)
                        diagnostics.Error("WF2013", "A parameter default must be a supported constant without overflow or division by zero.", parameter.Default.Location);
                    else if (defaultValue.Type == WeftType.Int32 && type == WeftType.Int64)
                        defaultValue = new((long)(int)defaultValue.Value, WeftType.Int64);
                    else Require(type, defaultValue.Type, parameter.Default.Location);
                }
                else if (optionalSeen) diagnostics.Error("WF2013", "Required parameters must precede optional parameters.", parameter.Location);
                parameters.Add(new(nextSymbol++, parameter.Name, type, parameter.Location, defaultValue));
            }
            var symbol = new FunctionSymbol(nextSymbol++, Qualify(owner ?? ns, function.Name), ResolveType(function.ReturnType, true),
                parameters.ToImmutable(), function.Location, visibility, owner);
            if (!functions.TryGetValue(symbol.Name, out var overloads)) functions.Add(symbol.Name, overloads = []);
            var duplicate = overloads.FirstOrDefault(other => other.Parameters.Select(p => p.Type).SequenceEqual(symbol.Parameters.Select(p => p.Type)));
            if (duplicate is not null || types.ContainsKey(symbol.Name) || namespaces.Contains(symbol.Name))
                diagnostics.Add(new("WF2002", $"Duplicate function signature '{symbol.Name}'. Return types, parameter names, and defaults do not distinguish overloads.", function.Location,
                    Related: duplicate is null ? null : [duplicate.Location]));
            else { overloads.Add(symbol); bodies.Add((function, symbol, ns)); }
        }
    }

    private WeftType ResolveType(TypeSyntax syntax, bool allowVoid)
    {
        var type = WeftType.Builtin(syntax.Name);
        if (type is null) { diagnostics.Error("WF2001", $"Unknown or not-yet-bound type '{syntax.Name}'.", syntax.Location); return WeftType.Error; }
        if (syntax.Arguments.Length > 0 || syntax.ArrayRank > 0 || syntax.Nullable ||
            type.Kind is not (TypeKind.Void or TypeKind.Bool or TypeKind.Int32 or TypeKind.Int64 or TypeKind.String))
            diagnostics.Error("WF2009", $"Execution of type '{syntax.Name}' with these refinements is Phase 2 work and is not implemented yet.", syntax.Location);
        if (!allowVoid && type == WeftType.Void) { diagnostics.Error("WF2003", "A value cannot have type void.", syntax.Location); return WeftType.Error; }
        return type;
    }

    private IrBlock BindBlock(BlockSyntax block)
    {
        var previous = scope;
        scope = new(previous);
        var statements = ImmutableArray.CreateBuilder<IrStatement>();
        var terminated = false;
        foreach (var statement in block.Statements)
        {
            if (terminated) diagnostics.Error("WF2010", "Statement is unreachable after a guaranteed return.", statement.Location);
            var bound = BindStatement(statement);
            statements.Add(bound);
            terminated |= AlwaysReturns(bound);
        }
        scope = previous;
        return new(statements.ToImmutable(), new(block.Location));
    }

    private IrStatement BindNestedStatement(StatementSyntax statement)
    {
        var previous = scope; scope = new(previous);
        var result = BindStatement(statement); scope = previous;
        return result;
    }

    private IrStatement BindStatement(StatementSyntax statement)
    {
        var origin = new SourceOrigin(statement.Location);
        switch (statement)
        {
            case BlockSyntax block: return BindBlock(block);
            case VariableSyntax variable:
                var initializer = BindExpression(variable.Initializer);
                var type = variable.Type is null ? initializer.Type : ResolveType(variable.Type, false);
                initializer = ConvertImplicit(initializer, type);
                if (type == WeftType.Void) diagnostics.Error("WF2003", "Cannot bind a void expression to a variable.", variable.Location);
                var symbol = new VariableSymbol(nextSymbol++, variable.Name, type, variable.Location);
                if (!scope.Declare(symbol)) diagnostics.Error("WF2002", $"Duplicate local '{variable.Name}'.", variable.Location);
                return new IrVariable(symbol, initializer, origin);
            case ReturnSyntax returned:
                var value = returned.Expression is null ? null : BindExpression(returned.Expression);
                if (value?.Type == WeftType.Void) diagnostics.Error("WF2003", "A void return cannot carry an expression; call it before returning.", statement.Location);
                if (value is not null && value.Type != WeftType.Void) value = ConvertImplicit(value, currentFunction.ReturnType);
                else Require(currentFunction.ReturnType, value?.Type ?? WeftType.Void, statement.Location);
                return new IrReturn(value, origin);
            case ExpressionStatementSyntax expression:
                var bound = BindExpression(expression.Expression);
                if (bound.Type != WeftType.Error && bound is not (IrCall or IrIntrinsic or IrAssign)) diagnostics.Error("WF2008", "Only calls or assignments may be expression statements.", expression.Location);
                return new IrExpressionStatement(bound, origin);
            case IfSyntax conditional:
                var condition = BindExpression(conditional.Condition); Require(WeftType.Bool, condition.Type, condition.Origin.Location);
                return new IrIf(condition, BindNestedStatement(conditional.Then), conditional.Else is null ? null : BindNestedStatement(conditional.Else), origin);
            case WhileSyntax loop:
                var test = BindExpression(loop.Condition); Require(WeftType.Bool, test.Type, test.Origin.Location);
                return new IrWhile(test, BindNestedStatement(loop.Body), origin);
            case EffectScopeSyntax effect:
                diagnostics.Error("WF2009", $"'{effect.Kind}' syntax is represented; its semantic/lowering pass is not implemented yet.", effect.Location);
                return new IrBlock([], origin);
            default: throw new InvalidOperationException($"Unhandled syntax: {statement.GetType().Name}");
        }
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
            case NameSyntax name:
                var variable = scope.Lookup(name.Name);
                if (variable is not null) return new IrRead(variable, origin);
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
            case BinarySyntax binary when binary.Operator == "=":
                var assigned = binary.Left is NameSyntax target ? scope.Lookup(target.Name) : null;
                var rhs = BindExpression(binary.Right);
                if (assigned is null) { diagnostics.Error("WF2005", "Assignment requires a declared local or parameter.", binary.Location); return Error(origin); }
                rhs = ConvertImplicit(rhs, assigned.Type);
                return new IrAssign(assigned, rhs, origin);
            case BinarySyntax binary:
                var left = BindExpression(binary.Left); var right = BindExpression(binary.Right);
                var op = binary.Operator;
                if (op == "+" && (left.Type == WeftType.String || right.Type == WeftType.String))
                    return new IrBinary(AsString(left), op, AsString(right), WeftType.String, origin);
                if (IsInteger(left.Type) && IsInteger(right.Type) && left.Type != right.Type)
                {
                    left = ConvertImplicit(left, WeftType.Int64); right = ConvertImplicit(right, WeftType.Int64);
                }
                Require(left.Type, right.Type, binary.Location);
                if (left.Type == WeftType.Void || right.Type == WeftType.Void) diagnostics.Error("WF2003", "A void expression cannot be an operator operand.", binary.Location);
                if (op is "&&" or "||") Require(WeftType.Bool, left.Type, binary.Location);
                else if (op is not ("==" or "!=") && !IsInteger(left.Type)) diagnostics.Error("WF2003", $"Operator '{op}' requires integer operands.", binary.Location);
                var result = op is "==" or "!=" or "<" or ">" or "<=" or ">=" or "&&" or "||" ? WeftType.Bool : left.Type;
                return new IrBinary(left, op, right, result, origin);
            case CallSyntax call: return BindCall(call);
            default:
                diagnostics.Error("WF2009", $"Expression '{syntax.GetType().Name}' is represented but not implemented yet.", syntax.Location);
                return Error(origin);
        }
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
        var arguments = call.Arguments.Select(a => BindExpression(a.Expression)).ToImmutableArray();
        var root = call.Target;
        while (root is MemberSyntax member) root = member.Target;
        if (root is NameSyntax localName && scope.Lookup(localName.Name) is { } local)
        {
            if (call.Target is NameSyntax)
                diagnostics.Error("WF2003", $"Local '{local.Name}' has non-callable type '{local.Type.Name}'.", call.Target.Location);
            else diagnostics.Error("WF2009", $"Member calls on local '{local.Name}' require the Phase 2 member-binding pass.", call.Target.Location);
            return Error(origin);
        }
        if (arguments.Any(a => a.Type == WeftType.Error)) return Error(origin);
        var group = FindFunctions(fullName, out var foundName);
        if (!foundName && (fullName is "Print" or "Log"))
        {
            if (arguments.Length != 1) { diagnostics.Error("WF2006", $"'{fullName}' expects one argument.", call.Location); return Error(origin); }
            if (call.Arguments[0].Name is not (null or "value"))
            { diagnostics.Error("WF2014", $"'{fullName}' has no parameter named '{call.Arguments[0].Name}'.", call.Arguments[0].Location); return Error(origin); }
            intrinsics.Add(Intrinsics.Print);
            return new IrIntrinsic(Intrinsics.Print, [AsString(arguments[0])], origin);
        }
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
        var supplied = arguments.Select((argument, i) => ConvertImplicit(argument, selected.Function.Parameters[selected.Order[i]].Type)).ToImmutableArray().ToBuilder();
        for (var i = supplied.Count; i < selected.Order.Length; i++)
        {
            var parameter = selected.Function.Parameters[selected.Order[i]];
            supplied.Add(new IrConstant(parameter.Default!.Value, parameter.Type, new(parameter.Location, "optional-argument", origin)));
        }
        return new IrCall(selected.Function, supplied.ToImmutable(), origin,
            selected.Order.Where((index, i) => index != i).Any() ? selected.Order : default);
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
    private static bool AlwaysReturns(IrStatement statement) => statement switch
    { IrReturn => true, IrBlock block => block.Statements.Any(AlwaysReturns), IrIf { Else: not null } branch => AlwaysReturns(branch.Then) && AlwaysReturns(branch.Else), _ => false };
}
