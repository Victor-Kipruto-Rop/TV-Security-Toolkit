using System.Text.RegularExpressions;

namespace TVSecurityToolkit.UnitTests.App;

/// <summary>
/// Static verification of the WPF design system. A missing <c>StaticResource</c> key is a runtime
/// crash in WPF, so these checks guard every resource reference in every view.
/// </summary>
public class XamlResourceTests
{
    private static string AppDir => Path.Combine(TestPaths.Root, "src", "TVSecurityToolkit.App");

    private static IEnumerable<string> XamlFiles(string subdirectory)
    {
        var dir = Path.Combine(AppDir, subdirectory);
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories)
            : Array.Empty<string>();
    }

    /// <summary>Keys declared with x:Key plus implicit keys from unkeyed TargetType styles.</summary>
    private static HashSet<string> DeclaredResourceKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in XamlFiles("Resources"))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"x:Key\s*=\s*""([^""]+)"""))
                keys.Add(m.Groups[1].Value);

            // An unkeyed <Style TargetType="X"> is addressable as "X" and as "{x:Type X}".
            foreach (Match m in Regex.Matches(text, @"<Style\s+TargetType\s*=\s*""([^""]+)""(?![^>]*x:Key)"))
            {
                keys.Add(m.Groups[1].Value);
                keys.Add($"{{x:Type {m.Groups[1].Value}}}");
            }
        }
        return keys;
    }

    [Fact]
    public void Every_static_resource_reference_resolves()
    {
        var declared = DeclaredResourceKeys();
        var missing = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in XamlFiles(".").Concat(XamlFiles("Resources")))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\{(?:StaticResource)\s+([A-Za-z_][\w.:]*|x:Type\s+[\w.]+)\s*\}"))
            {
                var key = m.Groups[1].Value.Trim();
                if (!declared.Contains(key))
                    missing.Add($"{Path.GetFileName(file)}: {key}");
            }
        }

        Assert.True(missing.Count == 0,
            "unresolved StaticResource references (these crash at runtime):\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void Every_style_basedon_reference_resolves()
    {
        var declared = DeclaredResourceKeys();
        var missing = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in XamlFiles(".").Concat(XamlFiles("Resources")))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"BasedOn\s*=\s*""\{StaticResource\s+([A-Za-z_][\w.:]*|x:Type\s+[\w.]+)\s*\}"""))
                if (!declared.Contains(m.Groups[1].Value.Trim()))
                    missing.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value.Trim()}");

        Assert.True(missing.Count == 0, "unresolved BasedOn:\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void No_view_uses_a_hardcoded_foreground_or_background_colour()
    {
        // Colours must come from the design system so the theme stays centrally controlled.
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        var viewsDir = Path.Combine(AppDir, "Views");
        if (!Directory.Exists(viewsDir)) return;

        foreach (var file in Directory.EnumerateFiles(viewsDir, "*.xaml", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"(?:Foreground|Background)\s*=\s*""#[0-9A-Fa-f]{3,8}"""))
                offenders.Add($"{Path.GetFileName(file)}: {m.Value}");

        Assert.True(offenders.Count == 0,
            "hardcoded colours in views (use a resource brush instead):\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Every_keyed_style_is_applied_to_a_matching_element_type()
    {
        // Applying a TextBox-targeted style to a ListBox throws at runtime ("TargetType does not
        // match type of element"). This compares each keyed Style's TargetType against the element
        // it is applied to in every view.
        var styles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in XamlFiles("Resources"))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text,
                @"<Style\s+x:Key\s*=\s*""([^""]+)""[^>]*?TargetType\s*=\s*""([^""]+)""",
                RegexOptions.Singleline))
                styles[m.Groups[1].Value] = m.Groups[2].Value;
        }

        var mismatches = new SortedSet<string>(StringComparer.Ordinal);
        var viewsDir = Path.Combine(AppDir, "Views");
        if (!Directory.Exists(viewsDir)) return;

        foreach (var file in Directory.EnumerateFiles(viewsDir, "*.xaml", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file),
                @"<(\w+)[^>]*?Style\s*=\s*""\{StaticResource\s+([A-Za-z_][\w.:]*)\s*\}""",
                RegexOptions.Singleline))
            {
                var element = m.Groups[1].Value;
                var key = m.Groups[2].Value;
                if (!styles.TryGetValue(key, out var target)) continue;
                if (!string.Equals(element, target, StringComparison.Ordinal))
                    mismatches.Add($"{Path.GetFileName(file)}: '{key}' targets {target} but is applied to <{element}>");
            }
        }

        Assert.True(mismatches.Count == 0,
            "style TargetType mismatches (these throw at runtime):\n  " + string.Join("\n  ", mismatches));
    }

    [Fact]
    public void Design_system_declares_its_required_brushes()
    {
        var declared = DeclaredResourceKeys();
        foreach (var required in new[]
        {
            "WindowBrush", "SurfaceBrush", "SurfaceSubtleBrush", "SidebarBrush", "SidebarTextBrush",
            "BorderBrushLight", "BorderBrushStrong", "TextBrush", "MutedBrush",
            "PrimaryBrush", "PrimaryDarkBrush", "AccentBrush", "DangerBrush", "SuccessBrush",
            "WarnBrush", "InfoBrush", "Card", "Title", "SectionTitle", "Eyebrow", "Muted",
            "SecondaryButton", "BoolToVis"
        })
            Assert.True(declared.Contains(required), $"design system is missing '{required}'");
    }
}