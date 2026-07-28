using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Models;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels.Base;

/// <summary>
/// Abstract base for the four "Detail" ViewModels (Password, CreditCard, Identity, SecureNote).
/// Encapsulates the shared add/edit/save/delete workflow via a template-method pattern,
/// removing ~200 lines of duplicated state and command logic across concrete subclasses.
/// </summary>
/// <typeparam name="TEntry">A concrete vault entry type implementing <see cref="IVaultEntry"/>.</typeparam>
/// <remarks>
/// <para>Lifecycle:</para>
/// <list type="number">
///   <item><see cref="StartNew"/> resets state and prepares the panel for a new entry creation.</item>
///   <item><see cref="StartEdit(TEntry)"/> loads an existing entry into the editor.</item>
///   <item><see cref="SaveAsync"/> creates or applies changes to the entry; raises <see cref="Saved"/>.</item>
///   <item><see cref="DeleteAsync"/> confirms with the user and removes the entry; raises <see cref="Deleted"/>.</item>
/// </list>
/// <para>Subclasses must implement the protected abstract hooks that handle type-specific
/// concerns (field reset/load/apply, vault collection accessor, dialog strings, validation).
/// They may additionally override <see cref="OnSavedNew(TEntry)"/> /
/// <see cref="OnSavedEdit(TEntry)"/> to perform extra work after a successful save
/// (e.g., <c>SecureNoteDetailViewModel</c> transitions to edit mode in-place after creation).</para>
/// </remarks>
public abstract partial class BaseDetailViewModel<TEntry> : ObservableObject
    where TEntry : class, IVaultEntry
{
    /// <summary>Vault state service for accessing the in-memory unlocked vault.</summary>
    protected readonly IVaultStateService VaultState;

    /// <summary>Dialog queue used to serialize <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> instances.</summary>
    protected readonly IDialogQueueService DialogQueue;

    /// <summary>Diagnostic log. Records which entry is loaded and why a save was accepted or refused.</summary>
    protected readonly ILogService Log;

    /// <summary>Localized string resources (delete dialog + subclass fallback display names).</summary>
    protected readonly ResourceLoader _res = new();

    /// <summary>The entry currently being edited, or <see langword="null"/> when creating a new entry.</summary>
    protected TEntry? EditingEntry;

    private bool _isNew;

    /// <summary>Indicates whether the panel is currently in "create new" mode (vs editing an existing entry).</summary>
    public bool IsNew => _isNew;

    /// <summary>Whether the current field values satisfy the type-specific validation rules.</summary>
    /// <remarks>
    /// Drives <c>SaveCommand.CanExecute</c>. The generated command does not observe this property
    /// on its own, so <see cref="NotifyCanExecuteChangedForAttribute"/> is what makes a bound
    /// button enable and disable itself — replacing the hand-written <c>IsEnabled</c> juggling
    /// that let a dead button look alive.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool CanSave { get; set; }

    /// <summary>Indicates that a save operation is in progress (used to disable UI / show spinner).</summary>
    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    /// <summary>Raised after a successful save. Parameters: <c>wasNew</c> and the entry's <see cref="IVaultEntry.Id"/>.</summary>
    public Action<bool, Guid>? Saved { get; set; }

    /// <summary>Raised after the entry has been deleted from the vault. Parameter: deleted entry id.</summary>
    public Action<Guid>? Deleted { get; set; }

    /// <summary>Raised when the user clicks the cancel button on the editor panel.</summary>
    public Action? Cancelled { get; set; }

    /// <summary>Initialises shared dependencies. Subclasses pass these through their own DI constructor.</summary>
    protected BaseDetailViewModel(IVaultStateService vaultState, IDialogQueueService dialogQueue, ILogService log)
    {
        VaultState = vaultState;
        DialogQueue = dialogQueue;
        Log = log;
    }

    /// <summary>Short type name used as the entity label in log lines (e.g. "CreditCardEntry").</summary>
    private static string EntryTypeName => typeof(TEntry).Name;

    // ─── Template-method hooks (subclass implementations) ─────────────────────

    /// <summary>Reset all type-specific observable properties to their defaults for a new entry.</summary>
    protected abstract void ResetFieldsForNew();

    /// <summary>Populate observable properties from the supplied entry (called by <see cref="StartEdit"/>).</summary>
    protected abstract void LoadFromEntry(TEntry entry);

    /// <summary>Construct a brand-new <typeparamref name="TEntry"/> from the current observable property values.</summary>
    protected abstract TEntry CreateNewEntry();

    /// <summary>Copy current observable property values onto the supplied existing entry (save edit).</summary>
    protected abstract void ApplyToEntry(TEntry entry);

    /// <summary>Return the vault's typed collection where this entry kind lives (Passwords / CreditCards / …).</summary>
    protected abstract IList<TEntry> GetVaultCollection(Vault vault);

    /// <summary>Recompute <see cref="CanSave"/> based on the type-specific required-field validation.</summary>
    protected abstract void UpdateCanSave();

    /// <summary>Best-effort display name shown inside the delete-confirmation dialog for the supplied entry.</summary>
    protected abstract string GetDeleteDisplayName(TEntry entry);

    /// <summary>Hook invoked after a successful save of a new entry (default: no-op).
    /// Subclasses can override to transition the panel state in-place (e.g., notes editor).</summary>
    protected virtual void OnSavedNew(TEntry entry) { }

    /// <summary>Hook invoked after a successful save of an edited entry (default: no-op).
    /// Subclasses can use this to refresh internal "original" snapshots, etc.</summary>
    protected virtual void OnSavedEdit(TEntry entry) { }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Prepare the panel for adding a brand-new entry.</summary>
    public virtual void StartNew()
    {
        EditingEntry = null;
        _isNew = true;
        ResetFieldsForNew();
        UpdateCanSave();
        Log.Debug(LogArea.Detail, "Panel opened for new entry", $"type={EntryTypeName} canSave={CanSave}");
    }

    /// <summary>Prepare the panel for editing the supplied existing entry.</summary>
    public virtual void StartEdit(TEntry entry)
    {
        EditingEntry = entry;
        _isNew = false;
        LoadFromEntry(entry);
        UpdateCanSave();
        // Records WHICH entry the shared panel is now bound to: the line that distinguishes
        // "the panel shows the right item" from "the panel is still holding the previous one".
        Log.Debug(LogArea.Detail, "Panel opened for edit", $"type={EntryTypeName} entryId={entry.Id} canSave={CanSave}");
    }

    /// <summary>Persist the new or edited entry into the in-memory vault; the actual disk write is the caller's job.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    protected virtual Task SaveAsync()
    {
        // Defence in depth: with the button bound to the command this branch should be
        // unreachable. If it is ever hit, the reason is written down instead of the command
        // failing silently — the exact blind spot that made "Save does nothing" undiagnosable.
        if (!CanSave)
        {
            Log.Warn(LogArea.Command, "Save refused: validation not satisfied",
                $"type={EntryTypeName} entryId={EditingEntry?.Id.ToString() ?? "(new)"}");
            return Task.CompletedTask;
        }

        var vault = VaultState.CurrentVault;
        if (vault is null)
        {
            Log.Warn(LogArea.Command, "Save refused: vault is locked", $"type={EntryTypeName}");
            return Task.CompletedTask;
        }

        IsSaving = true;

        try
        {
            bool wasNew = _isNew;
            Guid entryId;

            if (_isNew)
            {
                var entry = CreateNewEntry();
                GetVaultCollection(vault).Add(entry);
                entryId = entry.Id;
                OnSavedNew(entry);
            }
            else if (EditingEntry is not null)
            {
                ApplyToEntry(EditingEntry);
                EditingEntry.ModifiedAt = DateTime.UtcNow;
                entryId = EditingEntry.Id;
                OnSavedEdit(EditingEntry);
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

    /// <summary>Ask the user to confirm and then remove the current entry from the vault.</summary>
    [RelayCommand]
    protected virtual async Task DeleteAsync()
    {
        if (EditingEntry is null || _isNew) return;

        var displayName = GetDeleteDisplayName(EditingEntry);
        var confirmed = await DialogQueue.ConfirmAsync(
            title: string.Format(_res.GetString("DeleteConfirmTitle"), displayName),
            content: string.Format(_res.GetString("DeleteConfirmMessage"), displayName),
            primaryButtonText: _res.GetString("DeleteButton"),
            closeButtonText: _res.GetString("CancelButton"));

        if (confirmed)
        {
            var vault = VaultState.CurrentVault;
            var entryId = EditingEntry.Id;
            if (vault is not null)
            {
                GetVaultCollection(vault).Remove(EditingEntry);
            }
            Deleted?.Invoke(entryId);
        }
    }

    // ─── Helpers for subclass state transitions (used by SecureNote) ──────────

    /// <summary>Subclass-accessible mutator for the "is new" flag (used by special-case state transitions).</summary>
    protected void SetIsNew(bool value) => _isNew = value;
}
