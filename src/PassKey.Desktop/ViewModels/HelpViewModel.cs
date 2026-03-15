using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Help/Guide page.
/// Exposes static information (app version) for display.
/// </summary>
public partial class HelpViewModel : ObservableObject
{
    public string AppVersion { get; } = GetAppVersion();

    private static string GetAppVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        return ver is null ? "1.0.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }
}
