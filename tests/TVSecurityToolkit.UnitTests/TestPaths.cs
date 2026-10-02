namespace TVSecurityToolkit.UnitTests;

public static class TestPaths
{
    /// <summary>Walks up from the test binary to the folder containing the solution file.</summary>
    public static string Root
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !File.Exists(Path.Combine(d.FullName, "TV-Security-Toolkit.sln"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }
    public static string Config(params string[] p) => Path.Combine(new[] { Root, "config" }.Concat(p).ToArray());
}
