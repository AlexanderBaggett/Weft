using System.Collections.Immutable;
using Weft.Compiler.IR;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public sealed partial class Binder
{
    private readonly List<IrFunction> recordOperations = [];

    private ImmutableArray<DeclarationSyntax> ExpandDataMembers(ClassSyntax type)
    {
        if (type.Parameters.IsDefault) return type.Members;
        var members = ImmutableArray.CreateBuilder<DeclarationSyntax>();
        foreach (var parameter in type.Parameters)
        {
            var explicitMember = type.Members.FirstOrDefault(m => m.Name == parameter.Name);
            if (explicitMember is null)
                members.Add(new PropertySyntax(parameter.Name, parameter.Type,
                    [new("get", null, [], parameter.Location), new("init", null, [], parameter.Location)],
                    new NameSyntax(parameter.Name, parameter.Location), ["public"], parameter.Location));
            else
            {
                var memberType = explicitMember switch { FieldSyntax field => field.Type, PropertySyntax declaredProperty => declaredProperty.Type, _ => null };
                if (memberType is null || ResolveType(memberType, false) != ResolveType(parameter.Type, false) ||
                    explicitMember is FieldSyntax { Modifiers: var fieldModifiers } && fieldModifiers.Contains("static") ||
                    explicitMember is PropertySyntax property && (property.Modifiers.Contains("static") || !property.Accessors.Any(a => a.Kind == "get")))
                    diagnostics.Error("WF2031", $"Positional member '{parameter.Name}' must be a readable instance field/property of the parameter type.", explicitMember.Location);
            }
        }
        foreach (var constructor in type.Members.OfType<FunctionSyntax>().Where(f => f.IsConstructor && !f.Modifiers.Contains("static")))
            if (constructor.Initializer?.Kind != "this" && !IsRecordCopyConstructor(constructor, Nominal(Qualify(currentNamespace, type.Name))))
                diagnostics.Error("WF2031", "A non-copy constructor in a positional record must have a this(...) initializer.", constructor.Location);
        members.AddRange(type.Members);
        members.Add(new FunctionSyntax(type.Name, new("void", [], 0, false, type.Location), type.Parameters,
            new([], type.Location), ["public"], type.Location, IsConstructor: true, IsPrimaryConstructor: true));
        return members.ToImmutable();
    }

    private bool IsRecordCopyConstructor(FunctionSyntax function, WeftType owner) => function.IsConstructor &&
        !function.Modifiers.Contains("static") && !function.IsPrimaryConstructor && function.Parameters.Length == 1 && ResolveType(function.Parameters[0].Type, false) == owner;

    private void DeclareRecordOperations()
    {
        foreach (var type in types.Values.Where(t => t.Kind == DataKind.Record))
        {
            var nominal = Nominal(type.Name);
            var origin = new SourceOrigin(type.Location, "record-value-semantics");
            var receiver = new VariableSymbol(nextSymbol++, "this", nominal, type.Location);
            var other = new VariableSymbol(nextSymbol++, "other", nominal, type.Location);
            Add("Equals", WeftType.Bool, [other], new IrBinary(new IrRead(receiver, origin), "==", new IrRead(other, origin), WeftType.Bool, origin));
            Add("GetHashCode", WeftType.Int32, [], new IrObjectHash(new IrRead(receiver, origin), origin));
            void Add(string name, WeftType result, ImmutableArray<VariableSymbol> parameters, IrExpression value)
            {
                var qualified = Qualify(type.Name, name);
                if (!functions.TryGetValue(qualified, out var group)) functions.Add(qualified, group = []);
                if (group.Any(f => f.Parameters.Select(p => p.Type).SequenceEqual(parameters.Select(p => p.Type))))
                {
                    diagnostics.Error("WF2009", "Custom record equality/hash overrides require the remaining record customization pass.", type.Location);
                    return;
                }
                var symbol = new FunctionSymbol(nextSymbol++, qualified, result, parameters, type.Location,
                    Visibility.Public, type.Name, receiver);
                group.Add(symbol); recordOperations.Add(new(symbol, new([new IrReturn(value, origin)], origin), origin));
            }
        }
    }

    private IrExpression BindWith(WithSyntax syntax)
    {
        var origin = new SourceOrigin(syntax.Location, "with-copy");
        var receiver = BindExpression(syntax.Receiver);
        if (receiver.Type.Kind != TypeKind.Nominal || !types.TryGetValue(receiver.Type.Name, out var type) ||
            type.Kind is not (DataKind.Record or DataKind.Model))
        { diagnostics.Error("WF2030", "with requires a record or model value.", syntax.Location); return Error(origin); }
        if (syntax.Initializers.Any(m => m.Value is null))
            diagnostics.Error("WF2030", "A with initializer requires a value expression for each member; use a nested with expression to copy a referenced member.", syntax.Location);
        var copy = new VariableSymbol(nextSymbol++, "<copy>", receiver.Type, syntax.Location);
        var bindings = ImmutableArray.CreateBuilder<IrVariable>();
        bindings.Add(new(copy, new IrCopy(receiver, origin), origin));
        // The source is already initialized. Copying preserves required storage and
        // grants init writes only to the new object's identity.
        BindMemberInitializers(new IrRead(copy, origin), syntax.Initializers, bindings, [], direct: true);
        return new IrSequence(bindings.ToImmutable(), new IrRead(copy, origin), origin, copy);
    }
}
