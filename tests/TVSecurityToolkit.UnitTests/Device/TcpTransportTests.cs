using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Device.Communication;

namespace TVSecurityToolkit.UnitTests.Device;

public class TcpTransportTests
{
    [Fact]
    public async Task Call_fails_promptly_when_peer_closes_connection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var accept = listener.AcceptTcpClientAsync(timeout.Token);
            await using var protocol = new ProtocolClient(
                new TcpTransport(new ConnectionSettings { Host = IPAddress.Loopback.ToString(), Port = port }),
                timeoutMs: 5000);

            var call = protocol.CallAsync("info", new JsonObject(), timeout.Token);
            using var peer = await accept;
            var stream = peer.GetStream();
            var header = new byte[TVSecurityToolkit.Core.Constants.ProtocolConstants.HeaderSize];
            await stream.ReadExactlyAsync(header, timeout.Token);
            var payloadLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(5, 4)));
            await stream.ReadExactlyAsync(new byte[payloadLength + 4], timeout.Token);
            peer.Close();

            var stopwatch = Stopwatch.StartNew();
            var error = await Assert.ThrowsAsync<DeviceException>(() => call);

            Assert.Contains("connection closed", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Call took {stopwatch.Elapsed}");
        }
        finally
        {
            listener.Stop();
        }
    }
}
