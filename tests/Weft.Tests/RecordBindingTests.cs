using Weft.Compiler;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;

namespace Weft.Tests;

public sealed class RecordBindingTests
{
    [Theory]
    [InlineData("record R(int X); void Main() { var r = new R(1); r.X = 2; }", "WF2025")]
    [InlineData("record R(int X); void Main() { new R(); }", "WF2006")]
    [InlineData("record R(int X); void Main() { new R(1L); }", "WF2003")]
    [InlineData("record R(int X) { public R() {} }", "WF2031")]
    [InlineData("record R(int X) { public R() : base() {} }", "WF2031")]
    [InlineData("record R(int X, int X);", "WF2002")]
    [InlineData("record R(int X) { public string X = \"bad\"; }", "WF2031")]
    [InlineData("record R(int X) { public int X { set {} } }", "WF2031")]
    [InlineData("record R(int X) { public int X() => 1; }", "WF2031")]
    [InlineData("record R(string Name) { public string Name { get; init; } }", "WF2022")]
    [InlineData("record R(string Name) { public string Name { get; } = this.Name; }", "WF2020")]
    [InlineData("record R { public required int X; } void Main() { new R(); }", "WF2026")]
    [InlineData("record R { public required int X; } void Main() { var r = new R { X = 1 }; var c = r with { X = 1L }; }", "WF2003")]
    [InlineData("record R { public readonly int X; } void Main() { var c = new R() with { X = 1 }; }", "WF2021")]
    [InlineData("record R { public int X { get; } } void Main() { var c = new R() with { X = 1 }; }", "WF2023")]
    [InlineData("record R { private int X; } void Main() { var c = new R() with { X = 1 }; }", "WF2011")]
    [InlineData("record R { public int X { get; private init; } } void Main() { var c = new R() with { X = 1 }; }", "WF2011")]
    [InlineData("record R(int X); void Main() { var c = new R(1) with { X = 2, X = 3 }; }", "WF2028")]
    [InlineData("record R(int X); void Main() { var c = new R(1) with { Missing = 2 }; }", "WF2001")]
    [InlineData("record R(int X); void Main() { var r = new R(1); var c = r with { X = (r.X = 2) }; }", "WF2025")]
    [InlineData("record R(int X); void Main() { new R(1) with {}; }", "WF2008")]
    [InlineData("class C {} void Main() { var c = new C() with {}; }", "WF2030")]
    [InlineData("void Main() { var c = 1 with {}; }", "WF2030")]
    [InlineData("record R {} record S {} void Main() { Print(new R() == new S()); }", "WF2003")]
    [InlineData("record R { public R Child => this; } void Main() { var r = new R() with { Child = {} }; }", "WF2030")]
    [InlineData("static record R;", "WF2012")]
    [InlineData("static model M {}", "WF2012")]
    [InlineData("model M { string Name; } void Main() { new M(); }", "WF2026")]
    [InlineData("record R { public int GetHashCode() => 1; }", "WF2009")]
    [InlineData("record R { public bool Equals(R other) => true; }", "WF2009")]
    [InlineData("class C {} public record R(C Value);", "WF2019")]
    [InlineData("record R(R Value);", "WF2031")]
    [InlineData("record R(int X) { public R(R source) : this(source.X) {} }", "WF2031")]
    [InlineData("record R { public required string Name; public R(R source) {} }", "WF2022")]
    [InlineData("record R { public string Name = \"x\"; public R(R source) {} }", "WF2022")]
    public void Invalid_record_and_model_operations_are_rejected(string source, string code)
    {
        var result = Compilation.Analyze("invalid", [new("invalid.weft", source)]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void Data_kind_and_positional_contracts_survive_lowering()
    {
        var result = Compilation.Analyze("data", [new("data.weft", "model M { string Name; int N; } record R(string Name, int N = 2); class C {}")]);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(DataKind.Model, result.Types.Single(t => t.Name == "M").Kind);
        Assert.Equal(DataKind.Record, result.Types.Single(t => t.Name == "R").Kind);
        Assert.Equal(DataKind.Class, result.Types.Single(t => t.Name == "C").Kind);
        Assert.All(result.Properties, p => { Assert.True(p.InitOnly); Assert.Equal(Visibility.Public, p.Visibility); });
        var model = result.Module!.Classes.Single(t => t.Symbol.Name == "M");
        Assert.True(model.Fields.Single(f => f.Name == "Name").Required);
        Assert.All(model.Fields, f => Assert.Equal(Visibility.Public, f.Visibility));
        Assert.Empty(IrValidator.Validate(result.Module));
    }

    [Fact]
    public void Copy_ir_preserves_freshness_and_rejects_forged_initialization()
    {
        var module = Compilation.Analyze("copy", [new("copy.weft", "record R(int X); class C {} void Main() { var r = new R(1); var c = r with { X = 2 }; }")]).Module!;
        var main = module.Functions.Single(f => f.Symbol.Name == "Main");
        var declaration = (IrVariable)main.Body.Statements[1];
        var sequence = (IrSequence)declaration.Initializer;
        var copy = (IrCopy)sequence.Bindings[0].Initializer;
        IrSequence[] invalid = [
            sequence with { Initializing = null },
            sequence with { Bindings = sequence.Bindings.SetItem(0, sequence.Bindings[0] with { Initializer = copy.Receiver }) },
            sequence with { Value = copy.Receiver },
            sequence with { Bindings = sequence.Bindings.Add(new(new(999, "bad", copy.Type, copy.Origin.Location), new IrAssign(sequence.Initializing!, copy.Receiver, copy.Origin), copy.Origin)) }
        ];
        foreach (var expression in invalid)
        {
            var changed = main with { Body = main.Body with { Statements = main.Body.Statements.SetItem(1, declaration with { Initializer = expression }) } };
            Assert.Contains(IrValidator.Validate(module with { Functions = module.Functions.Replace(main, changed) }), d => d.Code == "WF3001");
        }
        var record = module.Classes.Single(c => c.Symbol.Kind == DataKind.Record);
        Assert.Contains(IrValidator.Validate(module with { Classes = module.Classes.Replace(record, record with { Symbol = record.Symbol with { Kind = DataKind.Class } }) }), d => d.Code == "WF3001");
    }
}
