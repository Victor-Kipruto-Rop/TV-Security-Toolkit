using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Core.Interfaces;

public interface IEvidenceCollector
{
    void Record(string call, JsonNode? args, JsonNode? result, IReadOnlyList<string> errors);
    List<Evidence> Drain();
}
