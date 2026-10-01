using System.Collections.Immutable;
using Weft.Compiler.IR;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public sealed partial class Binder
{
    private HashSet<int> initializedFields = [];
    private bool bindingFieldInitializer;
    private IrRead This(SourceOrigin origin) => new(currentFunction.Receiver!, origin);
    private bool IsThis(IrExpression receiver) => receiver is IrRead read && read.Symbol == currentFunction.Receiver;
    private void RequireInitialized(SourceLocation location, bool completing = false)
    {
        if (!currentFunction.IsConstructor) return;
        var missing = fields[currentFunction.ContainingType!].Where(f => f.Symbol.Type.Kind is TypeKind.String or TypeKind.Nominal && !initializedFields.Contains(f.Symbol.Id) && !(completing && f.Symbol.Required && !currentFunction.IsCopyConstructor)).Select(f => f.Symbol.Name).ToArray();
        if (missing.Length > 0) diagnostics.Error("WF2022", "Constructor must initialize non-null fields before this escapes: " + string.Join(", ", missing) + ".", location);
    }
    private void RequireFieldInitialized(FieldSymbol field, IrExpression receiver, SourceLocation location)
    {
        if (currentFunction.IsConstructor && IsThis(receiver) && field.Type.Kind is TypeKind.String or TypeKind.Nominal && !initializedFields.Contains(field.Id))
            diagnostics.Error("WF2022", $"Field '{field.Name}' is read before it is initialized.", location);
    }
    private bool IsStaticQualifier(ExpressionSyntax target, out string? qualifier)
    {
        var root = target;
        while (root is MemberSyntax member) root = member.Target;
        if (root is not NameSyntax name || name.Name == "this" || scope.Lookup(name.Name) is not null ||
            currentFunction.ContainingType is { } owner && MemberType(owner, name.Name) is not null) { qualifier = null; return false; }
        var nameText = NameOf(target);
        var type = FindType(nameText);
        if (type is not null) { qualifier = type.Name; return true; }
        var ns = currentNamespace;
        while (true)
        {
            var candidate = Qualify(ns, nameText);
            if (namespaces.Contains(candidate)) { qualifier = candidate; return true; }
            if (types.ContainsKey(Qualify(ns, name.Name)) || namespaces.Contains(Qualify(ns, name.Name)) || ns.Length == 0) break;
            var dot = ns.LastIndexOf('.'); ns = dot < 0 ? "" : ns[..dot];
        }
        qualifier = null; return false;
    }
    private sealed record BoundTarget(VariableSymbol? Local, FieldSymbol? Field, PropertySymbol? Property,
        IrExpression? Receiver, SourceOrigin Origin, bool Initializing = false)
    {
        public WeftType Type => Local?.Type ?? Field?.Type ?? Property!.Type;
    }
    private WeftType? MemberType(string owner, string name) =>
        fields[owner].FirstOrDefault(f => f.Symbol.Name == name).Symbol?.Type ?? properties[owner].FirstOrDefault(p => p.Name == name)?.Type;

    private BoundTarget? BindImplicitMember(string name, SourceLocation location)
    {
        if (currentFunction.ContainingType is not { } owner || MemberType(owner, name) is null) return null;
        if (currentFunction.Receiver is null || bindingFieldInitializer)
        { diagnostics.Error("WF2020", "An instance member requires an object; initializers cannot access this.", location); return null; }
        return SelectMember(owner, name, This(new(location)), new(location));
    }
    private BoundTarget? BindMember(MemberSyntax member)
    {
        var receiver = member.Target is NameSyntax { Name: "this" } && currentFunction.Receiver is not null && !bindingFieldInitializer
            ? This(new(member.Target.Location)) : BindExpression(member.Target);
        if (!fields.ContainsKey(receiver.Type.Name) || MemberType(receiver.Type.Name, member.Member) is null)
        { if (receiver.Type != WeftType.Error) diagnostics.Error("WF2001", $"Type '{receiver.Type.Name}' has no field or property '{member.Member}'.", member.Location); return null; }
        return SelectMember(receiver.Type.Name, member.Member, receiver, new(member.Location));
    }
    private BoundTarget SelectMember(string owner, string name, IrExpression receiver, SourceOrigin origin)
    {
        var field = fields[owner].FirstOrDefault(f => f.Symbol.Name == name).Symbol;
        var property = properties[owner].FirstOrDefault(p => p.Name == name);
        if ((field?.Visibility ?? property!.Visibility) == Visibility.Private && owner != currentFunction.ContainingType)
            diagnostics.Error("WF2011", $"Member '{name}' is inaccessible from this location.", origin.Location);
        return new(null, field, property, receiver, origin);
    }
    private BoundTarget? BindDestination(ExpressionSyntax syntax)
    {
        if (syntax is NameSyntax name)
        {
            if (name.Name == "this") return null;
            if (scope.Lookup(name.Name) is { } local) return new(local, null, null, null, new(syntax.Location));
            return BindImplicitMember(name.Name, name.Location);
        }
        return syntax is MemberSyntax member ? BindMember(member) : null;
    }
    private IrExpression ReadTarget(BoundTarget target)
    {
        if (target.Local is { } local) return new IrRead(local, target.Origin);
        if (target.Field is { } field)
        { RequireFieldInitialized(field, target.Receiver!, target.Origin.Location); return new IrFieldRead(field, target.Receiver!, target.Origin); }
        return ReadProperty(target.Property!, target.Receiver!, target.Origin);
    }
    private BoundTarget CheckWritable(BoundTarget target)
    {
        if (target.Property is { } property)
        {
            if (property.InitOnly && !target.Initializing && !(IsThis(target.Receiver!) && (currentFunction.IsConstructor || currentFunction.IsInitAccessor)))
                diagnostics.Error("WF2025", $"Init-only property '{property.Name}' can be assigned only during construction.", target.Origin.Location);
            if (property.BackingField is { } backing && currentFunction.IsConstructor && IsThis(target.Receiver!))
                target = target with { Property = null, Field = backing };
            else if (property.Setter is null)
                diagnostics.Error("WF2023", $"Property '{property.Name}' has no setter.", target.Origin.Location);
            else CheckAccessor(property.Setter, target.Origin);
        }
        if (target.Field is { ReadOnly: true } field && !((currentFunction.IsConstructor || currentFunction.IsInitAccessor) && IsThis(target.Receiver!) && field.Owner.Name == currentFunction.ContainingType))
            diagnostics.Error("WF2021", $"Readonly field '{field.Name}' can be assigned only through this in its constructor or an init accessor.", target.Origin.Location);
        return target;
    }
    private IrExpression WriteTarget(BoundTarget target, IrExpression value, SourceOrigin origin)
    {
        value = ConvertImplicit(value, target.Type);
        if (target.Local is { } local) return new IrAssign(local, value, origin);
        if (target.Field is { } field)
        {
            if (IsThis(target.Receiver!)) initializedFields.Add(field.Id);
            return new IrFieldWrite(field, target.Receiver!, value, origin);
        }
        if (IsThis(target.Receiver!)) RequireInitialized(origin.Location);
        return target.Property!.Setter is { } setter ? new IrSetterCall(setter, target.Receiver!, value, origin) : Error(origin);
    }
    private BoundTarget CaptureReceiver(BoundTarget target, ImmutableArray<IrVariable>.Builder bindings)
    {
        if (target.Receiver is null || IsThis(target.Receiver)) return target;
        var temporary = new VariableSymbol(nextSymbol++, "<member-target>", target.Receiver.Type, target.Origin.Location);
        bindings.Add(new(temporary, target.Receiver, new(target.Origin.Location, "assignment-target", target.Origin)));
        return target with { Receiver = new IrRead(temporary, target.Origin) };
    }
    private IrExpression BindAssignment(BinarySyntax syntax)
    {
        var origin = new SourceOrigin(syntax.Location);
        var target = BindDestination(syntax.Left);
        if (target is null)
        { BindExpression(syntax.Right); diagnostics.Error("WF2005", "Assignment requires a writable local, parameter, field, or property.", syntax.Location); return Error(origin); }
        target = CheckWritable(target);
        var bindings = ImmutableArray.CreateBuilder<IrVariable>();
        IrExpression? old = null;
        if (syntax.Operator != "=")
        {
            var original = target;
            // Check construction before replacing this/receiver with a generated temporary.
            var read = ReadTarget(original);
            target = CaptureReceiver(target, bindings);
            old = target == original ? read : ReadTarget(target);
            if (target.Property is not null || bindings.Count > 0)
            {
                var temporary = new VariableSymbol(nextSymbol++, "<member-old>", target.Type, syntax.Location);
                bindings.Add(new(temporary, old, new(syntax.Location, "assignment-old-value", origin)));
                old = new IrRead(temporary, origin);
            }
        }
        var value = BindExpression(syntax.Right);
        if (old is not null) value = BindBinary(old, syntax.Operator[..1], value, origin);
        var result = WriteTarget(target, value, origin);
        return bindings.Count == 0 ? result : new IrSequence(bindings.ToImmutable(), result, origin);
    }
    private IrExpression BindUpdate(UpdateSyntax syntax)
    {
        var origin = new SourceOrigin(syntax.Location);
        var target = BindDestination(syntax.Operand);
        if (target is null) { diagnostics.Error("WF2005", "Increment/decrement requires a writable local, parameter, field, or property.", syntax.Location); return Error(origin); }
        target = CheckWritable(target);
        if (!IsInteger(target.Type)) diagnostics.Error("WF2003", "Increment/decrement currently requires int32 or int64.", syntax.Location);
        if (target.Local is { } local) return new IrUpdate(local, syntax.Operator, syntax.Postfix, origin);
        if (target.Field is { } field) return new IrFieldUpdate(field, target.Receiver!, syntax.Operator, syntax.Postfix, origin);
        var read = ReadTarget(target);
        var original = target;
        var bindings = ImmutableArray.CreateBuilder<IrVariable>();
        target = CaptureReceiver(target, bindings);
        var old = new VariableSymbol(nextSymbol++, "<property-old>", target.Type, syntax.Location);
        bindings.Add(new(old, target == original ? read : ReadTarget(target), origin));
        var previous = new IrRead(old, origin);
        var result = WriteTarget(target, BindBinary(previous, syntax.Operator == "++" ? "+" : "-", new IrConstant(1, WeftType.Int32, origin), origin), origin);
        if (syntax.Postfix)
        {
            var ignored = new VariableSymbol(nextSymbol++, "<property-update>", result.Type, syntax.Location);
            bindings.Add(new(ignored, result, origin));
            result = previous;
        }
        return new IrSequence(bindings.ToImmutable(), result, origin);
    }
}
