namespace Weft.Tests;

public sealed class TestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "weft tests", Guid.NewGuid().ToString("N"));
    public TestWorkspace() => Directory.CreateDirectory(Root);
    public string Write(string name, string text)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }
    public void Dispose() => Directory.Delete(Root, recursive: true);
    public static string Repository
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Weft.slnx"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Cannot locate repository root.");
        }
    }
}
