using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Core.Models;

public sealed class Device
{
    public string Name { get; set; } = "";
    public ConnectionType Connection { get; set; }
    public DeviceState State { get; set; } = DeviceState.Disconnected;
    public DeviceIdentity? Identity { get; set; }
    public FirmwareInfo? Firmware { get; set; }
    public HardwareInfo? Hardware { get; set; }
}

/// <summary>User-supplied connection parameters. Endpoint numbers are decimal (0x81 = 129).</summary>
public sealed class ConnectionSettings
{
    public ConnectionType Type { get; set; } = ConnectionType.Simulator;
    public int Vid { get; set; }
    public int Pid { get; set; }
    public int Interface { get; set; }
    public int EpOut { get; set; } = 1;
    public int EpIn { get; set; } = 129;
    public string SerialPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 115200;
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public int TimeoutMs { get; set; } = 5000;
    /// <summary>Simulator only: comma-separated protections to disable (e.g. accept_replay).</summary>
    public string Flaws { get; set; } = "";
}
