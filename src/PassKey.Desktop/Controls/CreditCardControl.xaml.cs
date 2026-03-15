using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Constants;
using PassKey.Core.Services;

namespace PassKey.Desktop.Controls;

/// <summary>
/// Skeuomorphic credit card visual control that renders card data as a realistic card face
/// with a gradient background, masked PAN, cardholder name, and expiry date.
/// Uses <see cref="DependencyProperty"/> with <c>PropertyChangedCallback</c> instead of
/// x:Bind to avoid XamlCompiler Pass2 crashes in AOT builds (SC-04).
/// </summary>
public sealed partial class CreditCardControl : UserControl
{
    #region DependencyProperties

    /// <summary>Identifies the <see cref="CardNumber"/> dependency property.</summary>
    public static readonly DependencyProperty CardNumberProperty =
        DependencyProperty.Register(nameof(CardNumber), typeof(string), typeof(CreditCardControl),
            new PropertyMetadata(string.Empty, OnCardDisplayChanged));

    /// <summary>Identifies the <see cref="CardholderName"/> dependency property.</summary>
    public static readonly DependencyProperty CardholderNameProperty =
        DependencyProperty.Register(nameof(CardholderName), typeof(string), typeof(CreditCardControl),
            new PropertyMetadata(string.Empty, OnCardDisplayChanged));

    /// <summary>Identifies the <see cref="ExpiryMonth"/> dependency property.</summary>
    public static readonly DependencyProperty ExpiryMonthProperty =
        DependencyProperty.Register(nameof(ExpiryMonth), typeof(int), typeof(CreditCardControl),
            new PropertyMetadata(0, OnCardDisplayChanged));

    /// <summary>Identifies the <see cref="ExpiryYear"/> dependency property.</summary>
    public static readonly DependencyProperty ExpiryYearProperty =
        DependencyProperty.Register(nameof(ExpiryYear), typeof(int), typeof(CreditCardControl),
            new PropertyMetadata(0, OnCardDisplayChanged));

    /// <summary>Identifies the <see cref="CardType"/> dependency property.</summary>
    public static readonly DependencyProperty CardTypeProperty =
        DependencyProperty.Register(nameof(CardType), typeof(CardType), typeof(CreditCardControl),
            new PropertyMetadata(CardType.Unknown, OnCardDisplayChanged));

    /// <summary>Identifies the <see cref="AccentColor"/> dependency property.</summary>
    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(CardColor), typeof(CreditCardControl),
            new PropertyMetadata(CardColor.Default, OnAccentColorChanged));

    /// <summary>Identifies the <see cref="Label"/> dependency property.</summary>
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(CreditCardControl),
            new PropertyMetadata(string.Empty, OnCardDisplayChanged));

    /// <summary>Identifies the <see cref="Category"/> dependency property.</summary>
    public static readonly DependencyProperty CategoryProperty =
        DependencyProperty.Register(nameof(Category), typeof(CardCategory), typeof(CreditCardControl),
            new PropertyMetadata(CardCategory.Personal, OnCardDisplayChanged));

    #endregion

    #region CLR Properties

    /// <summary>
    /// Gets or sets the card number. The display shows a masked PAN produced by
    /// <see cref="CardTypeDetector.MaskCardNumber"/>. An empty value shows bullet placeholders.
    /// </summary>
    public string CardNumber
    {
        get => (string)GetValue(CardNumberProperty);
        set => SetValue(CardNumberProperty, value);
    }

    /// <summary>
    /// Gets or sets the cardholder name. Displayed in uppercase. An empty value shows a
    /// localized placeholder string.
    /// </summary>
    public string CardholderName
    {
        get => (string)GetValue(CardholderNameProperty);
        set => SetValue(CardholderNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the expiry month (1–12). Combined with <see cref="ExpiryYear"/> to display
    /// "MM/YY". Zero means no expiry has been set.
    /// </summary>
    public int ExpiryMonth
    {
        get => (int)GetValue(ExpiryMonthProperty);
        set => SetValue(ExpiryMonthProperty, value);
    }

    /// <summary>
    /// Gets or sets the expiry year (4-digit). Displayed as the last two digits.
    /// Zero means no expiry has been set.
    /// </summary>
    public int ExpiryYear
    {
        get => (int)GetValue(ExpiryYearProperty);
        set => SetValue(ExpiryYearProperty, value);
    }

    /// <summary>
    /// Gets or sets the card network type (Visa, MasterCard, Amex, etc.).
    /// Controls the network label displayed on the card face.
    /// </summary>
    public CardType CardType
    {
        get => (CardType)GetValue(CardTypeProperty);
        set => SetValue(CardTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets the accent color used for the card gradient background.
    /// Maps to a predefined start/end color pair via <see cref="GetGradientColors"/>.
    /// </summary>
    public CardColor AccentColor
    {
        get => (CardColor)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional custom label displayed on the card (e.g., "Personal", "Work").
    /// </summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Gets or sets the card category (Personal, Work, Travel, Online) displayed on the card face.
    /// </summary>
    public CardCategory Category
    {
        get => (CardCategory)GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    #endregion

    private static readonly ResourceLoader s_res = new();

    /// <summary>
    /// Initializes a new instance of <see cref="CreditCardControl"/> and sets localized static labels.
    /// </summary>
    public CreditCardControl()
    {
        InitializeComponent();
        // Set localized static labels
        CardholderLabel.Text = s_res.GetString("CardLabelCardholder");
        ExpiryLabel.Text = s_res.GetString("CardLabelExpiry");
    }

    #region Callbacks

    /// <summary>
    /// Triggers a full display refresh when any card data property changes.
    /// </summary>
    private static void OnCardDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CreditCardControl self)
            self.UpdateDisplay();
    }

    /// <summary>
    /// Updates the gradient and then refreshes the display when the accent color changes.
    /// </summary>
    private static void OnAccentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CreditCardControl self)
        {
            self.UpdateGradient();
            self.UpdateDisplay();
        }
    }

    #endregion

    /// <summary>
    /// Refreshes all text elements on the card face from the current dependency property values.
    /// </summary>
    private void UpdateDisplay()
    {
        // Card type
        CardTypeText.Text = GetCardTypeName(CardType);

        // Masked PAN
        MaskedPanText.Text = string.IsNullOrWhiteSpace(CardNumber)
            ? "•••• •••• •••• ••••"
            : CardTypeDetector.MaskCardNumber(CardNumber, CardType);

        // Cardholder
        CardholderText.Text = string.IsNullOrWhiteSpace(CardholderName)
            ? s_res.GetString("CardPlaceholderName")
            : CardholderName.ToUpperInvariant();

        // Expiry
        ExpiryText.Text = ExpiryMonth > 0 && ExpiryYear > 0
            ? $"{ExpiryMonth:D2}/{ExpiryYear % 100:D2}"
            : "MM/YY";

        // Label
        LabelText.Text = Label;

        // Category
        CategoryText.Text = GetCategoryName(Category);
    }

    /// <summary>
    /// Updates the card gradient brush with the start and end colors derived from
    /// the current <see cref="AccentColor"/>.
    /// </summary>
    private void UpdateGradient()
    {
        var (start, end) = GetGradientColors(AccentColor);
        GradientStart.Color = start;
        GradientEnd.Color = end;
    }

    /// <summary>
    /// Maps a <see cref="CardColor"/> enum value to a gradient start and end color pair.
    /// </summary>
    /// <param name="color">The accent color to map.</param>
    /// <returns>A tuple of (startColor, endColor) for the card's linear gradient brush.</returns>
    private static (Windows.UI.Color start, Windows.UI.Color end) GetGradientColors(CardColor color)
    {
        return color switch
        {
            CardColor.Blue => (ColorHelper.FromArgb(255, 21, 101, 192), ColorHelper.FromArgb(255, 13, 71, 161)),
            CardColor.Red => (ColorHelper.FromArgb(255, 198, 40, 40), ColorHelper.FromArgb(255, 153, 27, 27)),
            CardColor.Green => (ColorHelper.FromArgb(255, 46, 125, 50), ColorHelper.FromArgb(255, 27, 94, 32)),
            CardColor.Purple => (ColorHelper.FromArgb(255, 106, 27, 154), ColorHelper.FromArgb(255, 74, 20, 140)),
            CardColor.Orange => (ColorHelper.FromArgb(255, 230, 81, 0), ColorHelper.FromArgb(255, 191, 54, 12)),
            CardColor.Teal => (ColorHelper.FromArgb(255, 0, 131, 143), ColorHelper.FromArgb(255, 0, 96, 100)),
            CardColor.Pink => (ColorHelper.FromArgb(255, 173, 20, 87), ColorHelper.FromArgb(255, 136, 14, 79)),
            CardColor.Gold => (ColorHelper.FromArgb(255, 249, 168, 37), ColorHelper.FromArgb(255, 245, 127, 23)),
            CardColor.Black => (ColorHelper.FromArgb(255, 33, 33, 33), ColorHelper.FromArgb(255, 18, 18, 18)),
            _ => (ColorHelper.FromArgb(255, 55, 71, 79), ColorHelper.FromArgb(255, 38, 50, 56)) // Default
        };
    }

    /// <summary>
    /// Maps a <see cref="CardType"/> enum value to its display name string (e.g., "VISA", "MASTERCARD").
    /// </summary>
    /// <param name="cardType">The card network type.</param>
    /// <returns>A display name string for the card network.</returns>
    private static string GetCardTypeName(CardType cardType)
    {
        return cardType switch
        {
            CardType.Visa => "VISA",
            CardType.MasterCard => "MASTERCARD",
            CardType.Amex => "AMEX",
            CardType.Discover => "DISCOVER",
            CardType.JCB => "JCB",
            CardType.Maestro => "MAESTRO",
            CardType.DinersClub => "DINERS CLUB",
            _ => s_res.GetString("CardTypeDefault")
        };
    }

    /// <summary>
    /// Maps a <see cref="CardCategory"/> enum value to a localized display name.
    /// </summary>
    /// <param name="category">The card category.</param>
    /// <returns>A localized display name string for the category.</returns>
    private static string GetCategoryName(CardCategory category)
    {
        return category switch
        {
            CardCategory.Personal => s_res.GetString("CatPersonalLabel"),
            CardCategory.Work => s_res.GetString("CatWorkLabel"),
            CardCategory.Travel => s_res.GetString("CatTravelLabel"),
            CardCategory.Online => "Online",
            _ => s_res.GetString("CatPersonalLabel")
        };
    }
}
