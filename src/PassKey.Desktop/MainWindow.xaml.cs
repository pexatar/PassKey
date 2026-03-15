using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels;
using PassKey.Desktop.Views;

namespace PassKey.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        Title = "PassKey";
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "PassKey.ico"));

        // WinUI 3 uses HighVisibility focus visuals by default — no manual setup needed

        _mainViewModel = App.Services.GetRequiredService<MainViewModel>();
        _mainViewModel.PropertyChanged += OnViewModelPropertyChanged;

        Activated += OnActivated;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_initialized) return;
        _initialized = true;

        // Apply saved theme on startup
        ApplySavedTheme();

        try
        {
            await _mainViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            RootPresenter.Content = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = $"Startup error:\n{ex}",
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
            };
        }
    }

    private void ApplySavedTheme()
    {
        var settings = App.Services.GetRequiredService<ISettingsService>();
        ApplyTheme(settings.Theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        });
    }

    public void ApplyTheme(ElementTheme theme)
    {
        if (Content is FrameworkElement root)
            root.RequestedTheme = theme;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            OnCurrentPageChanged(_mainViewModel.CurrentPage);
        }
    }

    private void OnCurrentPageChanged(ObservableObject? viewModel)
    {
        UIElement? view = viewModel switch
        {
            LoginViewModel loginVm => CreateLoginView(loginVm),
            SetupViewModel setupVm => CreateSetupView(setupVm),
            WelcomeViewModel welcomeVm => CreateWelcomeView(welcomeVm),
            ShellViewModel shellVm => CreateShellView(shellVm),
            _ => null
        };

        RootPresenter.Content = view;
    }

    private static LoginView CreateLoginView(LoginViewModel vm)
    {
        var view = new LoginView();
        view.SetViewModel(vm);
        return view;
    }

    private static SetupView CreateSetupView(SetupViewModel vm)
    {
        var view = new SetupView();
        view.SetViewModel(vm);
        return view;
    }

    private static WelcomeView CreateWelcomeView(WelcomeViewModel vm)
    {
        var view = new WelcomeView();
        view.SetViewModel(vm);
        return view;
    }

    private static ShellView CreateShellView(ShellViewModel vm)
    {
        var view = new ShellView();
        view.SetViewModel(vm);
        return view;
    }
}
