using Weft.Compiler;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;
using Weft.Compiler.Text;

namespace Weft.Tests;

public sealed class ControlFlowTests
{
    [Theory]
    [InlineData("void Main() { break; }", "WF2016")]
    [InlineData("void Main() { continue; }", "WF2016")]
    [InlineData("void Main() { while (true) break; continue; }", "WF2016")]
    [InlineData("void Main() { for (; 1;) break; }", "WF2003")]
    [InlineData("void Main() { do {} while (1); }", "WF2003")]
    [InlineData("void Main() { Print(1 ? 2 : 3); }", "WF2003")]
    [InlineData("void Main() { Print(true ? 2 : false); }", "WF2003")]
    [InlineData("void Main() { var x = true ? Print(1) : Print(2); }", "WF2003")]
    [InlineData("void Main() { for (var i = 0; i < 1; i = i + 1) {} Print(i); }", "WF2001")]
    [InlineData("void Main() { for (; true; Print(x)) { var x = 1; break; } }", "WF2001")]
    [InlineData("void Main() { do { var x = false; } while (x); }", "WF2001")]
    [InlineData("void Main() { for (var i = 0, j = 1;;) break; }", "WF1104")]
    [InlineData("void Main() { for (; true; 42) break; }", "WF2008")]
    [InlineData("void Main() { for (42;;) break; }", "WF2008")]
    [InlineData("void Main() { if (true) var x = 1; }", "WF2017")]
    [InlineData("void Main() { while (false) var x = 1; }", "WF2017")]
    [InlineData("void Main() { do var x = 1; while (false); }", "WF2017")]
    [InlineData("void Main() { for (;;) var x = 1; }", "WF2017")]
    [InlineData("void Main() { while (true) { break; Print(1); } }", "WF2010")]
    [InlineData("void Main() { for (;;) { continue; Print(1); } }", "WF2010")]
    [InlineData("void Main() { for (;;) { if (true) break; else continue; Print(1); } }", "WF2010")]
    [InlineData("void Main() { while (true) {} Print(1); }", "WF2010")]
    [InlineData("int Main() { while (true) { break; } }", "WF2007")]
    [InlineData("int Main() { for (; false;) { return 1; } }", "WF2007")]
    [InlineData("int Main() { do { continue; } while (false); }", "WF2007")]
    [InlineData("int Main() { do { if (true) return 1; else break; } while (true); }", "WF2007")]
    [InlineData("int Value(int x = true ? 1 : Missing()) => x;", "WF2013")]
    public void Invalid_control_flow_is_diagnosed_before_emission(string source, string code)
    {
        var result = Compilation.Analyze("invalid", [new("invalid.weft", source)]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void Malformed_control_flow_ir_cannot_reach_either_backend()
    {
        var module = Compilation.Analyze("flow", [new("flow.weft", "void Main() {}")]).Module!;
        var function = module.Functions[0];
        var origin = function.Origin;
        var number = new IrConstant(1, WeftType.Int32, origin);
        var yes = new IrConstant(true, WeftType.Bool, origin);
        var variable = new VariableSymbol(50, "i", WeftType.Int32, origin.Location);
        var empty = new IrBlock([], origin);
        IrStatement[] malformed =
        [
            new IrBreak(origin), new IrContinue(origin),
            new IrWhile(yes, new IrBlock([new IrBreak(origin), new IrExpressionStatement(new IrAssign(variable, number, origin), origin)], origin), origin),
            new IrDoWhile(empty, number, origin),
            new IrFor([], number, [], empty, origin),
            new IrFor([new IrReturn(null, origin)], null, [], empty, origin),
            new IrFor([], yes, [number], empty, origin),
            new IrBlock([new IrFor([new IrVariable(variable, number, origin)], yes, [], new IrBreak(origin), origin),
                new IrVariable(variable with { Id = 51 }, new IrRead(variable, origin), origin)], origin),
            new IrVariable(variable, new IrConditional(number, number, number, WeftType.Int32, origin), origin),
            new IrVariable(variable, new IrConditional(yes, number, yes, WeftType.Int32, origin), origin)
        ];
        foreach (var body in malformed)
        {
            var invalid = module with { Functions = [function with { Body = new([body], origin) }] };
            Assert.Contains(IrValidator.Validate(invalid), diagnostic => diagnostic.Code == "WF3001");
        }
    }
}
