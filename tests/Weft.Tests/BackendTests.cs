using System.Collections.Immutable;
using Weft.Backend.DotNet;
using Weft.Backend.Jvm;
using Weft.Compiler;
using Weft.Compiler.Backends;
using Weft.Compiler.IR;
using Weft.Compiler.Semantics;
using Weft.Compiler.Text;

namespace Weft.Tests;

public sealed class BackendTests
{
    [Fact]
    public async Task A_canceled_tool_invocation_does_not_attempt_to_start_a_process()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ToolProcess.RunAsync("weft-process-that-must-not-be-started", [], cancellation: cancellation.Token));
    }

    private static IrModule Program() => Compilation.Analyze("sample", [new("sample.weft", "void Main() { Print(42); }")]).Module!;

    [Theory]
    [InlineData("dotnet")]
    [InlineData("jvm")]
    public async Task Runtime_integer_division_matches_CSharp_overflow_and_zero_behavior(string backend)
    {
        using var workspace = new TestWorkspace();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // Weft try/catch is Phase 2 work. A small host harness observes the real runtime
        // boundary without asserting platform-specific unhandled-exception output.
        var source = backend == "dotnet" ? """
            class WeftProgram {
                static void Main() {
                    try { Weft.Runtime.RuntimeContract.Divide(int.MinValue, -1); }
                    catch (System.OverflowException) { System.Console.WriteLine("overflow32"); }
                    try { Weft.Runtime.RuntimeContract.Divide(long.MinValue, -1L); }
                    catch (System.OverflowException) { System.Console.WriteLine("overflow64"); }
                    try { Weft.Runtime.RuntimeContract.Divide(1, 0); }
                    catch (System.DivideByZeroException) { System.Console.WriteLine("zero32"); }
                    try { Weft.Runtime.RuntimeContract.Divide(1L, 0L); }
                    catch (System.DivideByZeroException) { System.Console.WriteLine("zero64"); }
                    System.Console.WriteLine(Weft.Runtime.RuntimeContract.Divide(-7, 3));
                    System.Console.WriteLine(Weft.Runtime.RuntimeContract.Divide(-7L, 3L));
                }
            }
            """ : """
            public class WeftProgram {
                public static void main(String[] args) {
                    try { weft.runtime.RuntimeContract.divide(Integer.MIN_VALUE, -1); }
                    catch (ArithmeticException e) { System.out.println("overflow32"); }
                    try { weft.runtime.RuntimeContract.divide(Long.MIN_VALUE, -1L); }
                    catch (ArithmeticException e) { System.out.println("overflow64"); }
                    try { weft.runtime.RuntimeContract.divide(1, 0); }
                    catch (ArithmeticException e) { System.out.println("zero32"); }
                    try { weft.runtime.RuntimeContract.divide(1L, 0L); }
                    catch (ArithmeticException e) { System.out.println("zero64"); }
                    System.out.println(weft.runtime.RuntimeContract.divide(-7, 3));
                    System.out.println(weft.runtime.RuntimeContract.divide(-7L, 3L));
                }
            }
            """;
        var generated = new GeneratedSource(backend == "dotnet" ? "WeftProgram.cs" : "WeftProgram.java", source, []);
        var built = backend == "dotnet"
            ? await new DotNetEmitter().CompileGeneratedAsync(generated, "division", workspace.Root, timeout.Token)
            : await new JvmEmitter().CompileGeneratedAsync(generated, "division", workspace.Root, timeout.Token);
        Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
        var result = await ToolProcess.RunAsync(backend == "dotnet" ? "dotnet" : "java",
            backend == "dotnet" ? [built.Artifact!] : ["-jar", built.Artifact!], workspace.Root, timeout.Token);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("overflow32\noverflow64\nzero32\nzero64\n-2\n-2\n", result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Empty(result.StandardError);
    }

    [Theory]
    [InlineData("dotnet")]
    [InlineData("jvm")]
    public async Task Missing_and_incompatible_runtime_contracts_are_rejected(string backend)
    {
        using var workspace = new TestWorkspace();
        foreach (var descriptor in new[] { new RuntimeDescriptor("99", Intrinsics.Bootstrap), new RuntimeDescriptor("1", []) })
        {
            var result = backend == "dotnet" ? await new DotNetEmitter().BuildAsync(Program(), "Main", workspace.Root, descriptor)
                : await new JvmEmitter().BuildAsync(Program(), "Main", workspace.Root, descriptor);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == (descriptor.Abi == "99" ? "WF4001" : "WF4002"));
        }
    }

    [Theory]
    [InlineData("dotnet")]
    [InlineData("jvm")]
    public async Task Real_downstream_compiler_errors_map_to_original_weft_origin(string backend)
    {
        using var workspace = new TestWorkspace();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var origin = new SourceOrigin(new("policy.rules", 80, 4, 9, 7), "insert-rule", new(new("receiver.weft", 12, 1, 2, 4)));
        var text = backend == "dotnet" ? "class WeftProgram { static void Main() { Missing(); } }" : "public class WeftProgram { public static void main(String[] args) { Missing(); } }";
        var generated = new GeneratedSource(backend == "dotnet" ? "WeftProgram.cs" : "WeftProgram.java", text, [new(1, origin)]);
        var result = backend == "dotnet" ? await new DotNetEmitter().CompileGeneratedAsync(generated, "broken", workspace.Root, timeout.Token)
            : await new JvmEmitter().CompileGeneratedAsync(generated, "broken", workspace.Root, timeout.Token);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == (backend == "dotnet" ? "WF4101" : "WF4201") && d.Location == origin.Location);
        Assert.Contains("receiver.weft", File.ReadAllText(Path.Combine(workspace.Root, generated.FileName + ".map.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_ir_is_reported_instead_of_crashing_or_reaching_a_backend()
    {
        var module = Program();
        var function = module.Functions[0];
        var origin = function.Origin;
        var local = new VariableSymbol(500, "absent", WeftType.Int32, origin.Location);
        IrExpression[] invalid =
        [
            new IrConstant("wrong", WeftType.Int32, origin),
            new IrRead(local, origin),
            new IrUnary("!", new IrConstant(2, WeftType.Int32, origin), WeftType.Int32, origin),
            new IrBinary(new IrConstant(1, WeftType.Int32, origin), "+", new IrConstant(2, WeftType.Int32, origin), WeftType.Bool, origin),
            new IrIntrinsic(Intrinsics.Print, [], origin),
            new IrCall(function.Symbol with { Id = 999 }, [], origin),
            new IrCall(function.Symbol, [], origin, [0]),
            new IrConvert(new IrConstant(true, WeftType.Bool, origin), WeftType.Int64, origin)
        ];
        foreach (var expression in invalid)
        {
            var malformed = module with { Functions = [function with { Body = new([new IrExpressionStatement(expression, origin)], origin) }] };
            Assert.Contains(IrValidator.Validate(malformed), d => d.Code == "WF3001");
        }
        Assert.Contains(IrValidator.Validate(module with { Functions = [function, function] }), d => d.Message.Contains("Duplicate", StringComparison.Ordinal));
        Assert.Contains(IrValidator.Validate(module with { RequiredIntrinsics = [] }), d => d.Message.Contains("absent", StringComparison.Ordinal));
    }
}
