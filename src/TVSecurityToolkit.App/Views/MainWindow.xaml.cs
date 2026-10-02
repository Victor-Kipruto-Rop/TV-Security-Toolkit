using System.Windows;
using TVSecurityToolkit.App.ViewModels;

namespace TVSecurityToolkit.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(App.Services);
    }
}
