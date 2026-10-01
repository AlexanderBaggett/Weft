using Weft.Compiler;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;

namespace Weft.Tests;

public sealed class PropertyBindingTests
{
    [Theory]
    [InlineData("class C { public int P { get; } } void Main() { new C().P = 1; }", "WF2023")]
    [InlineData("class C { public int P { get; private set; } } void Main() { new C().P++; }", "WF2011")]
    [InlineData("class C { public int P { private get => 1; set {} } } void Main() { Print(new C().P); }", "WF2011")]
    [InlineData("class C { private int P { get; set; } } void Main() { Print(new C().P); }", "WF2011")]
    [InlineData("class C { public int P { set {} } } void Main() { Print(new C().P); }", "WF2023")]
    [InlineData("class C { public int P { set {} } } void Main() { new C().P += 1; }", "WF2023")]
    [InlineData("class C { public int P => 1; public C() { P = 2; } }", "WF2023")]
    [InlineData("class C { public int P { get; } void M() { P = 2; } }", "WF2023")]
    [InlineData("class C { public string P { get; set; } }", "WF2022")]
    [InlineData("class C { public string P { get; } public C(bool b) { if (b) P = \"x\"; } }", "WF2022")]
    [InlineData("class C { public string P { get; } public C() { Print(P); P = \"x\"; } }", "WF2022")]
    [InlineData("class C { public string P { get; } public C() { var alias = this; P = \"x\"; } }", "WF2022")]
    [InlineData("class C { public int P { get; set; } = this.P; }", "WF2020")]
    [InlineData("class C { public int P { get; set; } static int M() => P; }", "WF2020")]
    [InlineData("class C { public int P { get; set; } } void Main() { Print(C.P); }", "WF2001")]
    [InlineData("class Hidden {} public class C { public Hidden P { get; } = new Hidden(); }", "WF2019")]
    [InlineData("class C { int P { get; } int P; }", "WF2002")]
    [InlineData("class C { int P { get; } int P() => 1; }", "WF2002")]
    [InlineData("class C { int P { get; } int P { get; } }", "WF2002")]
    [InlineData("void Run() {} class C { int Run { get; } void M() { Run(); } }", "WF2003")]
    [InlineData("class C { int Print { get; } void M() { Print(1); } }", "WF2003")]
    [InlineData("class C { int P {} }", "WF2024")]
    [InlineData("class C { int P { get; get; } }", "WF2024")]
    [InlineData("class C { int P { set; } }", "WF2024")]
    [InlineData("class C { int P { get; set {} } }", "WF2024")]
    [InlineData("class C { int P { get => 1; } = 3; }", "WF2024")]
    [InlineData("class C { public int P { public get; set; } }", "WF2024")]
    [InlineData("class C { public int P { private get; private set; } }", "WF2024")]
    [InlineData("class C { public int P { private get; } }", "WF2024")]
    [InlineData("class C { private int P { internal get; set; } }", "WF2024")]
    [InlineData("class C { int P { get { } } }", "WF2007")]
    [InlineData("class C { int P { get => true; } }", "WF2003")]
    [InlineData("class C { int P { get => 1; set { return 1; } } }", "WF2003")]
    [InlineData("class C { int P { get => value; } }", "WF2001")]
    [InlineData("class C { public int P { get; set; } } void Main() { new C().P = 1L; }", "WF2003")]
    [InlineData("class C { public string P { get; set; } = \"x\"; } void Main() { new C().P++; }", "WF2003")]
    [InlineData("class C { public static int P { get; set; } }", "WF2009")]
    [InlineData("class C { public int P { get; init; } }", "WF2009")]
    [InlineData("class C { public int P { get; } } void Main() { var c = new C(); c.P; }", "WF2008")]
    [InlineData("class C { public int P { get; } } void Main() { var c = new C(); for (; false; c.P) {} }", "WF2008")]
    public void Invalid_properties_are_rejected_before_emission(string source, string code)
    {
        var result = Compilation.Analyze("invalid", [new("invalid.weft", source)]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void Property_metadata_and_cross_file_types_are_retained()
    {
        var result = Compilation.Analyze("properties", [
            new("a.weft", "public class A { public B Item { get; } = new B(); }"),
            new("b.weft", "public class B {} void Main() { Print(new A().Item == new B()); }")]);
        Assert.Empty(result.Diagnostics);
        var property = Assert.Single(result.Properties);
        Assert.Equal("B", property.Type.Name);
        Assert.Equal("A", property.Owner.Name);
        Assert.NotNull(property.Getter);
        Assert.Null(property.Setter);
        Assert.True(property.BackingField!.ReadOnly);
    }

    [Fact]
    public void Setter_ir_checks_the_target_receiver_value_and_void_signature()
    {
        var module = Compilation.Analyze("ir", [new("ir.weft", "class C { public int P { get; set; } } void Main() { var c = new C(); c.P = 1; }")]).Module!;
        var main = module.Functions.Single(f => f.Symbol.Name == "Main");
        var setter = IrTraversal.Descendants(main.Body).OfType<IrSetterCall>().Single();
        var getter = module.Functions.Single(f => f.Symbol.Name.Contains("<get:")).Symbol;
        IrSetterCall[] invalid = [
            setter with { Setter = setter.Setter with { Id = 999 } },
            setter with { Receiver = new IrConstant(1, WeftType.Int32, setter.Origin) },
            setter with { Value = new IrConstant(true, WeftType.Bool, setter.Origin) },
            setter with { Setter = getter }
        ];
        foreach (var expression in invalid)
        {
            var changed = main with { Body = main.Body with { Statements = main.Body.Statements.SetItem(1, new IrExpressionStatement(expression, expression.Origin)) } };
            Assert.Contains(IrValidator.Validate(module with { Functions = module.Functions.Replace(main, changed) }), d => d.Code == "WF3001");
        }
    }
}
