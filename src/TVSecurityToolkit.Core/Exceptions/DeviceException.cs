namespace TVSecurityToolkit.Core.Exceptions;

public class DeviceException : Exception
{
    public DeviceException(string message) : base(message) { }
    public DeviceException(string message, Exception inner) : base(message, inner) { }
}
