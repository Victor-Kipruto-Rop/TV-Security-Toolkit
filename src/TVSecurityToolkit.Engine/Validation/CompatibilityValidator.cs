using TVSecurityToolkit.Core.Constants;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Engine.Validation;

/// <summary>Flags tests that use commands the protocol does not define.</summary>
public static class CompatibilityValidator
{
    public static List<string> Validate(TestDefinition d) =>
        d.Steps.Where(s => !ProtocolConstants.Commands.Contains(s.Call))
               .Select(s => $"unsupported command '{s.Call}'").Distinct().ToList();
}
