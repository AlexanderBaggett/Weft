using System.Collections.Immutable;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public sealed partial class Binder
{
    private readonly Dictionary<string, List<(FieldSymbol Symbol, ExpressionSyntax? Initializer)>> fields = new(StringComparer.Ordinal);

    private void CollectTypes(ImmutableArray<DeclarationSyntax> declarations, string ns)
    {
        foreach (var declaration in declarations)
        {
            if (declaration is NamespaceSyntax space)
            {
                var name = Qualify(ns, space.Name); RegisterNamespace(name, space.Location);
                if (space.FileScoped) ns = name; else CollectTypes(space.Members, name);
            }
            else if (declaration is ClassSyntax type)
            {
                var visibility = DeclarationVisibility(type.Modifiers, Visibility.Internal, false, type.Location);
                foreach (var modifier in type.Modifiers.Where(m => m is not ("public" or "internal" or "private" or "static")))
                    diagnostics.Error("WF2012", $"Modifier '{modifier}' is invalid on a class.", type.Location);
                var name = Qualify(ns, type.Name);
                if (!types.TryAdd(name, new(name, type.Location, visibility, type.Modifiers.Contains("static"))) || namespaces.Contains(name))
                    diagnostics.Error("WF2002", $"Duplicate declaration '{name}'.", type.Location);
                fields.TryAdd(name, []); properties.TryAdd(name, []);
            }
        }
    }

    private void Declare(ImmutableArray<DeclarationSyntax> declarations, string ns, string? owner = null)
    {
        foreach (var declaration in declarations)
        {
            currentNamespace = ns;
            if (declaration is NamespaceSyntax space)
            {
                if (owner is not null) { diagnostics.Error("WF2012", "A namespace cannot be declared inside a class.", space.Location); continue; }
                var name = Qualify(ns, space.Name); RegisterNamespace(name, space.Location);
                if (space.FileScoped) ns = name; else Declare(space.Members, name);
                continue;
            }
            if (declaration is ClassSyntax type)
            {
                if (owner is not null) { diagnostics.Error("WF2009", "Nested types remain required declaration work.", type.Location); continue; }
                var typeName = Qualify(ns, type.Name);
                Declare(type.Members, ns, typeName);
                if (!types[typeName].IsStatic && !type.Members.OfType<FunctionSyntax>().Any(f => f.IsConstructor))
                {
                    var constructor = new FunctionSyntax(type.Name, new("void", [], 0, false, type.Location), [],
                        new([], type.Location), ["public"], type.Location, true);
                    Declare([constructor], ns, typeName);
                }
                continue;
            }
            if (declaration is FieldSyntax field && owner is not null)
            {
                var visibility = DeclarationVisibility(field.Modifiers, Visibility.Private, true, field.Location);
                foreach (var modifier in field.Modifiers.Where(m => m is not ("public" or "internal" or "private" or "readonly" or "required")))
                    diagnostics.Error("WF2009", $"Field modifier '{modifier}' requires a later declaration pass.", field.Location);
                if (types[owner].IsStatic) diagnostics.Error("WF2012", "An instance field cannot belong to a static class.", field.Location);
                if (fields[owner].Any(f => f.Symbol.Name == field.Name)) diagnostics.Error("WF2002", $"Duplicate field '{field.Name}'.", field.Location);
                fields[owner].Add((new(nextSymbol++, field.Name, Nominal(owner), ResolveType(field.Type, false), field.Location, visibility, field.Modifiers.Contains("readonly"), field.Modifiers.Contains("required")), field.Initializer));
                continue;
            }
            if (declaration is ConstructSyntax construct)
            {
                diagnostics.Error("WF2009", $"'{construct.Kind}' syntax is represented, but its semantic/lowering pass is not implemented yet. This remains required release work.", construct.Location);
                continue;
            }
            if (declaration is PropertySyntax property)
            {
                if (owner is not null) DeclareProperty(property, ns, owner);
                else diagnostics.Error("WF2012", "A property must belong to a class.", property.Location);
                continue;
            }
            if (declaration is not FunctionSyntax function) continue;
            var visibilityFunction = DeclarationVisibility(function.Modifiers, owner is null ? Visibility.Internal : Visibility.Private, owner is not null, function.Location);
            if (owner is not null && types[owner].IsStatic && !function.Modifiers.Contains("static"))
                diagnostics.Error("WF2012", "A method in a static class must be declared static.", function.Location);
            if (function.Modifiers.Any(m => m is "readonly" or "required") || function.IsConstructor && function.Modifiers.Any(m => m is not ("public" or "internal" or "private")))
                diagnostics.Error("WF2012", "Invalid method or constructor modifier.", function.Location);
            var parameters = ImmutableArray.CreateBuilder<VariableSymbol>();
            var optionalSeen = false;
            foreach (var parameter in function.Parameters)
            {
                if (parameter.Name == "this") diagnostics.Error("WF2012", "A parameter cannot be named this.", parameter.Location);
                var parameterType = ResolveType(parameter.Type, false);
                ConstantValue? defaultValue = null;
                if (parameter.Default is not null)
                {
                    optionalSeen = true; defaultValue = ConstantEvaluator.Evaluate(parameter.Default);
                    if (defaultValue is null) diagnostics.Error("WF2013", "A parameter default must be a supported constant without overflow or division by zero.", parameter.Default.Location);
                    else if (defaultValue.Type == WeftType.Int32 && parameterType == WeftType.Int64) defaultValue = new((long)(int)defaultValue.Value, WeftType.Int64);
                    else Require(parameterType, defaultValue.Type, parameter.Default.Location);
                }
                else if (optionalSeen) diagnostics.Error("WF2013", "Required parameters must precede optional parameters.", parameter.Location);
                parameters.Add(new(nextSymbol++, parameter.Name, parameterType, parameter.Location, defaultValue));
            }
            var instance = owner is not null && !types[owner].IsStatic && !function.Modifiers.Contains("static");
            var receiver = instance ? new VariableSymbol(nextSymbol++, "this", Nominal(owner!), function.Location) : null;
            var returned = function.IsConstructor ? Nominal(owner!) : ResolveType(function.ReturnType, true);
            var symbol = new FunctionSymbol(nextSymbol++, Qualify(owner ?? ns, function.IsConstructor ? ".ctor" : function.Name), returned,
                parameters.ToImmutable(), function.Location, visibilityFunction, owner, receiver, function.IsConstructor, function.IsInitAccessor);
            if (!functions.TryGetValue(symbol.Name, out var overloads)) functions.Add(symbol.Name, overloads = []);
            var duplicate = overloads.FirstOrDefault(other => other.Parameters.Select(p => p.Type).SequenceEqual(symbol.Parameters.Select(p => p.Type)));
            if (duplicate is not null || types.ContainsKey(symbol.Name) || namespaces.Contains(symbol.Name))
                diagnostics.Add(new("WF2002", $"Duplicate function signature '{symbol.Name}'. Return types, parameter names, and defaults do not distinguish overloads.", function.Location,
                    Related: duplicate is null ? null : [duplicate.Location]));
            else { overloads.Add(symbol); bodies.Add((function, symbol, ns)); }
        }
    }

    private static WeftType Nominal(string name) => new(TypeKind.Nominal, name);
    private TypeSymbol? FindType(string name)
    {
        var ns = currentNamespace;
        var first = name.Split('.')[0];
        while (true)
        {
            if (types.ContainsKey(Qualify(ns, first)) || namespaces.Contains(Qualify(ns, first)))
                return types.GetValueOrDefault(Qualify(ns, name));
            if (ns.Length == 0) return null;
            var dot = ns.LastIndexOf('.'); ns = dot < 0 ? "" : ns[..dot];
        }
    }
    private WeftType ResolveType(TypeSyntax syntax, bool allowVoid)
    {
        var named = FindType(syntax.Name);
        var type = WeftType.Builtin(syntax.Name) ?? (named is null ? null : Nominal(named.Name));
        if (type is null) { diagnostics.Error("WF2001", $"Unknown or not-yet-bound type '{syntax.Name}'.", syntax.Location); return WeftType.Error; }
        if (named?.IsStatic == true) diagnostics.Error("WF2020", "A static class cannot be used as a value type.", syntax.Location);
        if (syntax.Arguments.Length > 0 || syntax.ArrayRank > 0 || syntax.Nullable ||
            type.Kind is not (TypeKind.Void or TypeKind.Bool or TypeKind.Int32 or TypeKind.Int64 or TypeKind.String or TypeKind.Nominal))
            diagnostics.Error("WF2009", $"Execution of type '{syntax.Name}' with these refinements is Phase 2 work and is not implemented yet.", syntax.Location);
        if (!allowVoid && type == WeftType.Void) { diagnostics.Error("WF2003", "A value cannot have type void.", syntax.Location); return WeftType.Error; }
        return type;
    }

    private void ValidatePublicContracts()
    {
        void Exposed(WeftType type, SourceLocation location)
        {
            if (type.Kind == TypeKind.Nominal && types.TryGetValue(type.Name, out var symbol) && symbol.Visibility != Visibility.Public)
                diagnostics.Error("WF2019", $"Public contract exposes non-public type '{type.Name}'.", location);
        }
        foreach (var function in functions.Values.SelectMany(f => f))
        {
            if (function.ContainingType is { } owner && fields[owner].Any(f => Qualify(owner, f.Symbol.Name) == function.Name))
                diagnostics.Error("WF2002", $"Method '{function.Name}' conflicts with a field.", function.Location);
            if (function.Visibility != Visibility.Public || function.ContainingType is { } containing && types[containing].Visibility != Visibility.Public) continue;
            Exposed(function.ReturnType, function.Location);
            foreach (var parameter in function.Parameters) Exposed(parameter.Type, parameter.Location);
        }
        foreach (var pair in properties)
            foreach (var property in pair.Value)
            {
                if (fields[pair.Key].Any(f => f.Symbol.Name == property.Name) || functions.ContainsKey(Qualify(pair.Key, property.Name)))
                    diagnostics.Error("WF2002", $"Property '{property.Name}' conflicts with another member.", property.Location);
                if (types[pair.Key].Visibility == Visibility.Public && property.Visibility == Visibility.Public) Exposed(property.Type, property.Location);
            }
        foreach (var pair in fields)
            if (types[pair.Key].Visibility == Visibility.Public)
                foreach (var (field, _) in pair.Value.Where(f => f.Symbol.Visibility == Visibility.Public)) Exposed(field.Type, field.Location);
    }
}
