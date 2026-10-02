using TVSecurityToolkit.Device.PlatformAdapters;
using System.Net;
using System.Text;

namespace TVSecurityToolkit.UnitTests.Device;

public sealed class PlatformAdapterDiscoveryTests
{
    [Fact]
    public void Android_adb_list_reports_connected_devices_without_authenticating_identity()
    {
        var summary = PlatformAdapterDiscovery.SummarizeDeviceList(
            "Android TV / Google TV",
            "List of devices attached\r\n192.168.1.20:5555 device product:sample model:sample\r\n");

        Assert.Contains("1 connected candidate", summary);
        Assert.Contains("not authenticated", summary);
    }

    [Fact]
    public void Tizen_sdb_list_reports_unauthorized_device_without_claiming_compatibility()
    {
        var summary = PlatformAdapterDiscovery.SummarizeDeviceList(
            "Samsung Tizen",
            "List of devices attached\r\n192.168.1.21:26101 unauthorized\r\n");

        Assert.Contains("authorization", summary);
        Assert.Contains("not authenticated", summary);
    }

    [Fact]
    public void Webos_output_is_not_treated_as_validated_identity()
    {
        var summary = PlatformAdapterDiscovery.SummarizeDeviceList("LG webOS", "device list");

        Assert.Contains("format is not validated", summary);
        Assert.Contains("not authenticated", summary);
    }

    [Theory]
    [InlineData("192.168.1.30")]
    [InlineData("tv.local")]
    public void Roku_probe_builds_only_the_fixed_device_info_endpoint(string host)
    {
        Assert.True(RokuEcpAdapter.TryCreateDeviceInfoUri(host, out var uri));

        Assert.Equal("http", uri.Scheme);
        Assert.Equal(8060, uri.Port);
        Assert.Equal("/query/device-info", uri.AbsolutePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://192.168.1.30")]
    [InlineData("192.168.1.30/path")]
    [InlineData("user@192.168.1.30")]
    [InlineData("192.168.1.30?redirect=example.com")]
    public void Roku_probe_rejects_urls_and_non_host_input(string host)
    {
        Assert.False(RokuEcpAdapter.TryCreateDeviceInfoUri(host, out _));
    }

    [Fact]
    public async Task Roku_probe_displays_only_unverified_model_and_does_not_follow_redirects()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<device-info><model-name>Roku TV</model-name><device-id>secret-id</device-id></device-info>",
                Encoding.UTF8,
                "application/xml")
        });
        using var client = new HttpClient(handler);
        var adapter = new RokuEcpAdapter(client);

        var result = await adapter.ProbeAsync("192.168.1.30", CancellationToken.None);

        Assert.Contains("Reported model: Roku TV", result.Summary);
        Assert.Contains("not authenticated", result.Summary);
        Assert.DoesNotContain("secret-id", result.Summary);
        Assert.Equal("/query/device-info", handler.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Roku_probe_reports_redirect_without_following_it()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("http://example.invalid/") }
        });
        using var client = new HttpClient(handler);
        var adapter = new RokuEcpAdapter(client);

        var result = await adapter.ProbeAsync("192.168.1.30", CancellationToken.None);

        Assert.Contains("HTTP 302", result.Summary);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Roku_probe_reports_authentication_requirement_without_claiming_identity()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = new HttpClient(handler);
        var adapter = new RokuEcpAdapter(client);

        var result = await adapter.ProbeAsync("192.168.1.30", CancellationToken.None);

        Assert.Contains("requires authorization", result.Summary);
        Assert.Contains("no identity", result.Summary);
    }

    [Fact]
    public async Task Roku_probe_rejects_xml_with_document_type_declarations()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<!DOCTYPE device-info [<!ENTITY x SYSTEM \"file:///C:/secret\">]><device-info><model-name>&x;</model-name></device-info>",
                Encoding.UTF8,
                "application/xml")
        });
        using var client = new HttpClient(handler);
        var adapter = new RokuEcpAdapter(client);

        var result = await adapter.ProbeAsync("192.168.1.30", CancellationToken.None);

        Assert.Contains("invalid or unsupported XML", result.Summary);
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
