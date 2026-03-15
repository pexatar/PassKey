using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels;
using PassKey.Desktop.Views;
using Windows.UI;

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

        // Update title bar colors whenever the system theme changes (user has "System" selected)
        if (Content is FrameworkElement root)
            root.ActualThemeChanged += (s, _) => UpdateTitleBarColors(((FrameworkElement)s).ActualTheme == ElementTheme.Dark);

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
        {
            root.RequestedTheme = theme;
            UpdateTitleBarColors(root.ActualTheme == ElementTheme.Dark);
        }
    }

    private void UpdateTitleBarColors(bool isDark)
    {
        var tb = AppWindow.TitleBar;
        if (isDark)
        {
            var bg        = Color.FromArgb(255, 32, 32, 32);
            var bgHover   = Color.FromArgb(255, 55, 55, 55);
            var bgPressed = Color.FromArgb(255, 75, 75, 75);
            var fgDim     = Color.FromArgb(255, 150, 150, 150);

            tb.ForegroundColor               = Colors.White;
            tb.BackgroundColor               = bg;
            tb.ButtonForegroundColor         = Colors.White;
            tb.ButtonBackgroundColor         = bg;
            tb.ButtonHoverForegroundColor    = Colors.White;
            tb.ButtonHoverBackgroundColor    = bgHover;
            tb.ButtonPressedForegroundColor  = Colors.White;
            tb.ButtonPressedBackgroundColor  = bgPressed;
            tb.InactiveForegroundColor       = fgDim;
            tb.InactiveBackgroundColor       = bg;
            tb.ButtonInactiveForegroundColor = fgDim;
            tb.ButtonInactiveBackgroundColor = bg;
        }
        else
        {
            // null restores system default colors (light theme)
            tb.ForegroundColor = tb.BackgroundColor =
            tb.ButtonForegroundColor = tb.ButtonBackgroundColor =
            tb.ButtonHoverForegroundColor = tb.ButtonHoverBackgroundColor =
            tb.ButtonPressedForegroundColor = tb.ButtonPressedBackgroundColor =
            tb.InactiveForegroundColor = tb.InactiveBackgroundColor =
            tb.ButtonInactiveForegroundColor = tb.ButtonInactiveBackgroundColor = null;
        }
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
