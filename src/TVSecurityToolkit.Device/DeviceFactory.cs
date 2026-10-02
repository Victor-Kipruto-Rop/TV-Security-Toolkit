using TVSecurityToolkit.Core.Enums;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;
using TVSecurityToolkit.Device.Communication;
using TVSecurityToolkit.Device.Simulation;

namespace TVSecurityToolkit.Device;

public static class DeviceFactory
{
    public static IDeviceAdapter Create(ConnectionSettings s, byte[] labKey)
    {
        switch (s.Type)
        {
            case ConnectionType.Simulator:
                return new SimulatedTvAdapter(labKey, s.Flaws.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            case ConnectionType.Usb:
                if (s.Vid == 0 || s.Pid == 0) throw new DeviceException("USB VID/PID not set");
                return Wrap(new UsbTransport(s), s);
            case ConnectionType.Serial:
                return Wrap(new SerialTransport(s), s);
            case ConnectionType.Network:
                return Wrap(s.Host.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new HttpTransport(s.Host) : new TcpTransport(s), s);
            default:
                throw new DeviceException("unsupported connection type " + s.Type);
        }
    }

    private static IDeviceAdapter Wrap(IDeviceTransport t, ConnectionSettings s) =>
        new ProtocolDeviceAdapter(t.Name, new ProtocolClient(t, s.TimeoutMs));
}
