using TVSecurityToolkit.Core.Constants;

namespace TVSecurityToolkit.App.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    public string Name => ApplicationConstants.Name;
    public string Version => ApplicationConstants.Version;
    public string Text => "Defensive verification toolkit for pay-as-you-go TV devices. Use only on devices you own or are authorised to test.";
}
