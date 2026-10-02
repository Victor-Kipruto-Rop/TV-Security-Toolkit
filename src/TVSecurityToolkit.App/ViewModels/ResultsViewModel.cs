using System.Collections.ObjectModel;
using TVSecurityToolkit.App.Services;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class ResultsViewModel : ViewModelBase
{
    private TestResult? _selected;
    public ResultsViewModel(ApplicationService app) =>
        app.ReportChanged += () =>
        {
            Results.Clear();
            foreach (var r in app.LastReport!.Session.Results) Results.Add(r);
        };

    public ObservableCollection<TestResult> Results { get; } = new();
    public TestResult? Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value)) Raise(nameof(Evidence)); }
    }
    public string Evidence => _selected is null ? "" : string.Join(Environment.NewLine,
        _selected.Evidence.Select(e => $"{e.Call}  args={e.Args}  result={e.Result}" + (e.Errors.Count > 0 ? "  ERRORS: " + string.Join("; ", e.Errors) : "")));
}
