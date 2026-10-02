using System.Collections.ObjectModel;
using TVSecurityToolkit.App.Commands;
using TVSecurityToolkit.App.Services;
using TVSecurityToolkit.Core.Enums;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class SelectableTest : ViewModelBase
{
    private bool _selected = true;
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public Severity Severity { get; init; }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
}

public sealed class TestSelectionViewModel : ViewModelBase
{
    public TestSelectionViewModel(ApplicationService app)
    {
        foreach (var t in app.Registry.Tests.Select(t => app.Registry.TryGet(t.Id, out var d) ? d : null)
                     .Where(d => d is not null).OrderBy(d => d!.Category).ThenBy(d => d!.Id))
            Tests.Add(new SelectableTest { Id = t!.Id, Category = t.Category, Title = t.Title, Severity = t.Severity });
        SelectAllCommand = new RelayCommand(_ => SetAll(true));
        SelectNoneCommand = new RelayCommand(_ => SetAll(false));
        SelectCategoryCommand = new RelayCommand(p => { foreach (var t in Tests) t.IsSelected = t.Category == (string?)p; });
    }

    public ObservableCollection<SelectableTest> Tests { get; } = new();
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand SelectCategoryCommand { get; }
    public IReadOnlyList<string> Categories => Tests.Select(t => t.Category).Distinct().ToList();
    public IEnumerable<string> SelectedIds => Tests.Where(t => t.IsSelected).Select(t => t.Id);
    private void SetAll(bool v) { foreach (var t in Tests) t.IsSelected = v; }
}
