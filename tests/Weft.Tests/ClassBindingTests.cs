using Weft.Compiler;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;

namespace Weft.Tests;

public sealed class ClassBindingTests
{
    [Theory]
    [InlineData("class C { private int x; } void Main() { Print(new C().x); }", "WF2011")]
    [InlineData("class C { private C() {} } void Main() { new C(); }", "WF2011")]
    [InlineData("class C { private void M() {} } void Main() { new C().M(); }", "WF2011")]
    [InlineData("class C { public readonly int X; } void Main() { new C().X = 1; }", "WF2021")]
    [InlineData("class C { public readonly int X; } void Main() { new C().X++; }", "WF2021")]
    [InlineData("class C { public void M() {} } void Main() { C.M(); }", "WF2020")]
    [InlineData("class C { public static void M() {} } void Main() { new C().M(); }", "WF2020")]
    [InlineData("class C { int X; static int M() => X; }", "WF2020")]
    [InlineData("class C { int X = this.X; }", "WF2020")]
    [InlineData("class C { string X; }", "WF2022")]
    [InlineData("class C { string X; public C(bool b) { if (b) X = \"x\"; } }", "WF2022")]
    [InlineData("class C { string X; public C() { Print(X); X = \"x\"; } }", "WF2022")]
    [InlineData("class C { string X; public C() { var alias = this; X = \"x\"; } }", "WF2022")]
    [InlineData("class C { string X; public C() { M(); X = \"x\"; } void M() {} }", "WF2022")]
    [InlineData("class C { string X; public C(bool b) { while (b) { X = \"x\"; break; } } }", "WF2022")]
    [InlineData("class C { string X; public C(bool b) { do { if (b) break; X = \"x\"; } while (false); } }", "WF2022")]
    [InlineData("class C { string X; public C(bool b) { var k = b && ((X = \"x\") == \"x\"); } }", "WF2022")]
    [InlineData("class C { string X; public C(bool b) { var k = b ? (X = \"x\") : \"y\"; } }", "WF2022")]
    [InlineData("class C { public int X; public int X() => 1; }", "WF2002")]
    [InlineData("class Hidden {} public Hidden Expose() => new Hidden();", "WF2019")]
    [InlineData("class Hidden {} public class API { public Hidden Field = new Hidden(); }", "WF2019")]
    [InlineData("static class C {} void Main() { new C(); }", "WF2020")]
    [InlineData("static class C {} C Pass(C c) => c;", "WF2020")]
    [InlineData("class C { public C() { return 1; } }", "WF2003")]
    [InlineData("class C { public C(int n) {} } void Main() { new C(); }", "WF2006")]
    [InlineData("class A {} class B {} void Main() { A a = new B(); }", "WF2003")]
    [InlineData("class C { public C() { this = new C(); } }", "WF2005")]
    [InlineData("void Run() {} class C { int Run; void Test() { Run(); } }", "WF2003")]
    [InlineData("class C { int Print; void Test() { Print(1); } }", "WF2003")]
    [InlineData("class C { int Log; static void Test() { Log(1); } }", "WF2003")]
    public void Invalid_classes_are_rejected_before_emission(string source, string code)
    {
        var result = Compilation.Analyze("invalid", [new("invalid.weft", source)]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void Types_are_available_before_their_declarations_and_across_files()
    {
        var result = Compilation.Analyze("forward", [
            new("first.weft", "namespace Demo; C Make() => new C(); void Main() { var c = Make(); Print(c.Value); }"),
            new("second.weft", "namespace Demo; class C { public int Value = 9; }")]);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Module);
        Assert.Single(result.Module.Classes);
        Assert.Contains(result.Functions, f => f.IsConstructor && f.ReturnType.Name == "Demo.C");
    }

    [Fact]
    public void Object_ir_checks_field_identity_owner_and_receiver_signatures()
    {
        var module = Compilation.Analyze("ir", [new("ir.weft", "class C { public int Value; public int Get() => Value; } void Main() { new C(); }")]).Module!;
        var method = module.Functions.Single(f => f.Symbol.Name == "C.Get");
        var origin = method.Origin;
        var field = module.Classes[0].Fields[0];
        var receiver = new IrRead(method.Symbol.Receiver!, origin);
        IrExpression[] invalid = [
            new IrFieldRead(field with { Id = 999 }, receiver, origin),
            new IrFieldRead(field, new IrConstant(1, WeftType.Int32, origin), origin),
            new IrFieldWrite(field, receiver, new IrConstant(true, WeftType.Bool, origin), origin),
            new IrFieldUpdate(field, receiver, "+=", false, origin),
            new IrCall(method.Symbol, [], origin),
            new IrCall(method.Symbol, [], origin, Receiver: new IrConstant(1, WeftType.Int32, origin))
        ];
        foreach (var expression in invalid)
        {
            var changed = method with { Body = new([new IrReturn(expression, origin)], origin) };
            var bad = module with { Functions = module.Functions.Replace(method, changed) };
            Assert.Contains(IrValidator.Validate(bad), d => d.Code == "WF3001");
        }
    }

    [Fact]
    public void Empty_ir_sequences_are_rejected_before_emission()
    {
        var module = Compilation.Analyze("ir", [new("ir.weft", "void Main() {}")]).Module!;
        var main = module.Functions.Single();
        var origin = main.Origin;
        var expression = new IrSequence([], new IrConstant(1, WeftType.Int32, origin), origin);
        var changed = main with { Body = new([new IrExpressionStatement(expression, origin)], origin) };
        var bad = module with { Functions = module.Functions.Replace(main, changed) };
        Assert.Contains(IrValidator.Validate(bad), d => d.Code == "WF3001" && d.Message.Contains("at least one binding"));
    }
}
