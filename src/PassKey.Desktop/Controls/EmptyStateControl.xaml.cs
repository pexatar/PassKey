using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PassKey.Desktop.Controls;

/// <summary>
/// Reusable empty-state placeholder control displayed when a list or view has no content.
/// Shows a Segoe MDL2 Assets glyph icon, a title, a subtitle, and up to two action buttons.
/// Uses <see cref="DependencyProperty"/> with <c>PropertyChangedCallback</c> instead of x:Bind
/// to avoid XamlCompiler Pass2 crashes in AOT builds (SC-04).
/// </summary>
public sealed partial class EmptyStateControl : UserControl
{
    // ─── Dependency Properties ────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(EmptyStateControl),
            new PropertyMetadata("\uE8A5", OnIconChanged));

    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyStateControl),
            new PropertyMetadata(string.Empty, OnTitleChanged));

    /// <summary>Identifies the <see cref="Subtitle"/> dependency property.</summary>
    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(EmptyStateControl),
            new PropertyMetadata(string.Empty, OnSubtitleChanged));

    /// <summary>Identifies the <see cref="PrimaryActionText"/> dependency property.</summary>
    public static readonly DependencyProperty PrimaryActionTextProperty =
        DependencyProperty.Register(nameof(PrimaryActionText), typeof(string), typeof(EmptyStateControl),
            new PropertyMetadata(null, OnPrimaryActionTextChanged));

    /// <summary>Identifies the <see cref="PrimaryActionCommand"/> dependency property.</summary>
    public static readonly DependencyProperty PrimaryActionCommandProperty =
        DependencyProperty.Register(nameof(PrimaryActionCommand), typeof(ICommand), typeof(EmptyStateControl),
            new PropertyMetadata(null, OnPrimaryActionCommandChanged));

    /// <summary>Identifies the <see cref="SecondaryActionText"/> dependency property.</summary>
    public static readonly DependencyProperty SecondaryActionTextProperty =
        DependencyProperty.Register(nameof(SecondaryActionText), typeof(string), typeof(EmptyStateControl),
            new PropertyMetadata(null, OnSecondaryActionTextChanged));

    /// <summary>Identifies the <see cref="SecondaryActionCommand"/> dependency property.</summary>
    public static readonly DependencyProperty SecondaryActionCommandProperty =
        DependencyProperty.Register(nameof(SecondaryActionCommand), typeof(ICommand), typeof(EmptyStateControl),
            new PropertyMetadata(null, OnSecondaryActionCommandChanged));

    // ─── CLR Properties ───────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the Segoe MDL2 Assets glyph character displayed as the empty-state icon.
    /// Default is U+E8A5 (document icon). Use a four-digit Unicode hex string prefixed with \u.
    /// </summary>
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Gets or sets the primary heading text displayed below the icon.</summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>Gets or sets the secondary descriptive text displayed below the title.</summary>
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <summary>
    /// Gets or sets the label for the primary action button.
    /// The button is hidden when this value is null or empty.
    /// </summary>
    public string? PrimaryActionText { get => (string?)GetValue(PrimaryActionTextProperty); set => SetValue(PrimaryActionTextProperty, value); }

    /// <summary>
    /// Gets or sets the command bound to the primary action button.
    /// </summary>
    public ICommand? PrimaryActionCommand { get => (ICommand?)GetValue(PrimaryActionCommandProperty); set => SetValue(PrimaryActionCommandProperty, value); }

    /// <summary>
    /// Gets or sets the label for the optional secondary action button.
    /// The button is hidden when this value is null or empty.
    /// </summary>
    public string? SecondaryActionText { get => (string?)GetValue(SecondaryActionTextProperty); set => SetValue(SecondaryActionTextProperty, value); }

    /// <summary>
    /// Gets or sets the command bound to the optional secondary action button.
    /// </summary>
    public ICommand? SecondaryActionCommand { get => (ICommand?)GetValue(SecondaryActionCommandProperty); set => SetValue(SecondaryActionCommandProperty, value); }

    /// <summary>
    /// Initializes a new instance of <see cref="EmptyStateControl"/>.
    /// </summary>
    public EmptyStateControl()
    {
        InitializeComponent();
    }

    // ─── Property Changed Callbacks ───────────────────────────────────────────

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl self)
            self.IconElement.Glyph = (string)e.NewValue;
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl self)
            self.TitleText.Text = (string)e.NewValue;
    }

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl self)
            self.SubtitleText.Text = (string)e.NewValue;
    }

    private static void OnPrimaryActionTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl self)
            self.PrimaryButton.Content = (string?)e.NewValue;
    }

    private static void OnPrimaryActionCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl self)
            self.PrimaryButton.Command = (ICommand?)e.NewValue;
    }

    private static void OnSecondaryActionTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl self)
            self.SecondaryButton.Content = (string?)e.NewValue;
    }

    private static void OnSecondaryActionCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl self)
            self.SecondaryButton.Command = (ICommand?)e.NewValue;
    }
}
