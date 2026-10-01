using Weft.Compiler;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;

namespace Weft.Tests;

public sealed class ConstructorChainTests
{
    [Theory]
    [InlineData("class C { public C() : this() {} }", "WF2029")]
    [InlineData("class C { public C() : this(1) {} public C(int n) : this() {} }", "WF2029")]
    [InlineData("class C { public C() : this(1) {} public C(int n) : this(\"x\") {} public C(string s) : this() {} }", "WF2029")]
    [InlineData("class C { public C(int n = 1) : this() {} }", "WF2029")]
    [InlineData("class C { public C() : this(1) {} }", "WF2006")]
    [InlineData("class C { public C() : this(true) {} public C(int n) {} }", "WF2006")]
    [InlineData("class C { public C() : this(missing: 1) {} public C(int n) {} }", "WF2006")]
    [InlineData("class C { public C() : this(x: 1, 2) {} public C(int n, int x) {} }", "WF2006")]
    [InlineData("class C { public C() : this(1, 1) {} public C(int a, long b) {} public C(long a, int b) {} }", "WF2015")]
    [InlineData("class C { int x; public C() : this(x) {} public C(int n) {} }", "WF2020")]
    [InlineData("class C { int P { get; } public C() : this(P) {} public C(int n) {} }", "WF2020")]
    [InlineData("class C { public C() : this(this) {} public C(C other) {} }", "WF2020")]
    [InlineData("class C { int M() => 1; public C() : this(M()) {} public C(int n) {} }", "WF2020")]
    [InlineData("class C { int x; public C() : this(this.x) {} public C(int n) {} }", "WF2020")]
    [InlineData("class C { public C() : base(1) {} }", "WF2009")]
    [InlineData("class C { public C() : other() {} }", "WF1105")]
    [InlineData("class C { void M() : this() {} }", "WF1105")]
    [InlineData("class C { string Name; public C() : this(1) {} public C(int n) {} }", "WF2022")]
    [InlineData("class C { public required string Name; public C() : this(true) { Print(Name); } public C(bool skip) { if (skip) return; Name = \"x\"; } }", "WF2022")]
    [InlineData("class C { public required string Name; public C() : this(true) { var x = this; } public C(bool b) { if (b) Name = \"x\"; } }", "WF2022")]
    [InlineData("class C { public required string Name; public C() : this(1) {} public C(int n) { Name = \"x\"; } } void Main() { new C(); }", "WF2026")]
    [InlineData("class C { public C() : this(1); public C(int n); }", "WF2009")]
    public void Invalid_constructor_chains_are_rejected_before_emission(string source, string code)
    {
        var result = Compilation.Analyze("invalid", [new("invalid.weft", source)]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void Chained_initialization_handles_forward_types_and_all_returning_paths()
    {
        var result = Compilation.Analyze("forward", [
            new("a.weft", "namespace Demo; class C { public string Name { get; } public C() : this(true) { Print(Name); } public C(bool early) { if (early) { Name = GetName(); return; } Name = GetName(); } }"),
            new("b.weft", "namespace Demo; string GetName() => \"Ada\"; void Main() { new C(); }")]);
        Assert.Empty(result.Diagnostics);
        var constructors = result.Module!.Functions.Where(f => f.Symbol.IsConstructor).ToArray();
        Assert.IsType<IrAllocate>(((IrVariable)constructors[0].Body.Statements[0]).Initializer);
        Assert.IsType<IrCall>(((IrVariable)constructors[1].Body.Statements[0]).Initializer);
    }

    [Fact]
    public void Circular_initializer_diagnostic_identifies_the_cycle()
    {
        var result = Compilation.Analyze("cycle", [new("cycle.weft", "class C { public C() : this(1) {} public C(int n) : this() {} }")]);
        var cycle = Assert.Single(result.Diagnostics, d => d.Code == "WF2029");
        Assert.Contains("C..ctor() -> C..ctor(int32) -> C..ctor()", cycle.Message);
        Assert.Equal(3, cycle.Related!.Count);
    }

    [Fact]
    public void Ir_constructor_delegation_must_be_acyclic_and_constructor_only()
    {
        var module = Compilation.Analyze("ir", [new("ir.weft", "class C { public C() {} public C(int n) {} } C Make() => new C(); void Main() {}")]).Module!;
        var empty = module.Functions.Single(f => f.Symbol.IsConstructor && f.Symbol.Parameters.Length == 0);
        var integer = module.Functions.Single(f => f.Symbol.IsConstructor && f.Symbol.Parameters.Length == 1);
        var make = module.Functions.Single(f => f.Symbol.Name == "Make");
        var origin = empty.Origin;
        IrFunction Delegate(IrFunction source, IrExpression target)
        {
            var allocation = (IrVariable)source.Body.Statements[0];
            return source with { Body = source.Body with { Statements = source.Body.Statements.SetItem(0, allocation with { Initializer = target }) } };
        }
        IrModule[] invalid = [
            module with { Functions = module.Functions.Replace(empty, Delegate(empty, new IrCall(empty.Symbol, [], origin))) },
            module with { Functions = module.Functions.Replace(empty, Delegate(empty, new IrCall(integer.Symbol, [new IrConstant(1, WeftType.Int32, origin)], origin)))
                .Replace(integer, Delegate(integer, new IrCall(empty.Symbol, [], origin))) },
            module with { Functions = module.Functions.Replace(empty, Delegate(empty, new IrCall(make.Symbol, [], origin))) }
        ];
        foreach (var bad in invalid) Assert.Contains(IrValidator.Validate(bad), d => d.Code == "WF3001");
    }
}
