using System.Collections.Immutable;
using Weft.Compiler.IR;
using Weft.Compiler.Syntax;

namespace Weft.Compiler.Semantics;

public sealed partial class Binder
{
    private readonly Dictionary<int, IrCall> constructorChains = [];
    private readonly Dictionary<int, HashSet<int>> constructorInitializedFields = [];
    private List<HashSet<int>> constructorExits = [];

    private IEnumerable<(FunctionSyntax Syntax, FunctionSymbol Symbol, string Namespace)> OrderConstructorBodies()
    {
        foreach (var (syntax, symbol, ns) in bodies)
        {
            if (syntax.Initializer is not { } initializer) continue;
            if (initializer.Kind == "base")
            {
                if (initializer.Arguments.Length > 0)
                    diagnostics.Error("WF2009", "A base initializer with arguments requires inheritance support.", initializer.Location);
                continue;
            }
            currentFunction = symbol; currentNamespace = ns;
            scope = new(); initializedFields = [];
            foreach (var parameter in symbol.Parameters) scope.Declare(parameter);
            bindingFieldInitializer = true;
            var arguments = initializer.Arguments.Select(a => BindExpression(a.Expression)).ToImmutableArray();
            var callSyntax = new CallSyntax(new NameSyntax(symbol.Name, initializer.Location), initializer.Arguments, initializer.Location);
            var call = BindInvocation(callSyntax, symbol.Name, functions[symbol.Name], arguments);
            bindingFieldInitializer = false;
            if (call is IrCall target) constructorChains.Add(symbol.Id, target);
        }
        var constructorBodies = bodies.Where(b => b.Symbol.IsConstructor).ToDictionary(b => b.Symbol.Id);
        var order = ConstructorGraph.Order(constructorBodies.Values.Select(b => b.Symbol),
            constructorChains.ToDictionary(pair => pair.Key, pair => pair.Value.Function), cycle =>
                diagnostics.Add(new("WF2029", "Constructor chain is circular: " + string.Join(" -> ", cycle.Select(ConstructorGraph.Signature)) + ".",
                    constructorBodies[cycle[0].Id].Syntax.Initializer!.Location, Related: cycle.Select(c => c.Location).ToArray())));
        return order.Select(c => constructorBodies[c.Id]).Concat(bodies.Where(b => !b.Symbol.IsConstructor));
    }
}
