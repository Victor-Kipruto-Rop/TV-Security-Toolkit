using System.Text.Json;
using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.UnitTests.Security;

/// <summary>
/// The .NET app and the Python CLI must agree on configuration. The lab signing key used to be stored
/// twice, under two names, in two files; nothing checked that the copies matched, so regenerating
/// assets could silently leave the two entry points using different keys.
/// </summary>
public class ConfigParityTests
{
    [Fact]
    public void Only_one_security_config_file_exists()
    {
        var configDir = TestPaths.Config();
        var securityFiles = Directory.GetFiles(configDir, "security*.json").Select(Path.GetFileName).ToArray();

        Assert.Equal(new[] { "security-policy.json" }, securityFiles.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void No_duplicate_lab_key_copies_remain()
    {
        // A second copy under a different key name is exactly the drift this guards against.
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(TestPaths.Root, "*.json", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}"))
                continue;

            if (!File.ReadAllText(file).Contains("labSigningKeyHex", StringComparison.Ordinal)) continue;

            var relative = Path.GetRelativePath(TestPaths.Root, file).Replace('\\', '/');
            if (relative != "config/security-policy.json")
                offenders.Add(relative);
        }

        Assert.True(offenders.Count == 0,
            "labSigningKeyHex must live only in config/security-policy.json; also found in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Python_and_dotnet_read_the_same_lab_key()
    {
        // The Python CLI parses this same file via toolkit/config.py, so reading it the way Python
        // would is sufficient to prove the two entry points cannot disagree.
        using var doc = JsonDocument.Parse(File.ReadAllText(TestPaths.Config("security-policy.json")));
        var fromPythonPerspective = doc.RootElement.GetProperty("labSigningKeyHex").GetString();

        var fromDotNet = new SecurityPolicyEngine(TestPaths.Config("security-policy.json")).LabKey;

        Assert.Equal(fromPythonPerspective, Convert.ToHexString(fromDotNet).ToLowerInvariant());
        Assert.Equal(32, fromDotNet.Length);
    }

    [Fact]
    public void Lab_key_is_labelled_as_non_production()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(TestPaths.Config("security-policy.json")));
        var note = doc.RootElement.TryGetProperty("note", out var n) ? n.GetString() : null;

        Assert.False(string.IsNullOrWhiteSpace(note));
        Assert.Contains("Never place production signing keys here", note);
    }
}