using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.Engine.Validation;

public static class PolicyValidator
{
    public static (bool Allowed, string Reason) Check(TestDefinition d, TestPolicyEngine policy) => policy.Decide(d);
}
