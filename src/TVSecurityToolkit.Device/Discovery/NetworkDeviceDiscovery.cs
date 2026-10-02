using System.Net.Sockets;
using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Device.Discovery;

/// <summary>Checks a caller-supplied list of "host:port" endpoints for reachability (no network scanning).</summary>
public sealed class NetworkDeviceDiscovery
{
    public async Task<List<DiscoveredDevice>> DiscoverAsync(IEnumerable<string> endpoints, CancellationToken ct)
    {
        var found = new List<DiscoveredDevice>();
        foreach (var ep in endpoints)
        {
            var parts = ep.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port)) continue;
            try
            {
                using var tcp = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(500);
                await tcp.ConnectAsync(parts[0], port, cts.Token);
                found.Add(new DiscoveredDevice(ConnectionType.Network, ep, "reachable"));
            }
            catch (Exception e) when (e is SocketException or OperationCanceledException) { }
        }
        return found;
    }
}
