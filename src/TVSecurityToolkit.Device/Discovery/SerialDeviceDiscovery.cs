using System.IO.Ports;
using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Device.Discovery;

public sealed class SerialDeviceDiscovery
{
    public List<DiscoveredDevice> Discover() =>
        SerialPort.GetPortNames().Select(p => new DiscoveredDevice(ConnectionType.Serial, p, "serial port")).ToList();
}
