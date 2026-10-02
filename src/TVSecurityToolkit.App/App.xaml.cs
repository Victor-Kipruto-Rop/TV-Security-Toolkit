using System.Windows;
using TVSecurityToolkit.App.Services;
using Serilog;

namespace TVSecurityToolkit.App;

public partial class App : Application
{
    public static ApplicationService Services { get; } = new();

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            await Services.DisconnectAsync();
            Log.Information("Toolkit shutdown");
        }
        catch (Exception exception)
        {
            Log.Error("Error while closing the device session; error type {ErrorType}", exception.GetType().Name);
        }
        finally
        {
            try
            {
                await Log.CloseAndFlushAsync();
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }
}
