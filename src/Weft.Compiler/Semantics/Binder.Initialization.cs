using System.Collections.Immutable;
using Weft.Compiler.IR;
using Weft.Compiler.Syntax;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public sealed partial class Binder
{
    private void ValidateRequiredMembers()
    {
        foreach (var pair in fields)
            foreach (var (field, _) in pair.Value.Where(f => f.Symbol.Required && !f.Symbol.Name.StartsWith('<')))
                if (field.ReadOnly || field.Visibility < types[pair.Key].Visibility)
                    diagnostics.Error("WF2027", $"Required field '{field.Name}' must be writable and at least as visible as its class.", field.Location);
        foreach (var pair in properties)
            foreach (var property in pair.Value.Where(p => p.Required))
                if (property.Setter is null || property.Visibility < types[pair.Key].Visibility || property.Setter.Visibility < types[pair.Key].Visibility)
                    diagnostics.Error("WF2027", $"Required property '{property.Name}' needs a set/init accessor at least as visible as its class.", property.Location);
    }

    private IrExpression BindNew(NewSyntax syntax)
    {
        var origin = new SourceOrigin(syntax.Location);
        var type = ResolveType(syntax.Type, false);
        if (type.Kind != TypeKind.Nominal || !types.TryGetValue(type.Name, out var symbol) || symbol.IsStatic)
        { diagnostics.Error("WF2020", "new requires an ordinary class type.", syntax.Location); return Error(origin); }
        var arguments = syntax.Arguments.Select(a => BindExpression(a.Expression)).ToImmutableArray();
        var name = type.Name + "..ctor";
        var constructor = BindInvocation(new(new NameSyntax(name, syntax.Location), syntax.Arguments, syntax.Location), name,
            functions.GetValueOrDefault(name) ?? [], arguments);
        if (constructor.Type == WeftType.Error) return constructor;
        var required = fields[type.Name].Where(f => f.Symbol.Required && !f.Symbol.Name.StartsWith('<')).Select(f => f.Symbol.Name)
            .Concat(properties[type.Name].Where(p => p.Required).Select(p => p.Name)).ToHashSet(StringComparer.Ordinal);
        var initializers = syntax.Initializers.IsDefault ? [] : syntax.Initializers;
        required.ExceptWith(initializers.Where(m => m.Value is not null).Select(m => m.Name));
        if (required.Count > 0)
            diagnostics.Error("WF2026", "Object initializer must assign required members: " + string.Join(", ", required.Order()) + ".", syntax.Location);
        if (initializers.Length == 0) return constructor;

        var instance = new VariableSymbol(nextSymbol++, "<initializing>", type, syntax.Location);
        var bindings = ImmutableArray.CreateBuilder<IrVariable>();
        bindings.Add(new(instance, constructor, new(syntax.Location, "object-construction", origin)));
        // Required reference storage can be incomplete until these source-ordered writes.
        // Calling user code on that object must wait until all of it is populated.
        var pending = fields[type.Name].Where(f => f.Symbol.Required && f.Initializer is null &&
            f.Symbol.Type.Kind is TypeKind.String or TypeKind.Nominal).Select(f => f.Symbol.Id).ToHashSet();
        BindMemberInitializers(new IrRead(instance, origin), initializers, bindings, pending, direct: true);
        return new IrSequence(bindings.ToImmutable(), new IrRead(instance, origin), origin, instance);
    }

    private static bool HasMemberAssignments(ImmutableArray<MemberInitializerSyntax> members) =>
        members.Any(member => member.Value is not null || HasMemberAssignments(member.Members));

    private void BindMemberInitializers(IrExpression receiver, ImmutableArray<MemberInitializerSyntax> initializers,
        ImmutableArray<IrVariable>.Builder bindings, HashSet<int> pending, bool direct)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in initializers)
        {
            var origin = new SourceOrigin(member.Location, "object-initializer");
            if (!names.Add(member.Name)) diagnostics.Error("WF2028", $"Duplicate initializer for '{member.Name}'.", member.Location);
            if (!fields.ContainsKey(receiver.Type.Name) || MemberType(receiver.Type.Name, member.Name) is null)
            { diagnostics.Error("WF2001", $"Type '{receiver.Type.Name}' has no field or property '{member.Name}'.", member.Location); continue; }
            var target = SelectMember(receiver.Type.Name, member.Name, receiver, origin) with { Initializing = direct };
            if (member.Value is not null)
            {
                target = CheckWritable(target);
                if (direct && target.Property is { BackingField: null } && pending.Count > 0)
                    diagnostics.Error("WF2022", "Initialize required reference storage before invoking a custom accessor on the new object.", member.Location);
                var value = BindExpression(member.Value);
                var assignment = WriteTarget(target, value, origin);
                var ignored = new VariableSymbol(nextSymbol++, "<initializer-result>", assignment.Type, member.Location);
                bindings.Add(new(ignored, assignment, origin));
                if (direct && (target.Field ?? target.Property?.BackingField) is { } storage) pending.Remove(storage.Id);
            }
            else
            {
                if (direct && HasMemberAssignments(member.Members) && ((target.Field ?? target.Property?.BackingField) is { } storage && pending.Contains(storage.Id) ||
                    target.Property is { BackingField: null } && pending.Count > 0))
                    diagnostics.Error("WF2022", "Nested initializer reads reference storage before initialization.", member.Location);
                var child = ReadTarget(target);
                if (child.Type.Kind != TypeKind.Nominal)
                { diagnostics.Error("WF2028", "A nested member initializer requires an existing class object.", member.Location); continue; }
                // Like C#, each nested assignment evaluates its full member receiver again.
                // An empty nested initializer performs no getter call.
                BindMemberInitializers(child, member.Members, bindings, [], direct: false);
            }
        }
    }
}
