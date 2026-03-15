using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Welcome view shown after first-run vault creation.
/// Offers quick actions: add password, import, settings, or continue.
/// </summary>
public sealed partial class WelcomeView : UserControl
{
    private WelcomeViewModel? _viewModel;

    public WelcomeView()
    {
        InitializeComponent();
    }

    public void SetViewModel(WelcomeViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;
    }

    private void AddPasswordButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.AddPasswordCommand.Execute(null);

    private void ImportButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.ImportDataCommand.Execute(null);

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.OpenSettingsCommand.Execute(null);

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
        => _viewModel?.ContinueCommand.Execute(null);
}
