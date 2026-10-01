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
    private void RequireInitialized(SourceLocation location)
    {
        if (!currentFunction.IsConstructor) return;
        var missing = fields[currentFunction.ContainingType!].Where(f => f.Symbol.Type.Kind is TypeKind.String or TypeKind.Nominal && !initializedFields.Contains(f.Symbol.Id)).Select(f => f.Symbol.Name).ToArray();
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
            currentFunction.ContainingType is { } owner && fields[owner].Any(f => f.Symbol.Name == name.Name)) { qualifier = null; return false; }
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
    private IrExpression BindNew(NewSyntax syntax)
    {
        var type = ResolveType(syntax.Type, false);
        if (type.Kind != TypeKind.Nominal || !types.TryGetValue(type.Name, out var symbol) || symbol.IsStatic)
        { diagnostics.Error("WF2020", "new requires an ordinary class type.", syntax.Location); return Error(new(syntax.Location)); }
        var arguments = syntax.Arguments.Select(a => BindExpression(a.Expression)).ToImmutableArray();
        var name = type.Name + "..ctor";
        return BindInvocation(new(new NameSyntax(name, syntax.Location), syntax.Arguments, syntax.Location), name,
            functions.GetValueOrDefault(name) ?? [], arguments);
    }
    private IrFieldRead? BindImplicitField(string name, SourceLocation location)
    {
        if (currentFunction.ContainingType is not { } owner) return null;
        var field = fields[owner].FirstOrDefault(f => f.Symbol.Name == name).Symbol;
        if (field is null) return null;
        if (currentFunction.Receiver is null || bindingFieldInitializer)
        { diagnostics.Error("WF2020", "An instance field requires an object; field initializers cannot access this.", location); return null; }
        return new(field, This(new(location)), new(location));
    }
    private IrFieldRead? BindField(MemberSyntax member)
    {
        var receiver = member.Target is NameSyntax { Name: "this" } && currentFunction.Receiver is not null && !bindingFieldInitializer
            ? This(new(member.Target.Location)) : BindExpression(member.Target);
        if (!fields.TryGetValue(receiver.Type.Name, out var members))
        { if (receiver.Type != WeftType.Error) diagnostics.Error("WF2001", $"Type '{receiver.Type.Name}' has no field '{member.Member}'.", member.Location); return null; }
        var field = members.FirstOrDefault(f => f.Symbol.Name == member.Member).Symbol;
        if (field is null) { diagnostics.Error("WF2001", $"Unknown field '{member.Member}'.", member.Location); return null; }
        if (field.Visibility == Visibility.Private && field.Owner.Name != currentFunction.ContainingType)
            diagnostics.Error("WF2011", $"Field '{field.Name}' is inaccessible from this location.", member.Location);
        return new(field, receiver, new(member.Location));
    }
    private IrExpression? BindDestination(ExpressionSyntax syntax)
    {
        if (syntax is NameSyntax name)
        {
            if (name.Name == "this") return null;
            if (scope.Lookup(name.Name) is { } local) return new IrRead(local, new(syntax.Location));
            return BindImplicitField(name.Name, name.Location);
        }
        return syntax is MemberSyntax member ? BindField(member) : null;
    }
    private void CheckWritable(IrFieldRead field)
    {
        if (field.Field.ReadOnly && !(currentFunction.IsConstructor && IsThis(field.Receiver) && field.Field.Owner.Name == currentFunction.ContainingType))
            diagnostics.Error("WF2021", $"Readonly field '{field.Field.Name}' can be assigned only by its constructor.", field.Origin.Location);
    }
    private IrExpression BindAssignment(BinarySyntax syntax)
    {
        var origin = new SourceOrigin(syntax.Location);
        var destination = BindDestination(syntax.Left);
        if (destination is IrFieldRead read)
        {
            CheckWritable(read);
            if (syntax.Operator != "=") RequireFieldInitialized(read.Field, read.Receiver, syntax.Location);
        }
        var value = BindExpression(syntax.Right);
        if (destination is null) { diagnostics.Error("WF2005", "Assignment requires a writable local, parameter, or field.", syntax.Location); return Error(origin); }
        if (destination is IrRead local)
        {
            if (syntax.Operator != "=") value = BindBinary(local, syntax.Operator[..1], value, origin);
            return new IrAssign(local.Symbol, ConvertImplicit(value, local.Type), origin);
        }
        var field = (IrFieldRead)destination;
        if (IsThis(field.Receiver)) initializedFields.Add(field.Field.Id);
        if (syntax.Operator == "=") return new IrFieldWrite(field.Field, field.Receiver, ConvertImplicit(value, field.Type), origin);
        if (IsThis(field.Receiver))
            return new IrFieldWrite(field.Field, field.Receiver,
                ConvertImplicit(BindBinary(field, syntax.Operator[..1], value, origin), field.Type), origin);
        // Freeze both destination and old value before any effects in the RHS.
        var target = new VariableSymbol(nextSymbol++, "<field-target>", field.Receiver.Type, syntax.Left.Location);
        var old = new VariableSymbol(nextSymbol++, "<field-old>", field.Type, syntax.Left.Location);
        var receiver = new IrRead(target, origin);
        var result = ConvertImplicit(BindBinary(new IrRead(old, origin), syntax.Operator[..1], value, origin), field.Type);
        return new IrSequence([new(target, field.Receiver, new(syntax.Left.Location, "assignment-target")),
            new(old, new IrFieldRead(field.Field, receiver, origin), new(syntax.Left.Location, "assignment-old-value"))],
            new IrFieldWrite(field.Field, receiver, result, origin), origin);
    }
    private IrExpression BindUpdate(UpdateSyntax syntax)
    {
        var origin = new SourceOrigin(syntax.Location);
        var destination = BindDestination(syntax.Operand);
        if (destination is null) { diagnostics.Error("WF2005", "Increment/decrement requires a writable local, parameter, or field.", syntax.Location); return Error(origin); }
        if (!IsInteger(destination.Type)) diagnostics.Error("WF2003", "Increment/decrement currently requires int32 or int64.", syntax.Location);
        if (destination is IrRead local) return new IrUpdate(local.Symbol, syntax.Operator, syntax.Postfix, origin);
        var field = (IrFieldRead)destination; CheckWritable(field);
        return new IrFieldUpdate(field.Field, field.Receiver, syntax.Operator, syntax.Postfix, origin);
    }
}
