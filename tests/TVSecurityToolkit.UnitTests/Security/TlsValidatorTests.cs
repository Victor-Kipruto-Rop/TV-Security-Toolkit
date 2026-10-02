using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using TVSecurityToolkit.Security.Network;

namespace TVSecurityToolkit.UnitTests.Security;

public class TlsValidatorTests
{
    [Theory]
    [InlineData("Tls")]
    [InlineData("Tls11")]
    [InlineData("Ssl2")]
    [InlineData("Ssl3")]
    public void Legacy_protocols_are_detected(string protocolName)
    {
        var protocol = Enum.Parse<SslProtocols>(protocolName);

        Assert.True(TlsValidator.IsLegacy(protocol));
    }

    [Theory]
    [InlineData(SslProtocols.Tls12)]
    [InlineData(SslProtocols.Tls13)]
    public void Modern_protocols_are_not_detected_as_legacy(SslProtocols protocol)
    {
        Assert.False(TlsValidator.IsLegacy(protocol));
    }

    [Theory]
    [InlineData(SslProtocols.Tls12, "1.2", true)]
    [InlineData(SslProtocols.Tls12, "TLS 1.3", false)]
    [InlineData(SslProtocols.Tls13, "1.2", true)]
    [InlineData(SslProtocols.Tls13, "TLS1.3", true)]
    [InlineData(SslProtocols.None, "1.2", false)]
    public void Negotiated_protocol_is_checked_against_minimum(
        SslProtocols negotiated, string minimum, bool expected)
    {
        Assert.Equal(expected, TlsValidator.MeetsMinimum(negotiated, minimum));
    }

    [Fact]
    public void Unsupported_minimum_protocol_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => TlsValidator.MeetsMinimum(SslProtocols.Tls12, "1.4"));
    }

    [Fact]
    public async Task Probe_propagates_caller_cancellation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var accept = listener.AcceptTcpClientAsync();
            var probe = TlsValidator.ProbeAsync("127.0.0.1", port, SslProtocols.None, cancellation.Token);
            using var peer = await accept;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe);
        }
        finally
        {
            listener.Stop();
        }
    }
}
