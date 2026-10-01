namespace Weft.Compiler.IR;

[Flags]
public enum FlowExit { None = 0, Next = 1, Return = 2, Break = 4, Continue = 8 }

// Shared by binding, IR validation, and emission. Loops consume only their own
// breaks/continues; a nested loop never terminates an enclosing loop by accident.
public static class ControlFlow
{
    public static bool CanComplete(IrStatement statement) => Exits(statement).HasFlag(FlowExit.Next);
    public static bool ReachesIterator(IrStatement body) => (Exits(body) & (FlowExit.Next | FlowExit.Continue)) != 0;
    public static bool IsTrue(IrExpression? condition) => condition is null or IrConstant { Value: true };

    public static FlowExit Exits(IrStatement statement) => statement switch
    {
        IrReturn => FlowExit.Return,
        IrBreak => FlowExit.Break,
        IrContinue => FlowExit.Continue,
        IrBlock block => Sequence(block.Statements),
        IrIf branch => Exits(branch.Then) | (branch.Else is null ? FlowExit.Next : Exits(branch.Else)),
        IrWhile loop => Loop(loop.Body, loop.Condition, false),
        IrDoWhile loop => Loop(loop.Body, loop.Condition, true),
        IrFor loop => Loop(loop.Body, loop.Condition, false),
        _ => FlowExit.Next
    };

    private static FlowExit Sequence(IEnumerable<IrStatement> statements)
    {
        var exits = FlowExit.Next;
        foreach (var statement in statements)
            if (exits.HasFlag(FlowExit.Next)) exits = (exits & ~FlowExit.Next) | Exits(statement);
        return exits;
    }

    private static FlowExit Loop(IrStatement body, IrExpression? condition, bool atLeastOnce)
    {
        var exits = Exits(body);
        var canExit = exits.HasFlag(FlowExit.Break) || !IsTrue(condition) && (!atLeastOnce || ReachesIterator(body));
        return (exits & FlowExit.Return) | (canExit ? FlowExit.Next : FlowExit.None);
    }
}
