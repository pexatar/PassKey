using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Secure note detail ViewModel for the editor panel.
/// Fields: Title, Category (with color), Content (multiline), character/word counter,
/// pin toggle, unsaved changes indicator.
/// </summary>
public partial class SecureNoteDetailViewModel : ObservableObject
{
    private readonly IVaultStateService _vaultState;
    private readonly IDialogQueueService _dialogQueue;

    private SecureNoteEntry? _editingEntry;
    private bool _isNew;

    // Snapshot valori originali per tracking "non salvato"
    private string _originalTitle = string.Empty;
    private string _originalContent = string.Empty;
    private NoteCategory _originalCategory = NoteCategory.General;
    private bool _originalIsPinned;

    [ObservableProperty]
    public partial string PanelTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial NoteCategory Category { get; set; } = NoteCategory.General;

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int CharacterCount { get; set; }

    [ObservableProperty]
    public partial int WordCount { get; set; }

    [ObservableProperty]
    public partial bool CanSave { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    [ObservableProperty]
    public partial bool IsPinned { get; set; }

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    /// <summary>Callback when save completes: (isNew, entryId).</summary>
    public Action<bool, Guid>? Saved { get; set; }

    /// <summary>Callback when delete completes: (entryId).</summary>
    public Action<Guid>? Deleted { get; set; }

    /// <summary>Callback when cancel is clicked.</summary>
    public Action? Cancelled { get; set; }

    /// <summary>Callback when pin is toggled (instant save, no Save button needed).</summary>
    public Action? PinToggled { get; set; }

    public SecureNoteDetailViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue)
    {
        _vaultState = vaultState;
        _dialogQueue = dialogQueue;
    }

    public void StartNew()
    {
        _editingEntry = null;
        _isNew = true;
        IsEditMode = false;
        PanelTitle = "Nuova nota";
        Title = string.Empty;
        Category = NoteCategory.General;
        Content = string.Empty;
        CharacterCount = 0;
        WordCount = 0;
        IsPinned = false;
        HasUnsavedChanges = false;

        _originalTitle = string.Empty;
        _originalContent = string.Empty;
        _originalCategory = NoteCategory.General;
        _originalIsPinned = false;

        UpdateCanSave();
    }

    public void StartEdit(SecureNoteEntry entry)
    {
        _editingEntry = entry;
        _isNew = false;
        IsEditMode = true;
        PanelTitle = "Modifica nota";
        Title = entry.Title;
        Category = entry.Category;
        Content = entry.Content;
        CharacterCount = entry.Content.Length;
        WordCount = CountWords(entry.Content);
        IsPinned = entry.IsPinned;
        HasUnsavedChanges = false;

        _originalTitle = entry.Title;
        _originalContent = entry.Content;
        _originalCategory = entry.Category;
        _originalIsPinned = entry.IsPinned;

        UpdateCanSave();
    }

    partial void OnTitleChanged(string value)
    {
        UpdateCanSave();
        UpdateHasUnsavedChanges();
    }

    partial void OnContentChanged(string value)
    {
        CharacterCount = value.Length;
        WordCount = CountWords(value);
        UpdateCanSave();
        UpdateHasUnsavedChanges();
    }

    partial void OnCategoryChanged(NoteCategory value)
    {
        UpdateHasUnsavedChanges();
    }

    partial void OnIsPinnedChanged(bool value)
    {
        UpdateHasUnsavedChanges();
    }

    private void UpdateCanSave()
    {
        CanSave = !string.IsNullOrWhiteSpace(Title);
    }

    private void UpdateHasUnsavedChanges()
    {
        HasUnsavedChanges = Title != _originalTitle
            || Content != _originalContent
            || Category != _originalCategory
            || IsPinned != _originalIsPinned;
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;

        // Persiste immediatamente sul model (senza passare da Save)
        if (_editingEntry is not null)
        {
            _editingEntry.IsPinned = IsPinned;
        }

        // Aggiorna snapshot: il pin non conta come "modifica non salvata"
        _originalIsPinned = IsPinned;
        UpdateHasUnsavedChanges();

        // Callback: salva vault e aggiorna lista
        PinToggled?.Invoke();
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
                var entry = new SecureNoteEntry
                {
                    Title = Title.Trim(),
                    Category = Category,
                    Content = Content,
                    IsPinned = IsPinned
                };
                vault.SecureNotes.Add(entry);
                _editingEntry = entry;
                _isNew = false;
                IsEditMode = true;
                PanelTitle = "Modifica nota";
                entryId = entry.Id;
            }
            else if (_editingEntry is not null)
            {
                _editingEntry.Title = Title.Trim();
                _editingEntry.Category = Category;
                _editingEntry.Content = Content;
                _editingEntry.IsPinned = IsPinned;
                _editingEntry.ModifiedAt = DateTime.UtcNow;
                entryId = _editingEntry.Id;
            }
            else
            {
                return Task.CompletedTask;
            }

            // Aggiorna snapshot dopo save riuscito
            _originalTitle = Title;
            _originalContent = Content;
            _originalCategory = Category;
            _originalIsPinned = IsPinned;
            HasUnsavedChanges = false;

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

        var displayName = !string.IsNullOrWhiteSpace(_editingEntry.Title)
            ? _editingEntry.Title
            : "Nota senza titolo";

        var result = await _dialogQueue.EnqueueAndWait(() =>
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Elimina nota",
                Content = $"Eliminare \"{displayName}\"?\nQuesta azione è irreversibile.",
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
            vault?.SecureNotes.Remove(_editingEntry);
            Deleted?.Invoke(entryId);
        }
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
