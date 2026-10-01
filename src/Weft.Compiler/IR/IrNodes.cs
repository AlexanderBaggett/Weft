using System.Collections.Immutable;
using Weft.Compiler.Semantics;
using Weft.Compiler.Text;

namespace Weft.Compiler.IR;

public sealed record IrModule(string Name, ImmutableArray<IrFunction> Functions, ImmutableArray<IntrinsicSignature> RequiredIntrinsics, string RuntimeAbi);
public sealed record IrFunction(FunctionSymbol Symbol, IrBlock Body, SourceOrigin Origin);
public abstract record IrNode(SourceOrigin Origin);
public abstract record IrStatement(SourceOrigin Origin) : IrNode(Origin);
public sealed record IrBlock(ImmutableArray<IrStatement> Statements, SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrVariable(VariableSymbol Symbol, IrExpression Initializer, SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrReturn(IrExpression? Expression, SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrExpressionStatement(IrExpression Expression, SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrIf(IrExpression Condition, IrStatement Then, IrStatement? Else, SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrWhile(IrExpression Condition, IrStatement Body, SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrDoWhile(IrStatement Body, IrExpression Condition, SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrFor(ImmutableArray<IrStatement> Initializers, IrExpression? Condition,
    ImmutableArray<IrExpression> Iterators, IrStatement Body, SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrBreak(SourceOrigin Origin) : IrStatement(Origin);
public sealed record IrContinue(SourceOrigin Origin) : IrStatement(Origin);
public abstract record IrExpression(WeftType Type, SourceOrigin Origin) : IrNode(Origin);
public sealed record IrConstant(object Value, WeftType Type, SourceOrigin Origin) : IrExpression(Type, Origin);
public sealed record IrRead(VariableSymbol Symbol, SourceOrigin Origin) : IrExpression(Symbol.Type, Origin);
public sealed record IrAssign(VariableSymbol Symbol, IrExpression Value, SourceOrigin Origin) : IrExpression(Symbol.Type, Origin);
public sealed record IrUpdate(VariableSymbol Symbol, string Operator, bool Postfix, SourceOrigin Origin) : IrExpression(Symbol.Type, Origin);
public sealed record IrUnary(string Operator, IrExpression Operand, WeftType Type, SourceOrigin Origin) : IrExpression(Type, Origin);
public sealed record IrBinary(IrExpression Left, string Operator, IrExpression Right, WeftType Type, SourceOrigin Origin) : IrExpression(Type, Origin);
// Arguments remain in written evaluation order. ParameterOrder maps each argument to
// its destination parameter; default/empty means the identity order.
public sealed record IrCall(FunctionSymbol Function, ImmutableArray<IrExpression> Arguments, SourceOrigin Origin,
    ImmutableArray<int> ParameterOrder = default) : IrExpression(Function.ReturnType, Origin);
public sealed record IrConditional(IrExpression Condition, IrExpression WhenTrue, IrExpression WhenFalse,
    WeftType Type, SourceOrigin Origin) : IrExpression(Type, Origin);
public sealed record IrConvert(IrExpression Operand, WeftType Type, SourceOrigin Origin) : IrExpression(Type, Origin);
public sealed record IrIntrinsic(IntrinsicSignature Signature, ImmutableArray<IrExpression> Arguments, SourceOrigin Origin) : IrExpression(Signature.Result, Origin);

public sealed record IntrinsicSignature(string Name, ImmutableArray<WeftType> Parameters, WeftType Result);
public static class Intrinsics
{
    public const string AbiVersion = "1";
    public static readonly IntrinsicSignature Print = new("rt.console.write_line", [WeftType.String], WeftType.Void);
    public static readonly IntrinsicSignature ToStringInt32 = new("rt.text.int32", [WeftType.Int32], WeftType.String);
    public static readonly IntrinsicSignature ToStringInt64 = new("rt.text.int64", [WeftType.Int64], WeftType.String);
    public static readonly IntrinsicSignature ToStringBool = new("rt.text.bool", [WeftType.Bool], WeftType.String);
    public static readonly ImmutableArray<IntrinsicSignature> Bootstrap = [Print, ToStringInt32, ToStringInt64, ToStringBool];
}
