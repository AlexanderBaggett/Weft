using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.Text;

namespace Weft.Cli;

public sealed record ProjectManifest(string Path, string Name, string Entry, string Backend, ImmutableArray<string> Sources);
public sealed record ManifestResult(ProjectManifest? Project, ImmutableArray<Diagnostic> Diagnostics);

public static partial class ProjectLoader
{
    public static ManifestResult Load(string projectPath)
    {
        var path = System.IO.Path.GetFullPath(Directory.Exists(projectPath) ? System.IO.Path.Combine(projectPath, "weft.toml") : projectPath);
        var diagnostics = new DiagnosticBag();
        var location = new SourceLocation(path, 0, 0, 1, 1);
        if (!File.Exists(path)) return new(null, [new("WF5001", "Project manifest does not exist.", location)]);
        var source = new SourceText(path, File.ReadAllText(path));
        var syntax = Toml.Parse(source.Text, path);
        if (syntax.HasErrors)
        {
            foreach (var message in syntax.Diagnostics)
                diagnostics.Error("WF5002", message.Message, source.Location(Math.Clamp(message.Span.Offset, 0, source.Text.Length)));
            return new(null, diagnostics.ToImmutableArray());
        }
        var root = syntax.ToModel();
        foreach (var key in root.Keys.Where(k => k is not ("project" or "build" or "source-groups")))
            diagnostics.Error("WF5003", $"Manifest section '{key}' is unknown or requires a later semantic pass; it cannot be silently ignored.", location);
        var project = Table(root, "project", required: true);
        var build = Table(root, "build", required: false);
        var sourceGroups = Table(root, "source-groups", required: false);
        Keys(project, "project", "name", "entry", "sources");
        Keys(build, "build", "backend");
        var name = String(project, "name", "");
        if (!SafeName().IsMatch(name)) diagnostics.Error("WF5003", "project.name must start with a letter and contain only letters, digits, '_' or '-'.", location);
        var entry = String(project, "entry", "Main");
        var backend = String(build, "backend", "dotnet");
        if (backend is not ("dotnet" or "jvm")) diagnostics.Error("WF5003", "build.backend must be 'dotnet' or 'jvm'.", location);
        var patterns = Strings(project, "sources", ["**/*.weft", "**/*.rules"]).ToList();
        foreach (var group in sourceGroups.Keys.Order(StringComparer.Ordinal)) patterns.AddRange(Strings(sourceGroups, group, []));
        var directory = System.IO.Path.GetDirectoryName(path)!;
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var pattern in patterns)
        {
            if (System.IO.Path.IsPathRooted(pattern) || pattern.Split('/').Any(p => p == "..") || pattern.Contains('\\') || pattern.Contains('[') || pattern.Contains('{'))
            {
                diagnostics.Error("WF5003", $"Source pattern '{pattern}' must be project-relative and use only '*', '**', or '?' wildcards.", location);
                continue;
            }
            var glob = Glob(pattern);
            var matched = false;
            foreach (var candidate in EnumerateSources(directory))
            {
                var relative = System.IO.Path.GetRelativePath(directory, candidate).Replace('\\', '/');
                if (!glob.IsMatch(relative)) continue;
                files.Add(candidate); matched = true;
            }
            if (!matched && !pattern.Contains('*') && !pattern.Contains('?')) diagnostics.Error("WF5004", $"Source '{pattern}' does not exist or is not a .weft/.rules file.", location);
        }
        if (files.Count == 0) diagnostics.Error("WF5004", "Project contains no matching .weft or .rules source files.", location);
        return new(diagnostics.HasErrors ? null : new(path, name, entry, backend, files.ToImmutableArray()), diagnostics.ToImmutableArray());

        TomlTable Table(TomlTable table, string key, bool required)
        {
            if (table.TryGetValue(key, out var value) && value is TomlTable found) return found;
            if (required || table.ContainsKey(key)) diagnostics.Error("WF5003", $"'{key}' must be a TOML table.", location);
            return new();
        }
        void Keys(TomlTable table, string section, params string[] allowed)
        {
            foreach (var key in table.Keys.Except(allowed, StringComparer.Ordinal)) diagnostics.Error("WF5003", $"Unknown key '{section}.{key}'.", location);
        }
        string String(TomlTable table, string key, string fallback)
        {
            if (!table.TryGetValue(key, out var value)) return fallback;
            if (value is string text && text.Length > 0) return text;
            diagnostics.Error("WF5003", $"'{key}' must be a nonempty string.", location);
            return fallback;
        }
        string[] Strings(TomlTable table, string key, string[] fallback)
        {
            if (!table.TryGetValue(key, out var value)) return fallback;
            if (value is TomlArray array && array.All(item => item is string { Length: > 0 })) return array.Cast<string>().ToArray();
            diagnostics.Error("WF5003", $"'{key}' must be an array of nonempty source patterns.", location);
            return [];
        }
    }
    private static IEnumerable<string> EnumerateSources(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
            if (System.IO.Path.GetExtension(file) is ".weft" or ".rules") yield return file;
        foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (System.IO.Path.GetFileName(child) is ".git" or ".weft" or "bin" or "obj" or "artifacts") continue;
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
            foreach (var file in EnumerateSources(child)) yield return file;
        }
    }
    private static Regex Glob(string pattern)
    {
        var regex = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                i++;
                if (i + 1 < pattern.Length && pattern[i + 1] == '/') { i++; regex.Append("(?:.*/)?"); }
                else regex.Append(".*");
            }
            else regex.Append(pattern[i] switch { '*' => "[^/]*", '?' => "[^/]", _ => Regex.Escape(pattern[i].ToString()) });
        }
        return new(regex.Append('$').ToString(), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();
}
