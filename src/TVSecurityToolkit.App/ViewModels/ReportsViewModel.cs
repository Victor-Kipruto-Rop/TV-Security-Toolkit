using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using TVSecurityToolkit.App.Commands;
using TVSecurityToolkit.App.Services;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private readonly ApplicationService _app;
    private bool _html, _json, _pdf;

    public ReportsViewModel(ApplicationService app)
    {
        _app = app;
        app.ReportChanged += () =>
        {
            foreach (var path in app.LastGeneratedReportPaths)
                if (!Generated.Contains(path))
                    Generated.Add(path);
        };
        _html = app.ReportFormats.Contains("html", StringComparer.OrdinalIgnoreCase);
        _json = app.ReportFormats.Contains("json", StringComparer.OrdinalIgnoreCase);
        _pdf = app.ReportFormats.Contains("pdf", StringComparer.OrdinalIgnoreCase);
        GenerateCommand = new AsyncRelayCommand(_ => GenerateAsync(), _ => app.LastReport is not null, e => app.Dialogs.ShowError(e.Message));
        OpenFolderCommand = new RelayCommand(_ =>
        {
            var dir = Path.Combine(app.OutputDir, "reports"); Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        });
        OpenEvidenceFolderCommand = new RelayCommand(_ =>
        {
            var dir = Path.Combine(app.OutputDir, "evidence"); Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        });
    }

    public bool UseHtml { get => _html; set => Set(ref _html, value); }
    public bool UseJson { get => _json; set => Set(ref _json, value); }
    public bool UsePdf { get => _pdf; set => Set(ref _pdf, value); }
    public ObservableCollection<string> Generated { get; } = new();
    public AsyncRelayCommand GenerateCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenEvidenceFolderCommand { get; }

    private async Task GenerateAsync()
    {
        var formats = new List<string>();
        if (_html) formats.Add("html"); if (_json) formats.Add("json"); if (_pdf) formats.Add("pdf");
        foreach (var p in await _app.GenerateReportsAsync(formats, CancellationToken.None))
            if (!Generated.Contains(p))
                Generated.Add(p);
        _app.Notifications.Notify($"Generated {formats.Count} report(s)");
    }
}
