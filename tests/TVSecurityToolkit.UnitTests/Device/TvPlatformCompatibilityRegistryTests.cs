using TVSecurityToolkit.Device.PlatformAdapters;

namespace TVSecurityToolkit.UnitTests.Device;

public sealed class TvPlatformCompatibilityRegistryTests
{
    [Theory]
    [InlineData("android-google-tv")]
    [InlineData("samsung-tizen")]
    [InlineData("lg-webos")]
    [InlineData("roku-tv")]
    [InlineData("hisense-vidaa")]
    [InlineData("tcl")]
    [InlineData("sony-bravia")]
    [InlineData("other")]
    public void Requested_platforms_are_documented_in_the_registry(string id)
    {
        var profile = TvPlatformCompatibilityRegistry.Find(id);

        Assert.NotNull(profile);
        Assert.False(string.IsNullOrWhiteSpace(profile!.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(profile.SupportStatus));
        Assert.False(string.IsNullOrWhiteSpace(profile.Notes));
    }

    [Theory]
    [InlineData("android-google-tv")]
    [InlineData("samsung-tizen")]
    [InlineData("lg-webos")]
    [InlineData("roku-tv")]
    [InlineData("hisense-vidaa")]
    [InlineData("tcl")]
    [InlineData("sony-bravia")]
    [InlineData("other")]
    public void Real_device_security_tests_fail_closed_without_verified_vendor_evidence(string id)
    {
        Assert.False(TvPlatformCompatibilityRegistry.CanRunRealDeviceSecurityTests(id, explicitlyAuthorized: false));
        Assert.False(TvPlatformCompatibilityRegistry.CanRunRealDeviceSecurityTests(id, explicitlyAuthorized: true));
    }

    [Fact]
    public void Unknown_platform_fails_closed()
    {
        Assert.Null(TvPlatformCompatibilityRegistry.Find("unknown-brand"));
        Assert.False(TvPlatformCompatibilityRegistry.CanRunRealDeviceSecurityTests("unknown-brand", true));
    }

    [Fact]
    public void Every_shipped_profile_requires_future_vendor_verification()
    {
        Assert.All(TvPlatformCompatibilityRegistry.GetAll(), profile =>
        {
            Assert.False(profile.VendorProtocolVerified);
            Assert.False(profile.DeviceAuthenticationVerified);
            Assert.False(profile.CanRunRealDeviceSecurityTests(explicitlyAuthorized: true));
        });
    }

    [Fact]
    public void Registry_is_not_empty_and_ids_are_unique()
    {
        var profiles = TvPlatformCompatibilityRegistry.GetAll();

        Assert.NotEmpty(profiles);
        Assert.Equal(profiles.Count, profiles.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
