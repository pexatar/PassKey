using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels.Base;
using PassKey.Desktop.ViewModels.Items;

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
    [NotifyPropertyChangedFor(nameof(CounterText))]
    public partial int CharacterCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CounterText))]
    public partial int WordCount { get; set; }

    /// <summary>The "N car · M parole" caption under the editor.</summary>
    public string CounterText => string.Format(_res.GetString("NoteCharWordCount"), CharacterCount, WordCount);

    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinButtonText))]
    [NotifyPropertyChangedFor(nameof(PinAccessibleName))]
    public partial bool IsPinned { get; set; }

    /// <summary>Caption of the pin toggle, describing the current state.</summary>
    public string PinButtonText => IsPinned
        ? _res.GetString("NotesPinnedButton")
        : _res.GetString("NotesPinButtonLabel");

    /// <summary>Accessible name of the pin toggle, describing the action it would perform.</summary>
    public string PinAccessibleName => IsPinned
        ? _res.GetString("NoteUnpinName")
        : _res.GetString("NotePinName");

    /// <summary>True while the body is shown rendered as Markdown instead of editable text.</summary>
    /// <remarks>
    /// Editor-versus-preview is state of the editing session, so it lives with the session
    /// rather than in a field of a view that is replaced on every navigation.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorVisible))]
    [NotifyPropertyChangedFor(nameof(PreviewMarkdown))]
    public partial bool IsPreviewMode { get; set; }

    /// <summary>Inverse of <see cref="IsPreviewMode"/>; drives the text box's visibility.</summary>
    public bool IsEditorVisible => !IsPreviewMode;

    /// <summary>
    /// The text handed to the Markdown renderer: the body while previewing, nothing otherwise.
    /// </summary>
    /// <remarks>
    /// Empty outside preview so the renderer does not re-parse the whole note on every
    /// keystroke into a control the user cannot see. The body cannot change while previewing,
    /// because the text box is hidden.
    /// </remarks>
    public string PreviewMarkdown => IsPreviewMode ? Content : string.Empty;

    /// <summary>The picker's selected option, matched by reference against the shared list.</summary>
    public NoteCategoryOption? SelectedCategoryOption
    {
        get => NoteCategoryOption.For(Category);
        set
        {
            if (value is not null) Category = value.Category;
        }
    }

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; set; }

    // ── Inline validation (T5.6) ───────────────────────────────────────────────

    [ObservableProperty]
    public partial bool IsTitleEmpty { get; set; }

    /// <summary>Raised when <see cref="IsPinned"/> is toggled (instant-save, no Save button needed).</summary>
    public Action? PinToggled { get; set; }

    public SecureNoteDetailViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue,
        ILogService log)
        : base(vaultState, dialogQueue, log)
    {
    }

    // ─── Template-method overrides ────────────────────────────────────────────

    protected override string GetDeleteDisplayName(SecureNoteEntry entry)
        => !string.IsNullOrWhiteSpace(entry.Title) ? entry.Title : _res.GetString("NoteNoTitle");

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
        IsPreviewMode = false;

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
        IsPreviewMode = false;

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

    partial void OnCategoryChanged(NoteCategory value)
    {
        OnPropertyChanged(nameof(SelectedCategoryOption));
        UpdateHasUnsavedChanges();
    }
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

    /// <summary>Switches the body area back to the editable text box.</summary>
    [RelayCommand]
    private void ShowEditor() => IsPreviewMode = false;

    /// <summary>Switches the body area to the rendered Markdown preview.</summary>
    [RelayCommand]
    private void ShowPreview() => IsPreviewMode = true;

    /// <summary>Also releases the notes-only pin callback when the session ends.</summary>
    public override void Dispose()
    {
        PinToggled = null;
        base.Dispose();
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
