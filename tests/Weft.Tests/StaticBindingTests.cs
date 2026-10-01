using Weft.Compiler;
using Weft.Compiler.Backends;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;

namespace Weft.Tests;

public sealed class StaticBindingTests
{
    [Theory]
    [InlineData("class C { public static C() {} }", "WF2032")]
    [InlineData("class C { static C(int x) {} }", "WF2032")]
    [InlineData("class C { static C() : base() {} }", "WF2032")]
    [InlineData("class C { static C() {} static C() {} }", "WF2002")]
    [InlineData("class C { static C() { return 1; } }", "WF2003")]
    [InlineData("static class C { int X; }", "WF2012")]
    [InlineData("static class C { int X { get; set; } }", "WF2012")]
    [InlineData("class C { public static required int X; }", "WF2032")]
    [InlineData("class C { public static required int X { get; set; } }", "WF2032")]
    [InlineData("class C { public static int X { get; init; } }", "WF2032")]
    [InlineData("class C { public static int X; } void Main() { Print(new C().X); }", "WF2020")]
    [InlineData("class C { public static int X; } void Main() { new C { X = 1 }; }", "WF2020")]
    [InlineData("record C { public static int X; } void Main() { var c = new C() with { X = 1 }; }", "WF2020")]
    [InlineData("class C { public static int X { get; set; } } void Main() { Print(new C().X); }", "WF2020")]
    [InlineData("class C { public int X; } void Main() { Print(C.X); }", "WF2020")]
    [InlineData("class C { static int X; } void Main() { C.X = 1; }", "WF2011")]
    [InlineData("class C { public static int X { get; private set; } } void Main() { C.X++; }", "WF2011")]
    [InlineData("class C { public static readonly int X; public C() { X = 1; } }", "WF2021")]
    [InlineData("class C { public readonly int X; static C() { X = 1; } }", "WF2020")]
    [InlineData("class C { public static readonly int X; } void Main() { C.X = 1; }", "WF2021")]
    [InlineData("class C { public static int X { get; } } void Main() { C.X = 1; }", "WF2023")]
    [InlineData("class C { public static string X; }", "WF2033")]
    [InlineData("class C { public static string X { get; } }", "WF2033")]
    [InlineData("class C { static string A = B; static string B = \"b\"; }", "WF2033")]
    [InlineData("class C { static string A; static C() { if (true) return; A = \"a\"; } }", "WF2033")]
    [InlineData("class C { static string A; static C() { Print(A); A = \"a\"; } }", "WF2033")]
    [InlineData("class C { static C() { var x = this; } }", "WF2020")]
    [InlineData("record R(int X) { public static int X; }", "WF2031")]
    [InlineData("record R(int X) { static int Y = X; }", "WF2020")]
    public void Invalid_static_members_are_rejected(string source, string code)
    {
        var result = Compilation.Analyze("invalid", [new("invalid.weft", source)]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void Static_storage_initializer_and_accessor_metadata_is_registered()
    {
        var result = Compilation.Analyze("static", [new("static.weft", "static class C { public static readonly int X = 1; public static int P { get; set; } static C() { P = 2; } }")]);
        Assert.Empty(result.Diagnostics);
        var type = Assert.Single(result.Module!.Classes);
        Assert.True(type.Symbol.IsStatic);
        Assert.All(type.Fields, f => Assert.True(f.IsStatic));
        Assert.True(type.TypeInitializer!.IsTypeInitializer);
        var property = Assert.Single(result.Properties);
        Assert.True(property.IsStatic); Assert.Null(property.Getter!.Receiver); Assert.Null(property.Setter!.Receiver);
        Assert.Empty(IrValidator.Validate(result.Module));
        var diagnostics = new DiagnosticBag();
        Assert.Null(EntryPoint.Resolve(result.Module, "C..cctor", diagnostics));
        Assert.Contains(diagnostics, d => d.Code == "WF4003");
    }

    [Fact]
    public void Static_function_owners_must_exist_before_emission()
    {
        var module = Compilation.Analyze("owner", [new("owner.weft", "static class C { public static int M() => 1; } void Main() { var value = C.M(); }")]).Module!;
        var method = module.Functions.Single(f => f.Symbol.Name == "C.M");
        var main = module.Functions.Single(f => f.Symbol.Name == "Main");
        var forged = method.Symbol with { ContainingType = "Missing" };
        var variable = (IrVariable)main.Body.Statements[0];
        var changedMain = main with { Body = main.Body with { Statements = [variable with { Initializer = ((IrCall)variable.Initializer) with { Function = forged } }] } };
        var diagnostics = IrValidator.Validate(module with { Functions = module.Functions.Replace(method, method with { Symbol = forged }).Replace(main, changedMain) });
        Assert.Contains(diagnostics, d => d.Code == "WF3001" && d.Message.Contains("owner", StringComparison.Ordinal));
    }

    [Fact]
    public void Malformed_static_ir_cannot_forge_owners_receivers_or_initializer_calls()
    {
        var module = Compilation.Analyze("ir", [new("ir.weft", "class C { public static readonly int X = 1; public static int P { get; set; } } void Main() { Print(C.X); C.P = 2; }")]).Module!;
        var type = Assert.Single(module.Classes);
        var initializer = module.Functions.Single(f => f.Symbol.IsTypeInitializer);
        var main = module.Functions.Single(f => f.Symbol.Name == "Main");
        var origin = main.Origin;
        var direct = main with { Body = new([new IrExpressionStatement(new IrCall(initializer.Symbol, [], origin), origin)], origin) };
        Assert.Contains(IrValidator.Validate(module with { Functions = module.Functions.Replace(main, direct) }), d => d.Code == "WF3001");
        Assert.Contains(IrValidator.Validate(module with { Classes = [type with { TypeInitializer = null }] }), d => d.Code == "WF3001");
        Assert.Contains(IrValidator.Validate(module with { Classes = [type with { TypeInitializer = initializer.Symbol with { Id = 999 } }] }), d => d.Code == "WF3001");
        var field = type.Fields.Single(f => f.Name == "X");
        var write = main with { Body = new([new IrExpressionStatement(new IrFieldWrite(field, null, new IrConstant(2, WeftType.Int32, origin), origin), origin)], origin) };
        Assert.Contains(IrValidator.Validate(module with { Functions = module.Functions.Replace(main, write) }), d => d.Code == "WF3001");
        var wrongReceiver = main with { Body = new([new IrVariable(new(998, "x", WeftType.Int32, origin.Location), new IrFieldRead(field, new IrConstant(1, WeftType.Int32, origin), origin), origin)], origin) };
        Assert.Contains(IrValidator.Validate(module with { Functions = module.Functions.Replace(main, wrongReceiver) }), d => d.Code == "WF3001");
    }
}
