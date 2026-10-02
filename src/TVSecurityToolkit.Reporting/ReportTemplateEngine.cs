using System.Net;

namespace TVSecurityToolkit.Reporting;

/// <summary>Tiny {{placeholder}} substitution. Values are inserted as-is; encode them first with Html().</summary>
public static class ReportTemplateEngine
{
    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        foreach (var kv in values) template = template.Replace("{{" + kv.Key + "}}", kv.Value);
        return template;
    }

    public static string Html(string? s) => WebUtility.HtmlEncode(s ?? "");
}
