namespace TVSecurityToolkit.Device.PlatformAdapters;

/// <summary>
/// Evidence-based platform inventory. Discovery availability must not be interpreted as
/// protocol support, device authentication, or permission to execute security tests.
/// </summary>
public sealed record TvPlatformProfile(
    string Id,
    string DisplayName,
    string OperatingSystem,
    string DiscoveryMethod,
    string SupportStatus,
    bool HasImplementedDiscovery,
    bool VendorProtocolVerified,
    bool DeviceAuthenticationVerified,
    string Notes)
{
    /// <summary>Real-device security tests stay disabled until all evidence gates are satisfied.</summary>
    public bool CanRunRealDeviceSecurityTests(bool explicitlyAuthorized) =>
        explicitlyAuthorized
        && VendorProtocolVerified
        && DeviceAuthenticationVerified;
}

public static class TvPlatformCompatibilityRegistry
{
    private static readonly IReadOnlyList<TvPlatformProfile> Profiles =
    [
        new("android-google-tv", "Android TV / Google TV", "Android TV / Google TV",
            "Official Android Debug Bridge (adb) device listing", "Discovery only", true, false, false,
            "ADB listing is not proof of device identity, vendor diagnostic protocol support, or test authorization."),
        new("samsung-tizen", "Samsung Tizen", "Tizen", "Samsung Smart Development Bridge (sdb) device listing",
            "Discovery only", true, false, false,
            "SDB discovery does not establish a supported security-test protocol or authenticate the TV."),
        new("lg-webos", "LG webOS", "webOS", "LG webOS CLI (ares-device) listing",
            "Discovery only", true, false, false,
            "CLI output format is not validated as an identity assertion; no hardware test protocol is established."),
        new("roku-tv", "Roku TV", "Roku OS", "Optional read-only ECP device-info request to one explicit host",
            "Read-only identification probe", true, false, false,
            "Model text is unverified. No network scanning, redirects, or security-test execution is implied."),
        new("hisense-vidaa", "Hisense VIDAA", "VIDAA (model/version dependent)", "Not implemented",
            "Not implemented", false, false, false,
            "Requires model-specific, vendor-documented discovery and an authorized diagnostic interface."),
        new("tcl", "TCL", "Model-dependent (including Google TV, Android TV, and Roku TV variants)",
            "Use the applicable verified OS profile; brand alone is insufficient", "Model/OS identification required",
            false, false, false,
            "TCL is a manufacturer, not one uniform TV platform. Identify the exact OS and model before selecting an adapter."),
        new("sony-bravia", "Sony BRAVIA", "Model-dependent (including Google TV / Android TV)",
            "Use the applicable verified OS profile; model/OS identification required", "Model/OS identification required",
            false, false, false,
            "BRAVIA models vary by year and OS. Android/Google TV discovery is not proof of BRAVIA hardware compatibility."),
        new("other", "Other TV brands/platforms", "Unknown until identified", "No generic discovery assumed",
            "Unsupported until assessed", false, false, false,
            "Do not guess a protocol. Add an adapter only with documented vendor protocol, authorization, and test fixtures.")
    ];

    public static IReadOnlyList<TvPlatformProfile> GetAll() => Profiles;

    public static TvPlatformProfile? Find(string id) =>
        Profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Conservative gate: profile evidence and explicit authorization are all required.
    /// Every shipped profile currently fails closed because no vendor protocol/authentication
    /// has been verified against an authorized production TV.
    /// </summary>
    public static bool CanRunRealDeviceSecurityTests(string platformId, bool explicitlyAuthorized)
    {
        var profile = Find(platformId);
        return profile?.CanRunRealDeviceSecurityTests(explicitlyAuthorized) ?? false;
    }
}
