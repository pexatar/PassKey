using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Models;
using PassKey.Core.Services;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Password detail ViewModel for add/edit panel.
/// Fields: Title, URL, Username, Password, Notes.
/// </summary>
public partial class PasswordDetailViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IPasswordGenerator _generator;
    private readonly IDialogQueueService _dialogQueue;
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;

    private PasswordEntry? _editingEntry;
    private bool _isNew;

    [ObservableProperty]
    public partial string PanelTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Url { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? FaviconBase64 { get; set; }

    [ObservableProperty]
    public partial bool CanSave { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial int PasswordStrengthScore { get; set; }

    [ObservableProperty]
    public partial string PasswordStrengthLabel { get; set; } = string.Empty;

    /// <summary>Whether this is a new entry (for subtitle logic).</summary>
    public bool IsNew => _isNew;

    /// <summary>Callback when save completes: (isNew, entryId).</summary>
    public Action<bool, Guid>? Saved { get; set; }

    /// <summary>Callback when delete completes: (entryId).</summary>
    public Action<Guid>? Deleted { get; set; }

    /// <summary>Callback when cancel is clicked.</summary>
    public Action? Cancelled { get; set; }

    public PasswordDetailViewModel(
        IVaultStateService vaultState,
        IPasswordGenerator generator,
        IDialogQueueService dialogQueue,
        IPasswordStrengthAnalyzer strengthAnalyzer)
    {
        _vaultState = vaultState;
        _generator = generator;
        _dialogQueue = dialogQueue;
        _strengthAnalyzer = strengthAnalyzer;
    }

    public void StartNew()
    {
        _editingEntry = null;
        _isNew = true;
        PanelTitle = "Aggiungi password";
        Title = string.Empty;
        Url = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        Notes = string.Empty;
        FaviconBase64 = null;
        UpdateCanSave();
    }

    public void StartEdit(PasswordEntry entry)
    {
        _editingEntry = entry;
        _isNew = false;
        PanelTitle = "Modifica password";
        Title = entry.Title;
        Url = entry.Url;
        Username = entry.Username;
        Password = entry.Password;
        Notes = entry.Notes;
        FaviconBase64 = entry.FaviconBase64;
        UpdateCanSave();
    }

    partial void OnTitleChanged(string value) => UpdateCanSave();
    partial void OnUsernameChanged(string value) => UpdateCanSave();
    partial void OnPasswordChanged(string value)
    {
        UpdateCanSave();
        UpdatePasswordStrength();
    }

    private void UpdateCanSave()
    {
        CanSave = !string.IsNullOrWhiteSpace(Title) &&
                  !string.IsNullOrWhiteSpace(Username) &&
                  !string.IsNullOrWhiteSpace(Password);
    }

    private void UpdatePasswordStrength()
    {
        if (string.IsNullOrEmpty(Password))
        {
            PasswordStrengthScore = 0;
            PasswordStrengthLabel = string.Empty;
            return;
        }
        var result = _strengthAnalyzer.Analyze(Password.AsSpan());
        PasswordStrengthScore = result.Score;
        PasswordStrengthLabel = result.Label;
    }

    [RelayCommand]
    private void GeneratePassword()
    {
        Password = _generator.Generate(new PasswordGeneratorOptions
        {
            Length = 20,
            IncludeUppercase = true,
            IncludeLowercase = true,
            IncludeDigits = true,
            IncludeSymbols = true
        });
    }

    [RelayCommand]
    private Task SaveAsync()
    {
        if (!CanSave) return Task.CompletedTask;

        var vault = _vaultState.CurrentVault;
        if (vault is null) return Task.CompletedTask;

        IsSaving = true;

        try
        {
            bool wasNew = _isNew;
            Guid entryId;

            if (_isNew)
            {
                var entry = new PasswordEntry
                {
                    Title = Title.Trim(),
                    Url = Url.Trim(),
                    Username = Username.Trim(),
                    Password = Password,
                    Notes = Notes.Trim(),
                    FaviconBase64 = FaviconBase64
                };
                vault.Passwords.Add(entry);
                entryId = entry.Id;
            }
            else if (_editingEntry is not null)
            {
                _editingEntry.Title = Title.Trim();
                _editingEntry.Url = Url.Trim();
                _editingEntry.Username = Username.Trim();
                _editingEntry.Password = Password;
                _editingEntry.Notes = Notes.Trim();
                _editingEntry.FaviconBase64 = FaviconBase64;
                _editingEntry.ModifiedAt = DateTime.UtcNow;
                entryId = _editingEntry.Id;
            }
            else
            {
                return Task.CompletedTask;
            }

            Saved?.Invoke(wasNew, entryId);
        }
        finally
        {
            IsSaving = false;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_editingEntry is null || _isNew) return;

        var result = await _dialogQueue.EnqueueAndWait(() =>
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Elimina password",
                Content = $"Eliminare \"{_editingEntry.Title}\"?\nQuesta azione è irreversibile.",
                PrimaryButtonText = "Elimina",
                CloseButtonText = "Annulla",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close
            };
            return dialog.ShowAsync().AsTask();
        });

        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            var vault = _vaultState.CurrentVault;
            var entryId = _editingEntry.Id;
            vault?.Passwords.Remove(_editingEntry);
            Deleted?.Invoke(entryId);
        }
    }
}
