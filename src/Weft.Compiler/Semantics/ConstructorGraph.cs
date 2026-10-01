using System.Collections.Immutable;

namespace Weft.Compiler.Semantics;

// Constructor delegation has at most one outgoing edge. Iterate chains rather than
// recursing so long generated chains do not consume the compiler's call stack.
public static class ConstructorGraph
{
    public static ImmutableArray<FunctionSymbol> Order(IEnumerable<FunctionSymbol> constructors,
        IReadOnlyDictionary<int, FunctionSymbol> targets, Action<IReadOnlyList<FunctionSymbol>> reportCycle)
    {
        var nodes = constructors.ToDictionary(f => f.Id);
        var done = new HashSet<int>();
        var ordered = ImmutableArray.CreateBuilder<FunctionSymbol>();
        foreach (var root in nodes.Values)
        {
            var path = new List<FunctionSymbol>();
            var positions = new Dictionary<int, int>();
            var current = root;
            while (!done.Contains(current.Id))
            {
                if (positions.TryGetValue(current.Id, out var start))
                { reportCycle(path.Skip(start).Append(current).ToArray()); break; }
                positions.Add(current.Id, path.Count); path.Add(current);
                if (!targets.TryGetValue(current.Id, out var target) || !nodes.TryGetValue(target.Id, out current!)) break;
            }
            for (var i = path.Count - 1; i >= 0; i--)
                if (done.Add(path[i].Id)) ordered.Add(path[i]);
        }
        return ordered.ToImmutable();
    }

    public static string Signature(FunctionSymbol constructor) =>
        constructor.Name + "(" + string.Join(", ", constructor.Parameters.Select(p => p.Type.Name)) + ")";
}
