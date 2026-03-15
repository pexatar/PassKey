using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Code-behind for <see cref="HelpView"/>.
/// Delegates all business logic to <see cref="HelpViewModel"/> via <see cref="SetViewModel"/>.
/// </summary>
/// <remarks>
/// Displays keyboard shortcuts, per-page usage guides (8 Expander sections),
/// FAQ entries, and application version/data path information.
/// </remarks>
public sealed partial class HelpView : Microsoft.UI.Xaml.Controls.UserControl
{
    /// <summary>Initialises XAML components.</summary>
    public HelpView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Binds the view to the specified ViewModel and populates static fields
    /// (app version, vault database path) that cannot be bound via x:Bind.
    /// </summary>
    /// <param name="vm">The <see cref="HelpViewModel"/> to bind.</param>
    public void SetViewModel(HelpViewModel vm)
    {
        DataContext = vm;
        VersionText.Text = vm.AppVersion;
        DataPathText.Text = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PassKey", "vault.db");
    }
}
