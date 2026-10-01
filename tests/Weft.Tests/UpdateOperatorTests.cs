using Weft.Compiler;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;

namespace Weft.Tests;

public sealed class UpdateOperatorTests
{
    [Theory]
    [InlineData("void Main() { 1++; }", "WF2005")]
    [InlineData("void Main() { ++missing; }", "WF2005")]
    [InlineData("int Value() => 1; void Main() { Value()++; }", "WF2005")]
    [InlineData("void Main() { var x = 1; ++x++; }", "WF2005")]
    [InlineData("void Main() { var x = true; x++; }", "WF2003")]
    [InlineData("void Main() { var x = \"text\"; --x; }", "WF2003")]
    [InlineData("void Main() { var x = 1; x += 1L; }", "WF2003")]
    [InlineData("void Main() { var x = false; x += true; }", "WF2003")]
    [InlineData("void Main() { var x = \"text\"; x -= 1; }", "WF2003")]
    [InlineData("void Main() { 1 += 2; }", "WF2005")]
    [InlineData("int Value(int x = 1++) => x;", "WF2013")]
    public void Invalid_updates_are_rejected_by_the_frontend(string source, string code)
    {
        var result = Compilation.Analyze("invalid", [new("invalid.weft", source)]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void Ir_updates_must_target_an_in_scope_integer_with_a_valid_operator()
    {
        var module = Compilation.Analyze("update", [new("update.weft", "void Main() { var i = 0; i++; }")]).Module!;
        Assert.Empty(IrValidator.Validate(module));
        var function = module.Functions[0];
        var variable = ((IrVariable)function.Body.Statements[0]).Symbol;
        var origin = function.Origin;
        IrUpdate[] invalid =
        [
            new(variable, "+=", false, origin),
            new(variable with { Id = 500 }, "++", true, origin),
            new(variable with { Type = WeftType.Bool }, "--", false, origin)
        ];
        foreach (var update in invalid)
        {
            var bad = module with { Functions = [function with { Body = function.Body with
                { Statements = [function.Body.Statements[0], new IrExpressionStatement(update, origin)] } }] };
            Assert.Contains(IrValidator.Validate(bad), diagnostic => diagnostic.Code == "WF3001");
        }
    }
}
