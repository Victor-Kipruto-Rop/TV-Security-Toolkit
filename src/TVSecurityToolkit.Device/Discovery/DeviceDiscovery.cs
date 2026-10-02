namespace TVSecurityToolkit.Device.Discovery;

public sealed class DeviceDiscovery
{
    public async Task<List<DiscoveredDevice>> DiscoverAllAsync(
        IEnumerable<string> networkEndpoints,
        CancellationToken ct,
        Action<string, Exception>? onError = null)
    {
        var all = new List<DiscoveredDevice>();
        try { all.AddRange(new UsbDeviceDiscovery().Discover()); }
        catch (Exception e) when (e is DllNotFoundException or TypeInitializationException or UnauthorizedAccessException or IOException)
        {
            onError?.Invoke("USB discovery unavailable; check the USB driver and runtime", e);
        }
        try { all.AddRange(new SerialDeviceDiscovery().Discover()); }
        catch (Exception e) when (e is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
        {
            onError?.Invoke("Serial-port discovery unavailable", e);
        }
        all.AddRange(await new NetworkDeviceDiscovery().DiscoverAsync(networkEndpoints, ct));
        return all;
    }
}
