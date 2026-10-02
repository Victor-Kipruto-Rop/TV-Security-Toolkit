using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.Device.Discovery;

public sealed record DiscoveredDevice(ConnectionType Type, string Id, string Description, int Vid = 0, int Pid = 0);
