using Weft.Compiler;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;

namespace Weft.Tests;

public sealed class InitializationTests
{
    [Theory]
    [InlineData("class C { public int X { get; init; } } void Main() { var c = new C(); c.X = 1; }", "WF2025")]
    [InlineData("class C { public int X { get; init; } } void Main() { new C().X++; }", "WF2025")]
    [InlineData("class C { public int X { get; init; } void M() { X = 1; } }", "WF2025")]
    [InlineData("class C { public int X { get; init; } public C(C other) { other.X = 1; } }", "WF2025")]
    [InlineData("class C { public int X { get; init; } } class D { public int Y { init { new C().X = value; } } }", "WF2025")]
    [InlineData("class C { public int X { get; private init; } } void Main() { new C { X = 1 }; }", "WF2011")]
    [InlineData("class C { public int X { get; } } void Main() { new C { X = 1 }; }", "WF2023")]
    [InlineData("class C { public readonly int X; } void Main() { new C { X = 1 }; }", "WF2021")]
    [InlineData("class C { public int X { get; set; init; } }", "WF2024")]
    [InlineData("class C { public int X { init; } }", "WF2024")]
    [InlineData("class C { public int X { get; init {} } }", "WF2024")]
    [InlineData("class C { public required string Name { get; init; } } void Main() { new C(); }", "WF2026")]
    [InlineData("class C { public required string Name; } void Main() { new C {}; }", "WF2026")]
    [InlineData("class C { public required int X = 2; public C() { X = 3; } } void Main() { new C(); }", "WF2026")]
    [InlineData("class C { public required int X { get; } }", "WF2027")]
    [InlineData("class C { public required readonly int X; }", "WF2027")]
    [InlineData("class C { private required int X; }", "WF2027")]
    [InlineData("public class C { internal required int X; }", "WF2027")]
    [InlineData("public class C { public required int X { get; private init; } }", "WF2027")]
    [InlineData("class C { public required string Name; public C() { Print(Name); } }", "WF2022")]
    [InlineData("class C { public required string Name; public C() { var x = this; } }", "WF2022")]
    [InlineData("class C { public required string Name; public C() { M(); } void M() {} }", "WF2022")]
    [InlineData("class C { public required string Name { get; init; } public int X { init { Print(Name); } } } void Main() { new C { X = 1, Name = \"x\" }; }", "WF2022")]
    [InlineData("class C { public int X; } void Main() { new C { X = 1, X = 2 }; }", "WF2028")]
    [InlineData("class C {} void Main() { new C { Missing = 1 }; }", "WF2001")]
    [InlineData("class C { private int X; } void Main() { new C { X = 1 }; }", "WF2011")]
    [InlineData("class C { public int X; } void Main() { new C { X = 1L }; }", "WF2003")]
    [InlineData("class C { public int X; } void Main() { new C { X = {} }; }", "WF2028")]
    [InlineData("class Item { public int X { get; init; } } class C { public Item Item { get; } = new Item(); } void Main() { new C { Item = { X = 2 } }; }", "WF2025")]
    [InlineData("class Item { public int X; } class C { public required Item Item; } void Main() { new C { Item = { X = 2 } }; }", "WF2026")]
    [InlineData("class C { public int X { get; init; } } void Main() { var other = new C(); new C { X = (other.X = 2) }; }", "WF2025")]
    [InlineData("class C { public string Name { get; init; } }", "WF2022")]
    [InlineData("class C { public int X { get; } public int Y { init { X = value; } } }", "WF2023")]
    [InlineData("class C { public readonly int X; public int Y { init { new C().X = value; } } }", "WF2021")]
    [InlineData("class C { private C Self => this; } void Main() { new C { Self = {} }; }", "WF2011")]
    [InlineData("class C {} void Main() { new C { Missing = {} }; }", "WF2001")]
    public void Invalid_initialization_is_rejected_before_emission(string source, string code)
    {
        var result = Compilation.Analyze("invalid", [new("invalid.weft", source)]);
        Assert.Null(result.Module);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void Required_and_init_metadata_survives_binding()
    {
        var result = Compilation.Analyze("metadata", [new("metadata.weft", "class C { public required string Name { get; init; } } void Main() { new C { Name = \"Ada\" }; }")]);
        Assert.Empty(result.Diagnostics);
        var property = Assert.Single(result.Properties);
        Assert.True(property.Required);
        Assert.True(property.InitOnly);
        Assert.True(property.Setter!.IsInitAccessor);
        Assert.True(property.BackingField!.Required);
        Assert.True(property.BackingField.ReadOnly);
    }

    [Fact]
    public void Initialization_ir_rejects_lost_scopes_fake_objects_and_reassignment()
    {
        var module = Compilation.Analyze("ir", [new("ir.weft", "class C { public int X { get; init; } } void Main() { var c = new C { X = 1 }; }")]).Module!;
        var main = module.Functions.Single(f => f.Symbol.Name == "Main");
        var declaration = (IrVariable)main.Body.Statements[0];
        var sequence = (IrSequence)declaration.Initializer;
        var instance = sequence.Initializing!;
        var origin = sequence.Origin;
        IrSequence[] invalid = [
            sequence with { Initializing = null },
            sequence with { Value = new IrConstant(1, WeftType.Int32, origin) },
            sequence with { Bindings = sequence.Bindings.SetItem(0, sequence.Bindings[0] with { Initializer = new IrAllocate(instance.Type, origin) }) },
            sequence with { Bindings = sequence.Bindings.Add(new(new(999, "replace", instance.Type, origin.Location), new IrAssign(instance, new IrRead(instance, origin), origin), origin)) }
        ];
        foreach (var expression in invalid)
        {
            var changed = main with { Body = main.Body with { Statements = [declaration with { Initializer = expression }] } };
            Assert.Contains(IrValidator.Validate(module with { Functions = module.Functions.Replace(main, changed) }), d => d.Code == "WF3001");
        }
    }
    [Fact]
    public void Constructors_cannot_lend_initialization_privileges_to_existing_objects()
    {
        var module = Compilation.Analyze("ir", [new("ir.weft", "class C { public int X { get; init; } public C() {} public C(C other) {} } void Main() { var old = new C(); var fresh = new C(old) { X = 1 }; }")]).Module!;
        var constructor = module.Functions.Single(f => f.Symbol.IsConstructor && f.Symbol.Parameters.Length == 1);
        var origin = constructor.Origin;
        var other = new IrRead(constructor.Symbol.Parameters[0], origin);
        var allocation = (IrVariable)constructor.Body.Statements[0];
        IrBlock[] invalid = [
            new([new IrReturn(other, origin)], origin),
            constructor.Body with { Statements = constructor.Body.Statements.SetItem(1, new IrReturn(other, origin)) },
            constructor.Body with { Statements = constructor.Body.Statements.SetItem(0, allocation with { Initializer = other }) },
            constructor.Body with { Statements = constructor.Body.Statements.Insert(1, new IrExpressionStatement(new IrAssign(constructor.Symbol.Receiver!, other, origin), origin)) }
        ];
        foreach (var body in invalid)
            Assert.Contains(IrValidator.Validate(module with { Functions = module.Functions.Replace(constructor, constructor with { Body = body }) }), d => d.Code == "WF3001");
    }
}
