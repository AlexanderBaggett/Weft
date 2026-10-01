using System.Collections.Immutable;
using Weft.Compiler.IR;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public sealed partial class Binder
{
    private void BindStaticInitializers(ImmutableArray<IrStatement>.Builder prefix)
    {
        bindingFieldInitializer = true;
        foreach (var (field, initializer) in fields[currentFunction.ContainingType!])
        {
            if (!field.IsStatic || initializer is null) continue;
            var value = ConvertImplicit(BindExpression(initializer), field.Type);
            var origin = new SourceOrigin(initializer.Location, "static-field-initializer");
            prefix.Add(new IrExpressionStatement(new IrFieldWrite(field, null, value, origin), origin));
            initializedFields.Add(field.Id);
        }
        bindingFieldInitializer = false;
    }

    private void RequireStaticInitialized(SourceLocation location)
    {
        var missing = fields[currentFunction.ContainingType!].Where(f => f.Symbol.IsStatic &&
            f.Symbol.Type.Kind is TypeKind.String or TypeKind.Nominal && !initializedFields.Contains(f.Symbol.Id)).Select(f => f.Symbol.Name).ToArray();
        if (missing.Length > 0)
            diagnostics.Error("WF2033", "Static initialization must populate non-null fields on every normal return: " + string.Join(", ", missing) + ".", location);
    }
}
