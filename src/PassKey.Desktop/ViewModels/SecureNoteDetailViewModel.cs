using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels.Base;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Secure note detail ViewModel for the editor panel.
/// Fields: Title, Category (with color), Content (multiline), character/word counter,
/// pin toggle, unsaved changes indicator.
/// Shared add/edit/save/delete plumbing is provided by <see cref="BaseDetailViewModel{TEntry}"/>.
/// </summary>
/// <remarks>
/// Unlike the other detail ViewModels, after a successful "create new" save this VM
/// keeps the panel open and transitions in-place to "edit" mode, so the user can keep
/// writing without re-opening the entry.
/// </remarks>
public partial class SecureNoteDetailViewModel : BaseDetailViewModel<SecureNoteEntry>
{
    // Snapshot of original values for "unsaved changes" tracking.
    private string _originalTitle = string.Empty;
    private string _originalContent = string.Empty;
    private NoteCategory _originalCategory = NoteCategory.General;
    private bool _originalIsPinned;

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
    public partial bool IsEditMode { get; set; }

    [ObservableProperty]
    public partial bool IsPinned { get; set; }

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    // ── Inline validation (T5.6) ───────────────────────────────────────────────

    [ObservableProperty]
    public partial bool IsTitleEmpty { get; set; }

    /// <summary>Raised when <see cref="IsPinned"/> is toggled (instant-save, no Save button needed).</summary>
    public Action? PinToggled { get; set; }

    public SecureNoteDetailViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue)
        : base(vaultState, dialogQueue)
    {
    }

    // ─── Template-method overrides ────────────────────────────────────────────

    protected override string GetPanelTitleForNew() => "Nuova nota";
    protected override string GetPanelTitleForEdit() => "Modifica nota";
    protected override string GetDeleteDialogTitle() => "Elimina nota";
    protected override string GetDeleteDisplayName(SecureNoteEntry entry)
        => !string.IsNullOrWhiteSpace(entry.Title) ? entry.Title : "Nota senza titolo";

    protected override IList<SecureNoteEntry> GetVaultCollection(Vault vault) => vault.SecureNotes;

    protected override void ResetFieldsForNew()
    {
        Title = string.Empty;
        Category = NoteCategory.General;
        Content = string.Empty;
        CharacterCount = 0;
        WordCount = 0;
        IsPinned = false;
        HasUnsavedChanges = false;
        IsEditMode = false;

        _originalTitle = string.Empty;
        _originalContent = string.Empty;
        _originalCategory = NoteCategory.General;
        _originalIsPinned = false;
        IsTitleEmpty = true;
    }

    protected override void LoadFromEntry(SecureNoteEntry entry)
    {
        Title = entry.Title;
        Category = entry.Category;
        Content = entry.Content;
        CharacterCount = entry.Content.Length;
        WordCount = CountWords(entry.Content);
        IsPinned = entry.IsPinned;
        HasUnsavedChanges = false;
        IsEditMode = true;

        _originalTitle = entry.Title;
        _originalContent = entry.Content;
        _originalCategory = entry.Category;
        _originalIsPinned = entry.IsPinned;
    }

    protected override SecureNoteEntry CreateNewEntry() => new()
    {
        Title = Title.Trim(),
        Category = Category,
        Content = Content,
        IsPinned = IsPinned
    };

    protected override void ApplyToEntry(SecureNoteEntry entry)
    {
        entry.Title = Title.Trim();
        entry.Category = Category;
        entry.Content = Content;
        entry.IsPinned = IsPinned;
    }

    protected override void UpdateCanSave()
    {
        CanSave = !string.IsNullOrWhiteSpace(Title);
    }

    /// <summary>After saving a brand-new note, transition the panel to edit-mode in-place
    /// so the user can keep editing without re-opening the entry.</summary>
    protected override void OnSavedNew(SecureNoteEntry entry)
    {
        EditingEntry = entry;
        SetIsNew(false);
        IsEditMode = true;
        PanelTitle = GetPanelTitleForEdit();
        UpdateSnapshotFromCurrent();
    }

    /// <summary>After applying edits, refresh the "original" snapshot so the dirty-flag clears.</summary>
    protected override void OnSavedEdit(SecureNoteEntry entry)
    {
        UpdateSnapshotFromCurrent();
    }

    // ─── Property change handlers ─────────────────────────────────────────────

    partial void OnTitleChanged(string value)
    {
        IsTitleEmpty = string.IsNullOrWhiteSpace(value);
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

    partial void OnCategoryChanged(NoteCategory value) => UpdateHasUnsavedChanges();
    partial void OnIsPinnedChanged(bool value) => UpdateHasUnsavedChanges();

    private void UpdateHasUnsavedChanges()
    {
        HasUnsavedChanges = Title != _originalTitle
            || Content != _originalContent
            || Category != _originalCategory
            || IsPinned != _originalIsPinned;
    }

    private void UpdateSnapshotFromCurrent()
    {
        _originalTitle = Title;
        _originalContent = Content;
        _originalCategory = Category;
        _originalIsPinned = IsPinned;
        HasUnsavedChanges = false;
    }

    // ─── Type-specific command (instant pin toggle, bypasses Save flow) ───────

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;

        // Persist immediately on the model (does not go through Save).
        if (EditingEntry is not null)
        {
            EditingEntry.IsPinned = IsPinned;
        }

        // The pin toggle does not count as an "unsaved change".
        _originalIsPinned = IsPinned;
        UpdateHasUnsavedChanges();

        // Caller saves the vault to disk and refreshes the list.
        PinToggled?.Invoke();
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
