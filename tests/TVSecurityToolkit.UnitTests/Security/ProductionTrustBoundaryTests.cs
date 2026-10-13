using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.UnitTests.Security;

public sealed class ProductionTrustBoundaryTests
{
    [Fact]
    public void Production_readonly_profile_refuses_bundled_lab_key()
    {
        var policy = new SecurityPolicyEngine(TestPaths.Config("security-policy.json"));

        var error = Assert.Throws<InvalidOperationException>(
            () => policy.EnsureProfileMayUseLabKey("production-readonly"));

        Assert.Contains("simulator-only lab signing key", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("development")]
    [InlineData("staging")]
    [InlineData("simulator")]
    public void Non_production_profiles_can_use_lab_key(string profileName)
    {
        var policy = new SecurityPolicyEngine(TestPaths.Config("security-policy.json"));

        policy.EnsureProfileMayUseLabKey(profileName);
    }

    [Fact]
    public void Production_profile_guard_is_case_insensitive()
    {
        var policy = new SecurityPolicyEngine(TestPaths.Config("security-policy.json"));

        Assert.Throws<InvalidOperationException>(
            () => policy.EnsureProfileMayUseLabKey("PRODUCTION-READONLY"));
    }
}
