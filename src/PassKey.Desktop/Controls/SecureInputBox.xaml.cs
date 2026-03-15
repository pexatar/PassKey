using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace PassKey.Desktop.Controls;

/// <summary>
/// Custom password input control that replaces <see cref="PasswordBox"/>, which crashes
/// under .NET AOT compilation (SC-01). Characters are stored as a plaintext
/// <see cref="string"/> internally but displayed as bullet characters (U+2022) in the
/// underlying <see cref="TextBox"/>. The control reconstructs the plaintext on every
/// keystroke by comparing the new masked text against the previous plaintext using cursor
/// position arithmetic.
///
/// <para>
/// <b>Reveal mechanism (B5):</b> The reveal button uses <c>PointerPressed</c> /
/// <c>PointerReleased</c> / <c>PointerExited</c> events to implement press-and-hold
/// password reveal. While pressed, the <see cref="TextBox"/> shows the plaintext; on
/// release or pointer exit it re-masks immediately.
/// </para>
/// </summary>
public sealed partial class SecureInputBox : UserControl
{
    private string _plainText = string.Empty;
    private bool _isRevealed;
    private bool _updatingText;
    private const char MaskChar = '\u2022'; // bullet

    // ─── Dependency Properties ────────────────────────────────────────────────

    /// <summary>
    /// Identifies the <see cref="PlaceholderText"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(SecureInputBox),
            new PropertyMetadata(string.Empty, OnPlaceholderTextChanged));

    /// <summary>
    /// Identifies the <see cref="MaxLength"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MaxLengthProperty =
        DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(SecureInputBox),
            new PropertyMetadata(128, OnMaxLengthChanged));

    /// <summary>
    /// Identifies the <see cref="ShowRevealButton"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowRevealButtonProperty =
        DependencyProperty.Register(nameof(ShowRevealButton), typeof(Visibility), typeof(SecureInputBox),
            new PropertyMetadata(Visibility.Visible, OnShowRevealButtonChanged));

    private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SecureInputBox self)
            self.InputBox.PlaceholderText = (string)e.NewValue;
    }

    private static void OnMaxLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SecureInputBox self)
            self.InputBox.MaxLength = (int)e.NewValue;
    }

    private static void OnShowRevealButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SecureInputBox self)
            self.RevealButton.Visibility = (Visibility)e.NewValue;
    }

    // ─── CLR Properties ───────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the placeholder text displayed in the input box when it is empty.
    /// </summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum number of characters allowed. Default is 128.
    /// </summary>
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="Visibility"/> of the press-and-hold reveal button.
    /// Set to <see cref="Visibility.Collapsed"/> to hide it (e.g., in confirm-password fields).
    /// Default is <see cref="Visibility.Visible"/>.
    /// </summary>
    public Visibility ShowRevealButton
    {
        get => (Visibility)GetValue(ShowRevealButtonProperty);
        set => SetValue(ShowRevealButtonProperty, value);
    }

    /// <summary>
    /// Gets the current plaintext password. Returns an empty string when the field is empty.
    /// </summary>
    public string Password => _plainText;

    /// <summary>
    /// Raised whenever the password content changes (insert, delete, or replace).
    /// The event argument is the new plaintext value.
    /// </summary>
    public event EventHandler<string>? PasswordChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="SecureInputBox"/>.
    /// </summary>
    public SecureInputBox()
    {
        InitializeComponent();
    }

    // ─── Input Handling ───────────────────────────────────────────────────────

    /// <summary>
    /// Reconstructs the plaintext after each text change by comparing the masked display
    /// with the previous plaintext using cursor-position arithmetic. Handles four cases:
    /// full clear, character insertion, character deletion, and same-length replacement.
    /// In revealed mode, the display text is accepted as-is.
    /// </summary>
    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingText) return;

        var currentText = InputBox.Text;
        var selectionStart = InputBox.SelectionStart;

        if (_isRevealed)
        {
            _plainText = currentText;
            PasswordChanged?.Invoke(this, _plainText);
            return;
        }

        // Reconstruct plaintext by comparing current (mixed bullets + new chars)
        // with the previous plaintext. The cursor position tells us where edits happened.
        var oldLen = _plainText.Length;
        var newLen = currentText.Length;

        if (newLen == 0)
        {
            // Full clear (Ctrl+A → Delete, or select-all → type)
            _plainText = string.Empty;
        }
        else if (newLen > oldLen)
        {
            // Characters were inserted — find the new (non-bullet) chars
            var insertedCount = newLen - oldLen;
            var insertPos = selectionStart - insertedCount;
            if (insertPos < 0) insertPos = 0;
            if (insertPos > oldLen) insertPos = oldLen;

            var inserted = currentText.Substring(insertPos, insertedCount);
            _plainText = _plainText.Insert(insertPos, inserted);
        }
        else if (newLen < oldLen)
        {
            // Characters were deleted
            var deletedCount = oldLen - newLen;
            var deleteStart = selectionStart;
            if (deleteStart < 0) deleteStart = 0;
            if (deleteStart + deletedCount > _plainText.Length)
                deleteStart = Math.Max(0, _plainText.Length - deletedCount);

            _plainText = _plainText.Remove(deleteStart, deletedCount);
        }
        else
        {
            // Same length — character replacement (select + type)
            // Find non-bullet characters and put them into their positions
            var chars = _plainText.ToCharArray();
            for (var i = 0; i < currentText.Length; i++)
            {
                if (currentText[i] != MaskChar && i < chars.Length)
                {
                    chars[i] = currentText[i];
                }
            }
            _plainText = new string(chars);
        }

        // Update display with masked text
        _updatingText = true;
        InputBox.Text = new string(MaskChar, _plainText.Length);
        InputBox.SelectionStart = Math.Min(selectionStart, _plainText.Length);
        _updatingText = false;

        PasswordChanged?.Invoke(this, _plainText);
    }

    /// <summary>
    /// Allows the Enter key to propagate to the parent element (e.g., to trigger a login button).
    /// </summary>
    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Let Enter key propagate to parent for login
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            // Don't mark as handled — let the parent handle it
        }
    }

    // ─── Reveal Mechanism ─────────────────────────────────────────────────────

    /// <summary>
    /// Switches the display to plaintext when the reveal button is pressed (press-and-hold).
    /// </summary>
    private void RevealButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isRevealed = true;
        _updatingText = true;
        var pos = InputBox.SelectionStart;
        InputBox.Text = _plainText;
        InputBox.SelectionStart = Math.Min(pos, _plainText.Length);
        _updatingText = false;
    }

    /// <summary>
    /// Re-masks the password when the reveal button is released.
    /// </summary>
    private void RevealButton_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        HidePassword();
    }

    /// <summary>
    /// Re-masks the password if the pointer leaves the reveal button while still pressed
    /// (e.g., user drags off the button).
    /// </summary>
    private void RevealButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_isRevealed)
            HidePassword();
    }

    /// <summary>
    /// Replaces the displayed text with bullet characters and clears the revealed flag.
    /// </summary>
    private void HidePassword()
    {
        _isRevealed = false;
        _updatingText = true;
        InputBox.Text = new string(MaskChar, _plainText.Length);
        InputBox.SelectionStart = _plainText.Length;
        _updatingText = false;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Clears the password field and resets the plaintext buffer without raising
    /// <see cref="PasswordChanged"/>.
    /// </summary>
    public void Clear()
    {
        _plainText = string.Empty;
        _updatingText = true;
        InputBox.Text = string.Empty;
        _updatingText = false;
    }

    /// <summary>
    /// Sets the internal password value and updates the masked display without raising
    /// <see cref="PasswordChanged"/>. Used to pre-populate the field in edit mode.
    /// </summary>
    /// <param name="value">The plaintext password to load. Null is treated as empty.</param>
    public void SetPassword(string value)
    {
        _plainText = value ?? string.Empty;
        _updatingText = true;
        InputBox.Text = new string(MaskChar, _plainText.Length);
        _updatingText = false;
    }
}
