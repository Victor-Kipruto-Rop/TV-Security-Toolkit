using LibUsbDotNet;
using LibUsbDotNet.Main;
using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Device.Discovery;

public sealed class UsbDeviceDiscovery
{
    public List<DiscoveredDevice> Discover()
    {
        var list = new List<DiscoveredDevice>();
        foreach (UsbRegistry reg in UsbDevice.AllDevices)
            list.Add(new DiscoveredDevice(ConnectionType.Usb, $"{reg.Vid:x4}:{reg.Pid:x4}", reg.Name ?? "", reg.Vid, reg.Pid));
        return list;
    }
}
