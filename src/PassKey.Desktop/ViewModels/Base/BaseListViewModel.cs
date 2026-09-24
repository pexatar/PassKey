using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Interfaces;
using PassKey.Core.Models;
using PassKey.Desktop.Services;
using PassKey.Desktop.ViewModels.Items;

namespace PassKey.Desktop.ViewModels.Base;

/// <summary>
/// Shared implementation of a vault section's master-detail list: the observable rows, the
/// search and sort pipeline, the selection, and the life cycle of the detail panel.
/// </summary>
/// <typeparam name="TEntry">The vault entry type this section lists.</typeparam>
/// <typeparam name="TItem">The observable row object wrapping <typeparamref name="TEntry"/>.</typeparam>
/// <remarks>
/// <para>
/// <b>Why this class exists (ARC-03).</b> The four section list ViewModels shared roughly
/// 70-80% of their skeleton across about a thousand duplicated lines, so every fix had to be
/// applied four times — and repeatedly was not. The stale-list remedy reached three sections
/// out of four; the crash guard on saving reached all four only because it was added
/// deliberately, one file at a time. Here each of those lives once.
/// </para>
/// <para>
/// <b>Detail panel life cycle (R4).</b> A brand-new detail ViewModel is created for every
/// editing session and disposed when the panel closes. The previous design injected a single
/// shared instance and had to null the property and reassign it to force a notification,
/// because the object never actually changed. A new instance notifies on its own.
/// </para>
/// <para>
/// <b>Persistence failures (ASY-01, R9).</b> The two callbacks the detail panel raises are
/// <c>async void</c> by necessity — they are event-style notifications — so an escaping
/// exception would terminate the process rather than surface as an error. Both funnel into
/// guarded methods here, in one place, instead of eight hand-written try/catch blocks.
/// </para>
/// </remarks>
public abstract partial class BaseListViewModel<TEntry, TItem> : ObservableObject, IDisposable
    where TEntry : class, IVaultEntry
    where TItem : EntryItemViewModel<TEntry>
{
    /// <summary>In-memory unlocked vault.</summary>
    protected readonly IVaultStateService VaultState;

    /// <summary>Serialized dialog host; WinUI tolerates only one ContentDialog at a time.</summary>
    protected readonly IDialogQueueService DialogQueue;

    /// <summary>Activity log persistence.</summary>
    protected readonly IVaultRepository Repository;

    /// <summary>Toast notifications shown to the user.</summary>
    protected readonly IToastService Toast;

    /// <summary>Diagnostic log (LOG-01). Every state transition worth explaining lands here (R8).</summary>
    protected readonly ILogService Log;

    /// <summary>Localized strings.</summary>
    protected readonly ResourceLoader Res = new();

    private readonly Func<BaseDetailViewModel<TEntry>> _detailFactory;
    private List<TEntry> _allEntries = [];
    private bool _disposed;

    /// <summary>The rows currently shown, after search and sort. Bound by the list template.</summary>
    public ObservableCollection<TItem> Items { get; } = [];

    /// <summary>The highlighted row, or <see langword="null"/> when nothing is selected.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    public partial TItem? SelectedItem { get; set; }

    /// <summary>Free-text search applied to the section's searchable fields.</summary>
    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    /// <summary>True when the section holds no entries at all (drives the "create your first" state).</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>True when filters are active and match nothing (drives the "no results" state).</summary>
    [ObservableProperty]
    public partial bool IsFilteredEmpty { get; set; }

    /// <summary>True when there is at least one row to show. Drives the list's visibility.</summary>
    [ObservableProperty]
    public partial bool HasItems { get; set; }

    /// <summary>True while an editing session is open in the detail panel.</summary>
    [ObservableProperty]
    public partial bool IsDetailOpen { get; set; }

    /// <summary>The ViewModel of the current editing session, or <see langword="null"/> when closed.</summary>
    [ObservableProperty]
    public partial BaseDetailViewModel<TEntry>? DetailViewModel { get; set; }

    /// <summary>Initialises the shared dependencies. Subclasses pass these through their own DI constructor.</summary>
    protected BaseListViewModel(
        IVaultStateService vaultState,
        IDialogQueueService dialogQueue,
        IVaultRepository repository,
        IToastService toast,
        ILogService log,
        Func<BaseDetailViewModel<TEntry>> detailFactory)
    {
        VaultState = vaultState;
        DialogQueue = dialogQueue;
        Repository = repository;
        Toast = toast;
        Log = log;
        _detailFactory = detailFactory;

        VaultState.VaultLocked += OnVaultLocked;
    }

    // ─── Template-method hooks (subclass implementations) ─────────────────────

    /// <summary>Entity label used in the activity log and in diagnostic lines (e.g. "SecureNoteEntry").</summary>
    protected abstract string EntityTypeName { get; }

    /// <summary>Returns the vault's typed collection holding this section's entries.</summary>
    protected abstract IList<TEntry> GetVaultCollection(Vault vault);

    /// <summary>Creates the observable row wrapping the supplied entry.</summary>
    protected abstract TItem CreateItem(TEntry entry);

    /// <summary>Applies this section's search, filter and sort rules.</summary>
    protected abstract IEnumerable<TEntry> FilterAndSort(IEnumerable<TEntry> entries);

    /// <summary>Best-effort display name shown in the delete confirmation dialog.</summary>
    protected abstract string GetDisplayName(TItem item);

    /// <summary>Assigns section headings once the visible rows and their order are known.</summary>
    protected virtual void AssignSectionHeaders(IReadOnlyList<TItem> items)
    {
        foreach (var item in items) item.SectionHeader = string.Empty;
    }

    /// <summary>Wires the section-specific callbacks of a freshly created detail ViewModel.</summary>
    protected virtual void OnDetailSessionCreated(BaseDetailViewModel<TEntry> detail) { }

    /// <summary>Releases the section-specific callbacks before a detail ViewModel is dropped.</summary>
    protected virtual void OnDetailSessionClosing(BaseDetailViewModel<TEntry> detail) { }

    /// <summary>Resets section-specific state when the vault locks (e.g. an active filter).</summary>
    protected virtual void ResetSectionState() { }

    // ─── Loading and rebuilding ───────────────────────────────────────────────

    /// <summary>Reloads this section's entries from the unlocked vault and rebuilds the rows.</summary>
    [RelayCommand]
    public Task LoadEntriesAsync()
    {
        var vault = VaultState.CurrentVault;
        _allEntries = vault is null ? [] : [.. GetVaultCollection(vault)];
        RebuildItems();
        return Task.CompletedTask;
    }

    /// <summary>Re-applies search and sort whenever the query changes.</summary>
    partial void OnSearchQueryChanged(string value) => RebuildItems();

    /// <summary>
    /// Rebuilds the visible rows from the loaded entries, preserving row identity and selection.
    /// </summary>
    /// <remarks>
    /// Rows already showing an entry are reused rather than recreated, so the bindings the
    /// templates hold stay attached to the same objects and only the changed values are
    /// announced. Each reused row is refreshed, which is what makes an in-place edit appear
    /// without any container-rebuilding trick.
    /// </remarks>
    protected void RebuildItems()
    {
        var previouslySelected = SelectedItem?.Id;

        var existing = new Dictionary<Guid, TItem>();
        foreach (var item in Items) existing[item.Id] = item;

        var ordered = FilterAndSort(_allEntries).ToList();

        Items.Clear();
        foreach (var entry in ordered)
        {
            var row = existing.TryGetValue(entry.Id, out var reused) && ReferenceEquals(reused.Model, entry)
                ? reused
                : CreateItem(entry);
            row.Refresh();
            Items.Add(row);
        }

        AssignSectionHeaders(Items);

        IsEmpty = _allEntries.Count == 0;
        IsFilteredEmpty = Items.Count == 0 && !IsEmpty;
        HasItems = Items.Count > 0;

        SelectedItem = previouslySelected is null
            ? null
            : Items.FirstOrDefault(i => i.Id == previouslySelected.Value);

        Log.Debug(LogArea.List, "List rebuilt",
            $"type={EntityTypeName} total={_allEntries.Count} shown={Items.Count}");
    }

    // ─── Detail panel life cycle (R4) ─────────────────────────────────────────

    /// <summary>Opens an editing session for a brand-new entry.</summary>
    [RelayCommand]
    protected void AddNew()
    {
        SelectedItem = null;
        var detail = OpenDetailSession();
        detail.StartNew();
        IsDetailOpen = true;
    }

    /// <summary>Opens an editing session for the supplied row.</summary>
    [RelayCommand]
    protected void SelectItem(TItem? item)
    {
        if (item is null) return;

        SelectedItem = item;
        var detail = OpenDetailSession();
        detail.StartEdit(item.Model);
        IsDetailOpen = true;
    }

    /// <summary>Closes the current editing session and clears the selection.</summary>
    public void CloseDetail()
    {
        CloseDetailSession();
        IsDetailOpen = false;
        SelectedItem = null;
        Log.Debug(LogArea.Detail, "Detail session closed", $"type={EntityTypeName}");
    }

    /// <summary>Creates and wires a detail ViewModel for one editing session.</summary>
    private BaseDetailViewModel<TEntry> OpenDetailSession()
    {
        // Releasing the previous session first is what guarantees exactly one live editing
        // ViewModel: opening a second entry without closing the first used to leave the
        // earlier one wired to this list.
        CloseDetailSession();

        var detail = _detailFactory();
        detail.Saved = OnEntrySaved;
        detail.Deleted = OnEntryDeleted;
        detail.Cancelled = CloseDetail;
        OnDetailSessionCreated(detail);

        DetailViewModel = detail;
        return detail;
    }

    /// <summary>Unwires and disposes the current detail ViewModel, if any.</summary>
    private void CloseDetailSession()
    {
        var detail = DetailViewModel;
        if (detail is null) return;

        DetailViewModel = null;
        OnDetailSessionClosing(detail);
        detail.Dispose();
    }

    // ─── Persistence (ASY-01 / R9: guarded in one place) ──────────────────────

    /// <summary>Detail panel callback: an entry was created or edited in the in-memory vault.</summary>
    private async void OnEntrySaved(bool isNew, Guid entryId)
    {
        // async void event callback: nothing above can catch what escapes, so nothing may.
        try
        {
            await PersistSaveAsync(isNew, entryId);
        }
        catch (Exception ex)
        {
            Log.Error(LogArea.Persist, "Unexpected failure while handling a save", ex,
                $"type={EntityTypeName} entryId={entryId}");
        }
    }

    /// <summary>Detail panel callback: an entry was removed from the in-memory vault.</summary>
    private async void OnEntryDeleted(Guid entryId)
    {
        // async void event callback: nothing above can catch what escapes, so nothing may.
        try
        {
            await PersistDeletionAsync(entryId);
        }
        catch (Exception ex)
        {
            Log.Error(LogArea.Persist, "Unexpected failure while handling a deletion", ex,
                $"type={EntityTypeName} entryId={entryId}");
        }
    }

    /// <summary>Writes the vault to disk after a save and refreshes the list. Never throws.</summary>
    private async Task PersistSaveAsync(bool isNew, Guid entryId)
    {
        try
        {
            await VaultState.SaveVaultAsync();
            await Repository.LogActivityAsync(new ActivityLogEntry
            {
                EntityType = EntityTypeName,
                EntityId = entryId,
                Action = isNew ? "Created" : "Modified",
                Timestamp = DateTime.UtcNow
            });
            await LoadEntriesCommand.ExecuteAsync(null);
            Log.Info(LogArea.Persist, "Vault saved after entry save",
                $"type={EntityTypeName} entryId={entryId} isNew={isNew}");
            Toast.Show(ToastSeverity.Success, Res.GetString("ToastSaved"));
        }
        catch (Exception ex)
        {
            // The change is already in the in-memory vault and will reach disk with the next
            // successful save; the panel stays open so the user can retry. SQLite is ACID, so
            // a failed write cannot have corrupted the database.
            Log.Error(LogArea.Persist, "Vault save FAILED", ex, $"type={EntityTypeName} entryId={entryId}");
            Toast.Show(ToastSeverity.Error, Res.GetString("ToastSaveError"));
        }
    }

    /// <summary>Writes the vault to disk after a deletion and refreshes the list. Never throws.</summary>
    private async Task PersistDeletionAsync(Guid entryId)
    {
        try
        {
            await VaultState.SaveVaultAsync();
            await Repository.LogActivityAsync(new ActivityLogEntry
            {
                EntityType = EntityTypeName,
                EntityId = entryId,
                Action = "Deleted",
                Timestamp = DateTime.UtcNow
            });
            await LoadEntriesCommand.ExecuteAsync(null);
            CloseDetail();
            Log.Info(LogArea.Persist, "Vault saved after entry deletion",
                $"type={EntityTypeName} entryId={entryId}");
            Toast.Show(ToastSeverity.Info, Res.GetString("ToastDeleted"));
        }
        catch (Exception ex)
        {
            // The entry is already gone from the in-memory vault: refresh so the list matches
            // what the user sees, and say the write failed.
            Log.Error(LogArea.Persist, "Vault save FAILED after deletion", ex,
                $"type={EntityTypeName} entryId={entryId}");
            await LoadEntriesCommand.ExecuteAsync(null);
            CloseDetail();
            Toast.Show(ToastSeverity.Error, Res.GetString("ToastSaveError"));
        }
    }

    // ─── Deleting from the list (Delete key) ──────────────────────────────────

    /// <summary>Whether a row is selected and can therefore be deleted.</summary>
    protected bool CanDeleteSelected() => SelectedItem is not null;

    /// <summary>Asks for confirmation and removes the selected entry from the vault.</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    protected async Task DeleteSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null)
        {
            // Unreachable while the command drives the button, but a refusal is never silent (R3).
            Log.Warn(LogArea.Command, "Delete refused: nothing selected", $"type={EntityTypeName}");
            return;
        }

        var displayName = GetDisplayName(item);
        var confirmed = await DialogQueue.ConfirmAsync(
            title: string.Format(Res.GetString("DeleteConfirmTitle"), displayName),
            content: string.Format(Res.GetString("DeleteConfirmMessage"), displayName),
            primaryButtonText: Res.GetString("DeleteButton"),
            closeButtonText: Res.GetString("CancelButton"));

        if (!confirmed)
        {
            Log.Debug(LogArea.Command, "Delete cancelled by user", $"type={EntityTypeName} entryId={item.Id}");
            return;
        }

        var vault = VaultState.CurrentVault;
        if (vault is null)
        {
            Log.Warn(LogArea.Command, "Delete refused: vault is locked", $"type={EntityTypeName}");
            Toast.Show(ToastSeverity.Error, Res.GetString("ToastSaveError"));
            return;
        }

        GetVaultCollection(vault).Remove(item.Model);
        await PersistDeletionAsync(item.Id);
    }

    // ─── Lock and disposal ────────────────────────────────────────────────────

    /// <summary>Clears every trace of the vault contents when it locks.</summary>
    private void OnVaultLocked()
    {
        CloseDetailSession();

        _allEntries = [];
        Items.Clear();
        IsDetailOpen = false;
        SelectedItem = null;
        SearchQuery = string.Empty;
        IsEmpty = false;
        IsFilteredEmpty = false;
        HasItems = false;

        ResetSectionState();

        Log.Debug(LogArea.List, "List cleared on vault lock", $"type={EntityTypeName}");
    }

    /// <summary>Detaches the vault handler and releases any open editing session.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        VaultState.VaultLocked -= OnVaultLocked;
        CloseDetailSession();
    }
}
