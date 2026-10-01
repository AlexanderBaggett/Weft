using System.Text;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;

namespace Weft.Compiler.Backends;

public static class ObjectEmission
{
    public static string TypeName(WeftType type) => "t_" + Convert.ToHexString(Encoding.UTF8.GetBytes(type.Name));
    public static IEnumerable<FunctionSymbol> Setters(IrModule module) => module.Functions
        .SelectMany(f => IrTraversal.Descendants(f.Body)).OfType<IrSetterCall>().Select(s => s.Setter).Distinct();
    public static string SequenceName(WeftType ignored, WeftType result) => "s_" + TypeName(ignored) + "_" + TypeName(result);
    public static IEnumerable<VariableSymbol> Temporaries(IrFunction function) => IrTraversal.Descendants(function.Body)
        .OfType<IrSequence>().SelectMany(s => s.Bindings).Select(b => b.Symbol);
    public static IEnumerable<(WeftType Ignored, WeftType Result)> Sequences(IrModule module) => module.Functions
        .SelectMany(f => IrTraversal.Descendants(f.Body)).OfType<IrSequence>()
        .SelectMany(s => s.Bindings.Select(b => (b.Symbol.Type, s.Type))).Distinct();
    public static string Sequence(IrSequence sequence, Func<IrExpression, string> expression)
    {
        var result = expression(sequence.Value);
        foreach (var binding in sequence.Bindings.Reverse())
            result = $"{SequenceName(binding.Symbol.Type, sequence.Type)}({expression(new IrAssign(binding.Symbol, binding.Initializer, binding.Origin))}, {result})";
        return result;
    }
    public static IEnumerable<VariableSymbol> Parameters(FunctionSymbol function) => function.Receiver is null || function.IsConstructor
        ? function.Parameters : new[] { function.Receiver }.Concat(function.Parameters);
    public static IEnumerable<IrExpression> Arguments(IrCall call) => call.Receiver is null ? call.Arguments : new[] { call.Receiver }.Concat(call.Arguments);
}
