using System.Collections.ObjectModel;
using TVSecurityToolkit.App.Services;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class FindingsViewModel : ViewModelBase
{
    private Finding? _selected;

    public FindingsViewModel(ApplicationService app)
    {
        app.ReportChanged += () => LoadFindings(app.LastReport);
        LoadFindings(app.LastReport);
    }

    private void LoadFindings(SecurityReport? report)
    {
        Findings.Clear();
        if (report is not null)
        {
            foreach (var finding in report.Findings) Findings.Add(finding);
        }
        Selected = Findings.FirstOrDefault();
    }

    public ObservableCollection<Finding> Findings { get; } = new();
    public Finding? Selected
    {
        get => _selected;
        set { if (Set(ref _selected, value)) Raise(nameof(Details)); }
    }
    public string Details => Selected is null ? "" :
        $"Finding: {Selected.Id}{Environment.NewLine}" +
        $"Component: {Selected.AffectedComponent}{Environment.NewLine}" +
        $"Device: {Selected.DeviceName}{Environment.NewLine}" +
        $"Firmware: {Selected.FirmwareVersion}{Environment.NewLine}" +
        $"Expected: {Selected.ExpectedBehavior}{Environment.NewLine}" +
        $"Actual: {Selected.ActualBehavior}{Environment.NewLine}" +
        $"Evidence: {string.Join(", ", Selected.EvidenceCalls)}{Environment.NewLine}" +
        $"Recommendation: {Selected.Recommendation}{Environment.NewLine}" +
        $"Observed UTC: {Selected.ObservedUtc:u}{Environment.NewLine}" +
        $"Details: {Selected.Message}";
}
