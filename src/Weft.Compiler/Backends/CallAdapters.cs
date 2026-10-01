using System.Collections.Immutable;
using Weft.Compiler.IR;

namespace Weft.Compiler.Backends;

// Receivers and arguments are evaluated at the call site in written order, before
// a static adapter reorders the resulting values into parameter positions.
public static class CallAdapters
{
    public static string Name(IrCall call) => call.ParameterOrder.IsDefaultOrEmpty
        ? $"f_{call.Function.Id}" : $"c_{call.Function.Id}_{string.Join("_", call.ParameterOrder)}";
    public static ImmutableArray<IrCall> Collect(IrModule module) => module.Functions
        .SelectMany(f => IrTraversal.Descendants(f.Body)).OfType<IrCall>()
        .Where(c => !c.ParameterOrder.IsDefaultOrEmpty).DistinctBy(Name).ToImmutableArray();
}
