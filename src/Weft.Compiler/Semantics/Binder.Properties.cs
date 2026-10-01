using System.Collections.Immutable;
using Weft.Compiler.IR;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public sealed partial class Binder
{
    private readonly Dictionary<string, List<PropertySymbol>> properties = new(StringComparer.Ordinal);

    private void DeclareProperty(PropertySyntax syntax, string ns, string owner)
    {
        var visibility = DeclarationVisibility(syntax.Modifiers, Visibility.Private, true, syntax.Location);
        if (syntax.Modifiers.Any(m => m is not ("public" or "internal" or "private" or "required")) || types[owner].IsStatic)
        { diagnostics.Error("WF2009", "Static and other modified properties remain required declaration work.", syntax.Location); return; }
        if (properties[owner].Any(p => p.Name == syntax.Name))
        { diagnostics.Error("WF2002", $"Duplicate property '{syntax.Name}'.", syntax.Location); return; }
        if (syntax.Accessors.Length == 0 || syntax.Accessors.Any(a => a.Kind is not ("get" or "set" or "init")) ||
            syntax.Accessors.Any(a => a.Kind == "set") && syntax.Accessors.Any(a => a.Kind == "init") ||
            syntax.Accessors.Select(a => a.Kind).Distinct().Count() != syntax.Accessors.Length)
        { diagnostics.Error("WF2024", "A property requires distinct get and/or set/init accessors; set and init cannot coexist.", syntax.Location); return; }
        var automatic = syntax.Accessors.All(a => a.Body is null);
        if (automatic && !syntax.Accessors.Any(a => a.Kind == "get") ||
            !automatic && syntax.Accessors.Any(a => a.Body is null) || !automatic && syntax.Initializer is not null)
        { diagnostics.Error("WF2024", "Auto-properties require a getter and no accessor bodies; only auto-properties can have an initializer.", syntax.Location); return; }
        var restricted = syntax.Accessors.Count(a => a.Modifiers.Length > 0);
        if (restricted > 1 || restricted > 0 && syntax.Accessors.Length != 2)
            diagnostics.Error("WF2024", "Only one accessor of a get/set property may restrict accessibility.", syntax.Location);
        var type = ResolveType(syntax.Type, false);
        FieldSymbol? backing = null;
        if (automatic)
        {
            backing = new(nextSymbol++, "<" + syntax.Name + ">", Nominal(owner), type, syntax.Location,
                Visibility.Private, !syntax.Accessors.Any(a => a.Kind == "set"), syntax.Modifiers.Contains("required"));
            fields[owner].Add((backing, syntax.Initializer));
        }
        FunctionSymbol? getter = null, setter = null;
        foreach (var accessor in syntax.Accessors)
        {
            var access = DeclarationVisibility(accessor.Modifiers, visibility, true, accessor.Location);
            if (accessor.Modifiers.Any(m => m is not ("public" or "internal" or "private")) || accessor.Modifiers.Length > 0 && access >= visibility)
                diagnostics.Error("WF2024", "Accessor accessibility must be more restrictive than its property.", accessor.Location);
            var get = accessor.Kind == "get";
            var resultType = get ? syntax.Type : new TypeSyntax("void", [], 0, false, accessor.Location);
            ImmutableArray<ParameterSyntax> parameters = get ? [] : [new(syntax.Type, "value", accessor.Location)];
            var body = accessor.Body;
            if (automatic)
            {
                var field = new NameSyntax(backing!.Name, accessor.Location);
                StatementSyntax statement = get ? new ReturnSyntax(field, accessor.Location)
                    : new ExpressionStatementSyntax(new BinarySyntax(field, "=", new NameSyntax("value", accessor.Location), accessor.Location), accessor.Location);
                body = new([statement], accessor.Location);
            }
            var name = "<" + accessor.Kind + ":" + syntax.Name + ">";
            Declare([new FunctionSyntax(name, resultType, parameters, body, [access.ToString().ToLowerInvariant()], accessor.Location, IsInitAccessor: accessor.Kind == "init")], ns, owner);
            var symbol = functions[Qualify(owner, name)][0];
            if (get) getter = symbol; else setter = symbol;
        }
        properties[owner].Add(new(syntax.Name, Nominal(owner), type, syntax.Location, visibility, getter, setter, backing, syntax.Accessors.Any(a => a.Kind == "init"), syntax.Modifiers.Contains("required")));
    }

    private IrExpression ReadProperty(PropertySymbol property, IrExpression receiver, SourceOrigin origin)
    {
        if (property.Getter is null)
        { diagnostics.Error("WF2023", $"Property '{property.Name}' has no getter.", origin.Location); return Error(origin); }
        CheckAccessor(property.Getter, origin);
        if (IsThis(receiver) && currentFunction.IsConstructor && property.BackingField is { } backing)
        { RequireFieldInitialized(backing, receiver, origin.Location); return new IrFieldRead(backing, receiver, origin); }
        if (IsThis(receiver)) RequireInitialized(origin.Location);
        return new IrCall(property.Getter, [], origin, Receiver: receiver);
    }

    private void CheckAccessor(FunctionSymbol accessor, SourceOrigin origin)
    {
        if (accessor.Visibility == Visibility.Private && accessor.ContainingType != currentFunction.ContainingType)
            diagnostics.Error("WF2011", "Property accessor is inaccessible from this location.", origin.Location);
    }
}
