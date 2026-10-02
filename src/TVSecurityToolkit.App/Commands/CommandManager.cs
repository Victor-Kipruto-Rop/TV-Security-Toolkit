namespace TVSecurityToolkit.App.Commands;

/// <summary>Thin helper so view models can ask WPF to re-evaluate CanExecute.</summary>
public static class CommandRefresh
{
    public static void Invalidate() => System.Windows.Input.CommandManager.InvalidateRequerySuggested();
}
