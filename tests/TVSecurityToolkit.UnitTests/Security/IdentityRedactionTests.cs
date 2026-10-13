using System.Text.Json;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TVSecurityToolkit.Security.Policies;

namespace TVSecurityToolkit.UnitTests.Security;

public sealed class IdentityRedactionTests
{
    [Fact]
    public void Redaction_preserves_valid_json_and_masks_identity_fields()
    {
        const string input = """{"device_id":"SIM-0001","serial":"SERIAL-123","macAddress":"00:11:22:33:44:55","safe":"kept"}""";

        var redacted = IdentityRedactingSink.Redact(input);

        using var parsed = JsonDocument.Parse(redacted);
        var root = parsed.RootElement;
        Assert.Equal("[redacted]", root.GetProperty("device_id").GetString());
        Assert.Equal("[redacted]", root.GetProperty("serial").GetString());
        Assert.Equal("[redacted]", root.GetProperty("macAddress").GetString());
        Assert.Equal("kept", root.GetProperty("safe").GetString());
        Assert.DoesNotContain("SIM-0001", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("SERIAL-123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("00:11:22:33:44:55", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redaction_handles_empty_and_null_input()
    {
        Assert.Equal(string.Empty, IdentityRedactingSink.Redact(string.Empty));
        Assert.Null(IdentityRedactingSink.Redact(null!));
    }

    [Fact]
    public void Redaction_masks_bare_simulator_identifiers()
    {
        var redacted = IdentityRedactingSink.Redact("probe returned SIM-0001 and SIM-ABC-2");

        Assert.DoesNotContain("SIM-0001", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("SIM-ABC-2", redacted, StringComparison.Ordinal);
        Assert.Equal(2, redacted.Split("[redacted]", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Sink_redacts_identifiers_embedded_in_literal_message_text()
    {
        var capture = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(new IdentityRedactingSink(capture))
            .CreateLogger();

        logger.Information("Connected to device SIM-0001");

        Assert.NotNull(capture.LastEvent);
        Assert.DoesNotContain("SIM-0001", capture.LastEvent!.RenderMessage(), StringComparison.Ordinal);
        Assert.Contains("[redacted]", capture.LastEvent.RenderMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Sink_redacts_structured_identity_properties_and_exception_text()
    {
        var capture = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(new IdentityRedactingSink(capture))
            .CreateLogger();

        logger.Information("Device {DeviceId} connected", "SIM-0001");
        Assert.NotNull(capture.LastEvent);
        Assert.DoesNotContain("SIM-0001", capture.LastEvent!.RenderMessage(), StringComparison.Ordinal);

        logger.Error(new InvalidOperationException("connection failed for SIM-ABC-2"), "device request failed");
        Assert.NotNull(capture.LastEvent!.Exception);
        Assert.DoesNotContain("SIM-ABC-2", capture.LastEvent.Exception!.ToString(), StringComparison.Ordinal);
        Assert.Contains("[redacted]", capture.LastEvent.Exception.ToString(), StringComparison.Ordinal);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? LastEvent { get; private set; }
        public void Emit(LogEvent logEvent) => LastEvent = logEvent;
    }
}
