using Weft.Compiler;
using Weft.Compiler.Semantics;

namespace Weft.Tests;

public sealed class FunctionBindingTests
{
    [Theory]
    [InlineData("int F(int a, int b) => a; void Main() { F(a: 1, a: 2); }", "WF2014")]
    [InlineData("int F(int a, int b) => a; void Main() { F(b: 1, 2); }", "WF2014")]
    [InlineData("int F(int a) => a; void Main() { F(missing: 1); }", "WF2014")]
    [InlineData("int F(int a, int b = 2) => a; void Main() { F(); }", "WF2006")]
    [InlineData("int F(int a = 1, int b) => a; void Main() {}", "WF2013")]
    [InlineData("int F(int a = 2147483647 + 1) => a; void Main() {}", "WF2013")]
    [InlineData("int G() => 1; int F(int a = G()) => a; void Main() {}", "WF2013")]
    [InlineData("int F(int a) => a; long F(int other = 1) => 1L; void Main() {}", "WF2002")]
    [InlineData("static class Hidden { static int Read() => 1; } void Main() { Hidden.Read(); }", "WF2011")]
    [InlineData("static class Invalid { public int Read() => 1; } void Main() {}", "WF2012")]
    [InlineData("public private void Main() {}", "WF2012")]
    [InlineData("int F(long x) => 1; namespace App { int F(string x) => 2; void Main() { F(1); } }", "WF2003")]
    [InlineData("static class Tools { public static int Read() => 1; } namespace App { static class Tools {} void Main() { Tools.Read(); } }", "WF2001")]
    [InlineData("namespace Tools { int Read() => 1; } namespace App { namespace Tools {} void Main() { Tools.Read(); } }", "WF2001")]
    [InlineData("namespace Tools {} static class Tools {} void Main() {}", "WF2002")]
    public void Invalid_function_contracts_fail_before_codegen(string source, string code)
    {
        var result = Compilation.Analyze("functions", [new("functions.weft", source)]);
        Assert.Null(result.Module);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal("functions.weft", diagnostic.Location.File);
        Assert.InRange(diagnostic.Location.Start, 0, source.Length - 1);
    }

    [Fact]
    public void Ambiguous_overloads_report_the_call_and_both_declarations()
    {
        var source = "int Pick(int x, long y) => 1;\nint Pick(long x, int y) => 2;\nvoid Main() { Pick(1, 2); }";
        var result = Compilation.Analyze("ambiguous", [new("ambiguous.weft", source)]);
        var error = Assert.Single(result.Diagnostics);
        Assert.Null(result.Module);
        Assert.Equal("WF2015", error.Code);
        Assert.Equal(3, error.Location.Line);
        Assert.Equal(new[] { 1, 2 }, error.Related!.Select(location => location.Line));
    }

    [Fact]
    public void Public_method_contract_keeps_names_defaults_and_declaring_type()
    {
        var result = Compilation.Analyze("library", [new("api.weft", "namespace Pricing; public static class Prices { public static long Total(int count, long fee = 2 + 3) => count + fee; }")]);
        Assert.Empty(result.Diagnostics);
        var method = Assert.Single(result.Functions);
        Assert.Equal("Pricing.Prices", method.ContainingType);
        Assert.Equal(Visibility.Public, method.Visibility);
        Assert.Equal(new[] { "count", "fee" }, method.Parameters.Select(p => p.Name));
        Assert.Equal(new ConstantValue(5L, WeftType.Int64), method.Parameters[1].Default);
    }
}
