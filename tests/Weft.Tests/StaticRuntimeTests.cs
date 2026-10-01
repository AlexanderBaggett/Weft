using Weft.Backend.DotNet;
using Weft.Backend.Jvm;
using Weft.Compiler;
using Weft.Compiler.Backends;

namespace Weft.Tests;

public sealed class StaticRuntimeTests
{
    [Theory]
    [InlineData("dotnet", false)]
    [InlineData("jvm", false)]
    [InlineData("dotnet", true)]
    [InlineData("jvm", true)]
    public async Task Native_initialization_serializes_first_use_and_remembers_failure(string backend, bool failing)
    {
        using var workspace = new TestWorkspace();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var source = failing ? """
            class C {
                public static string Value = Read();
                static string Read() { Print("attempt"); return Value; }
                public static string Access() => Value;
            }
            void Main() { Print(C.Access()); }
            """ : """
            class C {
                public static int Count;
                static C() { Print("initialized"); Count++; }
                public static int Access() => Count;
            }
            void Main() { Print(C.Access()); }
            """;
        var compilation = Compilation.Analyze("static-runtime", [new("static.weft", source)]);
        Assert.Empty(compilation.Diagnostics);
        var module = compilation.Module!;
        var entry = module.Functions.Single(f => f.Symbol.Name == "Main").Symbol;
        var access = module.Functions.Single(f => f.Symbol.Name == "C.Access").Symbol;
        var initializer = module.Functions.Single(f => f.Symbol.IsTypeInitializer).Symbol;
        var generated = backend == "dotnet" ? new DotNetEmitter().Generate(module, entry) : new JvmEmitter().Generate(module, entry);
        // Weft exceptions and task/thread syntax are still being implemented. This
        // host harness exercises the actual generated type initializer and access
        // gates without depending on stack formatting, sleeps, or timing thresholds.
        string harness;
        if (failing)
        {
            harness = backend == "dotnet" ? $$"""
                try { f_{{access.Id}}(); throw new System.Exception("Initialization unexpectedly succeeded"); }
                catch (System.TypeInitializationException error) {
                    System.Exception cause = error;
                    while (cause.InnerException != null) cause = cause.InnerException;
                    if (!cause.Message.Contains("Static field 'C.Value' was read before initialization.")) throw;
                    System.Console.WriteLine("guarded");
                }
                try { f_{{access.Id}}(); throw new System.Exception("Initialization was retried"); }
                catch (System.TypeInitializationException) { System.Console.WriteLine("failed-again"); }
                """ : $$"""
                try { f_{{access.Id}}(); throw new AssertionError("Initialization unexpectedly succeeded"); }
                catch (ExceptionInInitializerError error) {
                    Throwable cause = error;
                    while (cause.getCause() != null) cause = cause.getCause();
                    if (!cause.getMessage().contains("Static field 'C.Value' was read before initialization.")) throw error;
                    System.out.println("guarded");
                }
                try { f_{{access.Id}}(); throw new AssertionError("Initialization was retried"); }
                catch (NoClassDefFoundError error) { System.out.println("failed-again"); }
                """;
        }
        else
        {
            var state = backend == "dotnet" ? """
                private static readonly System.Threading.ManualResetEventSlim started = new(false), release = new(false), start = new(false);
                private static readonly System.Threading.CountdownEvent attempted = new(8);
                """ : """
                private static final java.util.concurrent.CountDownLatch started = new java.util.concurrent.CountDownLatch(1), release = new java.util.concurrent.CountDownLatch(1), start = new java.util.concurrent.CountDownLatch(1), attempted = new java.util.concurrent.CountDownLatch(8);
                private static void waitFor(java.util.concurrent.CountDownLatch latch) {
                    try { latch.await(); } catch (InterruptedException error) { throw new AssertionError(error); }
                }
                """;
            var mainSignature = backend == "dotnet" ? "public static int Main" : "public static void main";
            var text = generated.Text.Replace(mainSignature, state + "\n" + mainSignature, StringComparison.Ordinal);
            var signature = $"private static void f_{initializer.Id}()\n{{";
            Assert.Contains(signature, text, StringComparison.Ordinal);
            text = text.Replace(signature, signature + (backend == "dotnet" ? "\nstarted.Set(); release.Wait();" : "\nstarted.countDown(); waitFor(release);"), StringComparison.Ordinal);
            generated = generated with { Text = text };
            harness = backend == "dotnet" ? $$"""
                var workers = new System.Threading.Thread[8];
                var values = new int[8];
                for (int i = 0; i < workers.Length; i++) {
                    int index = i;
                    workers[i] = new System.Threading.Thread(() => { start.Wait(); attempted.Signal(); values[index] = f_{{access.Id}}(); });
                    workers[i].Start();
                }
                start.Set(); started.Wait(); attempted.Wait(); release.Set();
                foreach (var worker in workers) worker.Join();
                int sum = 0; foreach (var value in values) sum += value;
                System.Console.WriteLine(sum);
                """ : $$"""
                Thread[] workers = new Thread[8];
                int[] values = new int[8];
                for (int i = 0; i < workers.length; i++) {
                    final int index = i;
                    workers[i] = new Thread(() -> { waitFor(start); attempted.countDown(); values[index] = f_{{access.Id}}(); });
                    workers[i].start();
                }
                start.countDown(); waitFor(started); waitFor(attempted); release.countDown();
                for (Thread worker : workers) {
                    try { worker.join(); } catch (InterruptedException error) { throw new AssertionError(error); }
                }
                int sum = 0; for (int value : values) sum += value;
                System.out.println(sum);
                """;
        }
        generated = generated with { Text = generated.Text.Replace($"f_{entry.Id}();", harness, StringComparison.Ordinal) };
        var built = backend == "dotnet"
            ? await new DotNetEmitter().CompileGeneratedAsync(generated, "static-runtime", workspace.Root, timeout.Token)
            : await new JvmEmitter().CompileGeneratedAsync(generated, "static-runtime", workspace.Root, timeout.Token);
        Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
        var ran = await ToolProcess.RunAsync(backend == "dotnet" ? "dotnet" : "java", backend == "dotnet" ? [built.Artifact!] : ["-jar", built.Artifact!], workspace.Root, timeout.Token);
        Assert.Equal(0, ran.ExitCode);
        Assert.Equal(failing ? "attempt\nguarded\nfailed-again\n" : "initialized\n8\n", ran.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal("", ran.StandardError);
    }
}
