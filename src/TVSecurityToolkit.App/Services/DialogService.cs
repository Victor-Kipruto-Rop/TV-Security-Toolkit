using System.Windows;

namespace TVSecurityToolkit.App.Services;

public sealed class DialogService
{
    public void ShowError(string message) => MessageBox.Show(message, "TV Security Toolkit", MessageBoxButton.OK, MessageBoxImage.Error);
    public void ShowInfo(string message) => MessageBox.Show(message, "TV Security Toolkit", MessageBoxButton.OK, MessageBoxImage.Information);
    public bool Confirm(string message) =>
        MessageBox.Show(message, "TV Security Toolkit", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
