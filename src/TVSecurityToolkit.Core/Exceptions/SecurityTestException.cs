namespace TVSecurityToolkit.Core.Exceptions;

public class SecurityTestException : Exception
{
    public SecurityTestException(string message) : base(message) { }
    public SecurityTestException(string message, Exception inner) : base(message, inner) { }
}
