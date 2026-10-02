using System.IO;
using System.Text.Json;
using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Device;
using TVSecurityToolkit.Device.Identity;
using TVSecurityToolkit.Engine;
using TVSecurityToolkit.Reporting;
using TVSecurityToolkit.Security.Cryptography;
using TVSecurityToolkit.Security.Policies;
using Serilog;
using Serilog.Formatting.Json;
using DeviceModel = TVSecurityToolkit.Core.Models.Device;

namespace TVSecurityToolkit.App.Services;

/// <summary>Composition root: loads config and the catalog, owns the connected device and the last report.</summary>
public sealed class ApplicationService
{
    private readonly string _base = AppContext.BaseDirectory;
    public SecurityPolicyEngine SecurityPolicy { get; }
    public SeverityEngine Severity { get; }
    public TestRegistry Registry { get; } = new();
    public TestEngine Engine { get; }
    public IEntitlementProvider Provider { get; }
    public NotificationService Notifications { get; } = new();
    public DialogService Dialogs { get; } = new();
    public NavigationService Navigation { get; } = new();

    public string EnvironmentName { get; set; } = "development";
    public string OutputDir { get; set; } = "output";
    public int TestTimeoutSeconds { get; private set; } = 60;
    public bool StopOnCriticalFailure { get; private set; }
    public IReadOnlyList<string> ReportFormats { get; private set; } = new[] { "html", "json", "pdf" };

    public IDeviceAdapter? Device { get; private set; }
    public DeviceModel? DeviceInfo { get; private set; }
    public SecurityReport? LastReport { get; private set; }
    public string? LastEvidenceArchive { get; private set; }
    public string? LastJsonReport { get; private set; }
    public IReadOnlyList<string> LastGeneratedReportPaths { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> Environments { get; }

    public event Action? DeviceChanged;
    public event Action? ReportChanged;

    /// <summary>
    /// Reads a required JSON configuration file and applies <paramref name="apply"/>. A missing or
    /// malformed file produces a clear, actionable error instead of a raw framework exception.
    /// </summary>
    private static JsonDocument LoadRequired(string path, Action<JsonElement> apply)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"Required configuration file not found: '{path}'. Restore the complete toolkit package and try again.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"Configuration file '{path}' is not valid JSON: {e.Message}", e);
        }
        catch (IOException e)
        {
            throw new InvalidOperationException($"Configuration file '{path}' could not be read: {e.Message}", e);
        }

        apply(doc.RootElement);
        return doc;
    }

    private void LoadRequiredSettings(string path)
    {
        using var _ = LoadRequired(path, root =>
        {
            EnvironmentName = root.TryGetProperty("defaultEnvironment", out var e0) && e0.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(e0.GetString())
                ? e0.GetString()!
                : "development";

            OutputDir = Path.Combine(_base,
                root.TryGetProperty("outputDir", out var e1) && e1.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(e1.GetString())
                    ? e1.GetString()!
                    : "output");

            ReportFormats = root.TryGetProperty("reportFormats", out var e2) && e2.ValueKind == JsonValueKind.Array
                ? e2.EnumerateArray()
                      .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                      .Select(x => x.GetString()!)
                      .ToList()
                : new List<string> { "html", "json", "pdf" };

            if (ReportFormats.Count == 0)
            {
                Log.Warning("No report formats configured; falling back to html and json");
                ReportFormats = new List<string> { "html", "json" };
            }
        });
    }

    public ApplicationService()
    {
        Directory.CreateDirectory(Path.Combine(_base, "logs"));
        var fileSink = new LoggerConfiguration()
            .WriteTo.File(
                new Serilog.Formatting.Json.JsonFormatter(renderMessage: true),
                Path.Combine(_base, "logs", "toolkit-.json"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 32L * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 14,
                shared: false)
            .CreateLogger();

        // Wrap the file sink so device identity values never reach disk.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(new IdentityRedactingSink(fileSink))
            .CreateLogger();
        Log.Information("Toolkit startup");

        // Fail with an actionable message when required configuration is missing or unreadable,
        // rather than an unhandled KeyNotFoundException or FileNotFoundException at startup.
        var settingsPath = Path.Combine(_base, "appsettings.json");
        LoadRequiredSettings(settingsPath);

        var testPolicyPath = Path.Combine(_base, "config", "test-policy.json");
        using var tp = LoadRequired(testPolicyPath, r =>
        {
            TestTimeoutSeconds = r.TryGetProperty("testTimeoutSeconds", out var t) && t.TryGetInt32(out var secs) && secs > 0
                ? secs : 60;
            StopOnCriticalFailure = r.TryGetProperty("stopOnCriticalFailure", out var s) && s.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                // Legacy snake_case key retained for backwards compatibility.
                JsonValueKind.String when bool.TryParse(s.GetString(), out var parsed) => parsed,
                _ => false
            };
        });

        SecurityPolicy = new SecurityPolicyEngine(Path.Combine(_base, "config", "security-policy.json"));
        Severity = SeverityEngine.Load(Path.Combine(_base, "config", "severity-rules.json"));
        Provider = new LabEntitlementProvider(SecurityPolicy.LabKey);

        Registry.LoadCatalog(Path.Combine(_base, "test-catalog"));
        Registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.PayG.Replay.ReplayProtectionTest).Assembly);
        Registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Firmware.Integrity.FirmwareHashTest).Assembly);
        Registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Update.Valid.ValidUpdateTest).Assembly);
        Registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Network.TLS.TlsConfigurationTest).Assembly);
        Registry.RegisterAssembly(typeof(TVSecurityToolkit.Tests.Local.Storage.SecureStorageTest).Assembly);
        Engine = new TestEngine(Registry);

        Environments = Directory.EnumerateFiles(Path.Combine(_base, "config", "environments"), "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(n => n).ToList();
        Log.Information("Toolkit initialized with {EnvironmentCount} environments and {TestCount} registered tests",
            Environments.Count, Registry.Tests.Count);
    }

    public async Task ConnectAsync(ConnectionSettings settings, CancellationToken ct)
    {
        await DisconnectAsync();
        var candidate = DeviceFactory.Create(settings, SecurityPolicy.LabKey);
        try
        {
            var deviceInfo = await new DeviceIdentityService(candidate).DescribeAsync(settings.Type, ct);
            Device = candidate;
            DeviceInfo = deviceInfo;
        }
        catch
        {
            await candidate.DisposeAsync();
            Log.Warning("Device connection failed for {ConnectionType}", settings.Type);
            throw;
        }
        Log.Information("Device connected using {ConnectionType}", settings.Type);
        DeviceChanged?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        var wasConnected = Device is not null;
        if (Device is not null) await Device.DisposeAsync();
        Device = null; DeviceInfo = null;
        if (wasConnected) Log.Information("Device disconnected");
        DeviceChanged?.Invoke();
    }

    public async Task<SecurityReport> RunAsync(IEnumerable<string> ids, IProgress<TestResult>? progress, CancellationToken ct)
    {
        if (Device is null) throw new InvalidOperationException("Connect to a device first.");
        if (DeviceInfo?.Connection != ConnectionType.Simulator)
            throw new InvalidOperationException(
                "Security test execution is disabled for hardware transports until a vendor-authenticated TV diagnostic protocol and target compatibility profile are configured and verified. The current connection provides transport access only.");
        LastEvidenceArchive = null;
        LastJsonReport = null;
        LastGeneratedReportPaths = Array.Empty<string>();
        var selectedIds = ids.ToArray();
        Log.Information("Test run started for {TestCount} selected tests in {Environment}",
            selectedIds.Length, EnvironmentName);
        var ctx = new TestContext
        {
            Device = Device, Registry = Registry, Provider = Provider, Environment = EnvironmentName,
            Policy = TestPolicyEngine.Load(Path.Combine(_base, "config", "environments", EnvironmentName + ".json")),
            PayloadsDir = Path.Combine(_base, "payloads"), TestTimeoutSeconds = TestTimeoutSeconds,
            StopOnCriticalFailure = StopOnCriticalFailure
        };
        try
        {
            LastReport = await Engine.RunAsync(ctx, Engine.Select(selectedIds), progress, ct);
            LastReport.Device = DeviceInfo;
            foreach (var finding in LastReport.Findings)
            {
                finding.FirmwareVersion = DeviceInfo?.Firmware?.Version ?? "";
                finding.ObservedUtc = LastReport.Session.FinishedUtc ?? DateTime.UtcNow;
            }
        }
        catch (Exception e)
        {
            Log.Error("Test run failed before a report could be produced; error type {ErrorType}", e.GetType().Name);
            throw;
        }
        var storageFailures = new List<string>();
        var evidenceDirectory = Path.Combine(OutputDir, "evidence", LastReport.Session.Id);
        var reportDirectory = Path.Combine(OutputDir, "reports", LastReport.Session.Id);
        try
        {
            LastEvidenceArchive = await new EvidenceArchiveWriter().WriteAsync(
                LastReport, evidenceDirectory, CancellationToken.None);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Error("Evidence archive could not be saved; error type {ErrorType}", e.GetType().Name);
            storageFailures.Add($"Evidence archive could not be saved to '{evidenceDirectory}': {e.Message}");
        }
        try
        {
            var formats = ReportFormats.Concat(new[] { "html", "json", "pdf" })
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var paths = await new ReportGenerator().GenerateAsync(
                LastReport, formats, reportDirectory, CancellationToken.None);
            LastGeneratedReportPaths = paths;
            LastJsonReport = paths.SingleOrDefault(p =>
                string.Equals(Path.GetExtension(p), ".json", StringComparison.OrdinalIgnoreCase));
            Log.Information("Generated {ReportCount} report files", paths.Count);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Error("Configured reports could not be saved; error type {ErrorType}", e.GetType().Name);
            storageFailures.Add($"Configured reports could not be saved to '{reportDirectory}': {e.Message}");
        }
        Log.Information(
            "Test run finished: {Passed} passed, {Failed} failed, {Errors} errors, {Skipped} skipped, {Cancelled} cancelled",
            LastReport.Passed, LastReport.Failed, LastReport.Errors, LastReport.Skipped, LastReport.Cancelled);
        ReportChanged?.Invoke();
        if (storageFailures.Count > 0)
            throw new IOException("Tests completed, but one or more result files could not be saved: " + string.Join(" ", storageFailures));
        return LastReport;
    }

    public async Task<List<string>> GenerateReportsAsync(IEnumerable<string> formats, CancellationToken ct)
    {
        if (LastReport is null) throw new InvalidOperationException("Run tests first.");
        var paths = await new ReportGenerator().GenerateAsync(LastReport, formats, Path.Combine(OutputDir, "reports"), ct);
        Log.Information("Generated {ReportCount} report files", paths.Count);
        return paths;
    }
}
