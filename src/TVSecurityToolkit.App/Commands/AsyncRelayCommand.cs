using System.Windows.Input;

namespace TVSecurityToolkit.App.Commands;

/// <summary>Runs an async action; disabled while running. Exceptions go to onError instead of crashing the UI.</summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private bool _running;

    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null, Action<Exception>? onError = null)
    {
        _execute = execute; _canExecute = canExecute; _onError = onError;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _running = true; CommandRefresh.Invalidate();
        try { await _execute(parameter); }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (_onError is not null) _onError(e); else throw; }
        finally { _running = false; CommandRefresh.Invalidate(); }
    }
}
