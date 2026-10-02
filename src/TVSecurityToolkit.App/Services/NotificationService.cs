namespace TVSecurityToolkit.App.Services;

public sealed class NotificationService
{
    public event Action<string>? Notified;
    public string Last { get; private set; } = "Ready";
    public void Notify(string message) { Last = message; Notified?.Invoke(message); }
}
