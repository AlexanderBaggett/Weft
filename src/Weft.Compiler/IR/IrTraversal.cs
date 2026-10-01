namespace Weft.Compiler.IR;

public static class IrTraversal
{
    public static IEnumerable<IrNode> Descendants(IrNode node)
    {
        yield return node;
        IEnumerable<IrNode> children = node switch
        {
            IrBlock block => block.Statements,
            IrVariable variable => [variable.Initializer],
            IrReturn { Expression: not null } returned => [returned.Expression],
            IrExpressionStatement expression => [expression.Expression],
            IrIf branch => branch.Else is null ? [branch.Condition, branch.Then] : [branch.Condition, branch.Then, branch.Else],
            IrWhile loop => [loop.Condition, loop.Body],
            IrDoWhile loop => [loop.Body, loop.Condition],
            IrFor loop => loop.Initializers.Cast<IrNode>().Concat(loop.Condition is null ? [] : [loop.Condition]).Concat(loop.Iterators).Append(loop.Body),
            IrAssign assign => [assign.Value],
            IrConvert convert => [convert.Operand],
            IrUnary unary => [unary.Operand],
            IrBinary binary => [binary.Left, binary.Right],
            IrConditional conditional => [conditional.Condition, conditional.WhenTrue, conditional.WhenFalse],
            IrCall call => (call.Receiver is null ? Enumerable.Empty<IrNode>() : [call.Receiver]).Concat(call.Arguments),
            IrIntrinsic intrinsic => intrinsic.Arguments,
            IrSetterCall setter => [setter.Receiver, setter.Value],
            IrCopy copy => [copy.Receiver],
            IrObjectHash hash => [hash.Receiver],
            IrFieldRead read => [read.Receiver],
            IrFieldWrite write => [write.Receiver, write.Value],
            IrFieldUpdate update => [update.Receiver],
            IrSequence sequence => sequence.Bindings.Cast<IrNode>().Append(sequence.Value),
            IrConstant or IrRead or IrUpdate or IrAllocate or IrBreak or IrContinue or IrReturn => [],
            _ => throw new InvalidOperationException($"Unrecognized IR node {node.GetType().Name}.")
        };
        foreach (var child in children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
