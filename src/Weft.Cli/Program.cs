using System.Text.Json;
using Weft.Backend.DotNet;
using Weft.Backend.Jvm;
using Weft.Compiler;
using Weft.Compiler.Backends;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.Text;

namespace Weft.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { return await ExecuteAsync(args, Console.Out, Console.Error, cancellation.Token); }
        finally { Console.CancelKeyPress -= cancel; }
    }

    public static async Task<int> ExecuteAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellation = default)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            await output.WriteLineAsync("weft <check|build|run|emit> [--project weft.toml] [--backend dotnet|jvm] [--output directory] [--diagnostics json]");
            return args.Length == 0 ? 2 : 0;
        }
        var command = args[0];
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (command is not ("check" or "build" or "run" or "emit")) return await Usage("Unknown command '" + command + "'.");
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] is not ("--project" or "--backend" or "--output" or "--diagnostics") || i + 1 == args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) return await Usage("Unknown option or missing option value: " + args[i]);
            var key = args[i++];
            if (!options.TryAdd(key, args[i])) return await Usage("Repeated option: " + key);
        }
        if (options.TryGetValue("--diagnostics", out var format) && format != "json") return await Usage("--diagnostics accepts 'json'.");
        if (options.TryGetValue("--backend", out var selected) && selected is not ("dotnet" or "jvm")) return await Usage("--backend accepts 'dotnet' or 'jvm'.");
        var projectPath = options.GetValueOrDefault("--project", "weft.toml");
        try
        {
            var loaded = ProjectLoader.Load(projectPath);
            await Report(loaded.Diagnostics);
            if (loaded.Project is not { } project) return 1;
            var analyzed = Compilation.Analyze(project.Name, project.Sources.Select(path => new SourceText(path, File.ReadAllText(path))));
            await Report(analyzed.Diagnostics);
            if (analyzed.Module is not { } module) return 1;
            var diagnostics = new DiagnosticBag();
            var entry = EntryPoint.Resolve(module, project.Entry, diagnostics);
            await Report(diagnostics);
            if (entry is null) return 1;
            if (command == "check") { await output.WriteLineAsync("Check succeeded."); return 0; }
            var backend = options.GetValueOrDefault("--backend", project.Backend);
            var destination = Path.GetFullPath(options.GetValueOrDefault("--output", Path.Combine(Path.GetDirectoryName(project.Path)!, ".weft", backend)));
            if (command == "emit")
            {
                var source = backend == "dotnet" ? new DotNetEmitter().Generate(module, entry) : new JvmEmitter().Generate(module, entry);
                Directory.CreateDirectory(destination);
                await File.WriteAllTextAsync(Path.Combine(destination, source.FileName), source.Text, cancellation);
                await File.WriteAllTextAsync(Path.Combine(destination, source.FileName + ".map.json"), JsonSerializer.Serialize(source.Lines), cancellation);
                await output.WriteLineAsync(Path.Combine(destination, source.FileName));
                return 0;
            }
            var built = backend == "dotnet" ? await new DotNetEmitter().BuildAsync(module, project.Entry, destination, cancellation: cancellation)
                : await new JvmEmitter().BuildAsync(module, project.Entry, destination, cancellation: cancellation);
            await Report(built.Diagnostics);
            if (!built.Success) return 1;
            if (command == "build") { await output.WriteLineAsync(built.Artifact); return 0; }
            var result = await ToolProcess.RunAsync(backend == "dotnet" ? "dotnet" : "java", backend == "dotnet" ? [built.Artifact!] : ["-jar", built.Artifact!], Path.GetDirectoryName(project.Path), cancellation);
            await output.WriteAsync(result.StandardOutput);
            await error.WriteAsync(result.StandardError);
            return result.ExitCode;
        }
        catch (OperationCanceledException) { await error.WriteLineAsync("WF5006: Operation canceled."); return 130; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            await Report([new("WF5005", exception.Message, new(projectPath, 0, 0, 1, 1))]);
            return 1;
        }
        async Task Report(IEnumerable<Diagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
                await error.WriteLineAsync(format == "json" ? JsonSerializer.Serialize(diagnostic) : diagnostic.ToString());
        }
        async Task<int> Usage(string message) { await error.WriteLineAsync(message); return 2; }
    }
}
