using System.Collections.ObjectModel;
using TVSecurityToolkit.App.Commands;
using TVSecurityToolkit.App.Services;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class TestExecutionViewModel : ViewModelBase
{
    private readonly ApplicationService _app;
    private readonly TestSelectionViewModel _selection;
    private CancellationTokenSource? _cts;
    private int _done, _total;
    private string _summary = "Idle";
    private string _currentTest = "Ready";

    public TestExecutionViewModel(ApplicationService app, TestSelectionViewModel selection)
    {
        _app = app; _selection = selection;
        RunCommand = new AsyncRelayCommand(_ => RunAsync(), _ => app.Device is not null, e => { Summary = "Error: " + e.Message; app.Dialogs.ShowError(e.Message); });
        CancelCommand = new RelayCommand(_ => _cts?.Cancel());
    }

    public ObservableCollection<string> Log { get; } = new();
    public AsyncRelayCommand RunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public int Done { get => _done; private set { Set(ref _done, value); Raise(nameof(Percent)); } }
    public int Total { get => _total; private set { Set(ref _total, value); Raise(nameof(Percent)); } }
    public double Percent => Total == 0 ? 0 : 100.0 * Done / Total;
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public string CurrentTest { get => _currentTest; private set => Set(ref _currentTest, value); }

    private async Task RunAsync()
    {
        var ids = _selection.SelectedIds.ToList();
        Log.Clear(); Done = 0; Total = ids.Count; Summary = "Running...";
        _cts = new CancellationTokenSource();
        var progress = new Progress<TestResult>(r =>
        {
            if (r.Status == TVSecurityToolkit.Core.Enums.TestStatus.Running)
            {
                CurrentTest = $"{r.Id} - {r.Title}";
                Log.Add($"RUNNING   [{r.Severity}] {r.Id}");
                return;
            }
            Done++;
            CurrentTest = $"{r.Status}: {r.Title}";
            Log.Add($"{r.Status,-9} [{r.Severity}] {r.Id}" + (r.Message.Length > 0 ? "  - " + r.Message : ""));
            Serilog.Log.Information("Test {TestId} completed with {Status} at severity {Severity} in {DurationMs}ms",
                r.Id, r.Status, r.Severity, r.DurationMs);
        });
        var report = await _app.RunAsync(ids, progress, _cts.Token);
        Summary = $"Done: {report.Passed} passed, {report.Failed} failed, {report.Errors} errors, {report.Skipped} skipped, {report.Cancelled} cancelled";
        if (_app.LastEvidenceArchive is not null)
            Summary += Environment.NewLine + "Evidence saved: " + _app.LastEvidenceArchive;
        if (_app.LastJsonReport is not null)
            Summary += Environment.NewLine + "JSON report saved: " + _app.LastJsonReport;
        if (_app.LastGeneratedReportPaths.Count > 1)
            Summary += Environment.NewLine + "Additional reports saved: " +
                string.Join(", ", _app.LastGeneratedReportPaths.Where(p => p != _app.LastJsonReport));
        _app.Notifications.Notify(Summary);
    }
}
