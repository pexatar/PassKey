using CommunityToolkit.Mvvm.ComponentModel;
using PassKey.Core.Constants;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels.Base;
using PassKey.Desktop.ViewModels.Items;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// Secure notes section: master-detail list with a category filter, search, pinned-first
/// ordering and an editor panel.
/// </summary>
/// <remarks>
/// Everything shared with the other three sections — the observable rows, the reload and
/// rebuild pipeline, the editing-session life cycle, the guarded persistence — lives in
/// <see cref="BaseListViewModel{TEntry, TItem}"/>. What remains here is what is true of
/// notes and of nothing else: the category filter, the search fields, the pinned-first
/// order, the two section headings and the instant pin toggle.
/// </remarks>
public partial class SecureNotesListViewModel : BaseListViewModel<SecureNoteEntry, SecureNoteItemViewModel>
{
    /// <summary>The category the list is filtered by, or <see langword="null"/> for all categories.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCategoryFilter))]
    public partial NoteCategory? FilterCategory { get; set; }

    /// <summary>Whether a category filter is active. Drives the badge on the filter button.</summary>
    public bool HasCategoryFilter => FilterCategory.HasValue;

    public SecureNotesListViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue,
        IVaultRepository repository,
        IToastService toast,
        ILogService log,
        Func<SecureNoteDetailViewModel> detailFactory)
        : base(vaultState, dialogQueue, repository, toast, log, detailFactory)
    {
    }

    /// <inheritdoc/>
    protected override string EntityTypeName => nameof(SecureNoteEntry);

    /// <inheritdoc/>
    protected override IList<SecureNoteEntry> GetVaultCollection(Vault vault) => vault.SecureNotes;

    /// <inheritdoc/>
    protected override SecureNoteItemViewModel CreateItem(SecureNoteEntry entry) => new(entry);

    /// <inheritdoc/>
    protected override string GetDisplayName(SecureNoteItemViewModel item)
        => !string.IsNullOrWhiteSpace(item.Title) ? item.Title : Res.GetString("NoteNoTitle");

    /// <summary>Applies the category filter and the search, then orders pinned notes first.</summary>
    protected override IEnumerable<SecureNoteEntry> FilterAndSort(IEnumerable<SecureNoteEntry> entries)
    {
        if (FilterCategory.HasValue)
            entries = entries.Where(e => e.Category == FilterCategory.Value);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
            entries = entries.Where(e =>
                e.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.Content.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        return entries
            .OrderByDescending(e => e.IsPinned)
            .ThenByDescending(e => e.ModifiedAt);
    }

    /// <summary>
    /// Marks the first pinned row and the first unpinned row that follows pinned ones, which
    /// are the only two places a heading appears.
    /// </summary>
    /// <remarks>
    /// A row cannot work this out on its own — it depends on the neighbour above — so the
    /// list assigns it once per rebuild. It used to be recomputed inside the container
    /// recycling callback, where an entry missing from the collection mid-refresh produced a
    /// heading that flickered on and off.
    /// </remarks>
    protected override void AssignSectionHeaders(IReadOnlyList<SecureNoteItemViewModel> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var previousPinned = i > 0 && items[i - 1].IsPinned;

            item.SectionHeader = item.IsPinned switch
            {
                true when i == 0 || !previousPinned => Res.GetString("NoteSectionPinned"),
                false when i > 0 && previousPinned => Res.GetString("NoteSectionOthers"),
                _ => string.Empty
            };
        }
    }

    /// <summary>Sets the category filter (<see langword="null"/> clears it).</summary>
    public void SetFilter(NoteCategory? category) => FilterCategory = category;

    /// <summary>Re-applies the filter when the selected category changes.</summary>
    partial void OnFilterCategoryChanged(NoteCategory? value)
    {
        Log.Debug(LogArea.List, "Category filter changed",
            $"type={EntityTypeName} category={value?.ToString() ?? "(all)"}");
        RebuildItems();
    }

    /// <inheritdoc/>
    protected override void ResetSectionState() => FilterCategory = null;

    /// <summary>Wires the notes-only instant pin toggle onto a new editing session.</summary>
    protected override void OnDetailSessionCreated(BaseDetailViewModel<SecureNoteEntry> detail)
    {
        if (detail is SecureNoteDetailViewModel noteDetail)
            noteDetail.PinToggled = OnPinToggled;
    }

    /// <summary>Releases the pin callback when an editing session ends.</summary>
    protected override void OnDetailSessionClosing(BaseDetailViewModel<SecureNoteEntry> detail)
    {
        if (detail is SecureNoteDetailViewModel noteDetail)
            noteDetail.PinToggled = null;
    }

    /// <summary>
    /// Persists an instant pin toggle, which bypasses the Save button, and reorders the list.
    /// </summary>
    private async void OnPinToggled()
    {
        // async void event callback: an escaping exception would terminate the process.
        try
        {
            await VaultState.SaveVaultAsync();
            await LoadEntriesCommand.ExecuteAsync(null);
            Log.Info(LogArea.Persist, "Vault saved after pin toggle", $"type={EntityTypeName}");
        }
        catch (Exception ex)
        {
            // The pin state is already in the in-memory vault and persists with the next
            // successful save, so the list stays as it is and only the failure is reported.
            Log.Error(LogArea.Persist, "Vault save FAILED after pin toggle", ex, $"type={EntityTypeName}");
            Toast.Show(ToastSeverity.Error, Res.GetString("ToastSaveError"));
        }
    }
}
