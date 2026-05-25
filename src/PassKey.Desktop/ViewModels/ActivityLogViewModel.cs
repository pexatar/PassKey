using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Interfaces;
using PassKey.Desktop.Services;

namespace PassKey.Desktop.ViewModels;

/// <summary>
/// ViewModel for the activity-log ("Cronologia") viewer: loads the most recent vault
/// activity entries newest-first and supports exporting the full list to a CSV file.
/// </summary>
public partial class ActivityLogViewModel : ObservableObject
{
    /// <summary>Upper bound on entries loaded into the viewer. The log is capped so a very
    /// long history never stalls the UI; the newest <see cref="MaxEntries"/> are shown.</summary>
    private const int MaxEntries = 1000;

    private readonly IVaultRepository _repository;
    private readonly IFilePickerService _filePicker;
    private readonly IToastService _toast;
    private readonly ResourceLoader _res = new();

    /// <summary>Activity entries shown in the list, ordered newest-first.</summary>
    public ObservableCollection<ActivityLogItem> Entries { get; } = [];

    /// <summary><see langword="true"/> when there are no entries — drives the empty-state placeholder.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public ActivityLogViewModel(
        IVaultRepository repository,
        IFilePickerService filePicker,
        IToastService toast)
    {
        _repository = repository;
        _filePicker = filePicker;
        _toast = toast;
    }

    /// <summary>Loads the most recent activity entries from the repository into <see cref="Entries"/>.</summary>
    public async Task LoadAsync()
    {
        Entries.Clear();
        var raw = await _repository.GetRecentActivityAsync(MaxEntries);
        foreach (var e in raw)
        {
            Entries.Add(new ActivityLogItem
            {
                FormattedTime = e.Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                EntityLabel = ResolveEntityLabel(e.EntityType),
                ActionLabel = ResolveActionLabel(e.Action),
                Action = e.Action,
                IconGlyph = ResolveIcon(e.EntityType)
            });
        }
        IsEmpty = Entries.Count == 0;
    }

    /// <summary>Exports every loaded entry to a UTF-8 (BOM) CSV file chosen via the save dialog.</summary>
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Entries.Count == 0) return;

        var path = await _filePicker.PickSaveFileAsync(
            $"PassKey_Cronologia_{DateTime.Now:yyyyMMdd}",
            ".csv",
            _res.GetString("ActivityCsvFileType"));
        if (path is null) return;

        var sb = new StringBuilder();
        sb.Append(CsvField(_res.GetString("ActivityColTime"))).Append(',')
          .Append(CsvField(_res.GetString("ActivityColType"))).Append(',')
          .Append(CsvField(_res.GetString("ActivityColAction"))).Append('\n');
        foreach (var item in Entries)
        {
            sb.Append(CsvField(item.FormattedTime)).Append(',')
              .Append(CsvField(item.EntityLabel)).Append(',')
              .Append(CsvField(item.ActionLabel)).Append('\n');
        }

        try
        {
            await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            _toast.Show(ToastSeverity.Success, _res.GetString("ActivityExportSuccess"));
        }
        catch
        {
            _toast.Show(ToastSeverity.Error, _res.GetString("ActivityExportError"));
        }
    }

    /// <summary>Quotes a CSV field when it contains a comma, quote, or newline (RFC 4180).</summary>
    private static string CsvField(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private string ResolveEntityLabel(string entityType) => entityType switch
    {
        "PasswordEntry" => _res.GetString("ActivityEntityPassword"),
        "CreditCardEntry" => _res.GetString("ActivityEntityCard"),
        "IdentityEntry" => _res.GetString("ActivityEntityIdentity"),
        "SecureNoteEntry" => _res.GetString("ActivityEntityNote"),
        "Vault" => _res.GetString("ActivityEntityVault"),
        _ => entityType
    };

    private string ResolveActionLabel(string action) => action switch
    {
        "Created" => _res.GetString("ActivityActionCreated"),
        "Modified" => _res.GetString("ActivityActionModified"),
        "Updated" => _res.GetString("ActivityActionModified"),
        "Deleted" => _res.GetString("ActivityActionDeleted"),
        "Copied" => _res.GetString("ActivityActionCopied"),
        "Unlocked" => _res.GetString("ActivityActionUnlocked"),
        "Locked" => _res.GetString("ActivityActionLocked"),
        _ => action
    };

    private static string ResolveIcon(string entityType) => entityType switch
    {
        "PasswordEntry" => "",
        "CreditCardEntry" => "",
        "IdentityEntry" => "",
        "SecureNoteEntry" => "",
        "Vault" => "",
        _ => ""
    };
}

/// <summary>Display model for a single row in the activity-log viewer.</summary>
public sealed class ActivityLogItem
{
    /// <summary>Local timestamp formatted for display and CSV export.</summary>
    public string FormattedTime { get; init; } = string.Empty;

    /// <summary>Localised entity-type label (e.g. "Password", "Carta").</summary>
    public string EntityLabel { get; init; } = string.Empty;

    /// <summary>Localised action label (e.g. "Creato", "Eliminato").</summary>
    public string ActionLabel { get; init; } = string.Empty;

    /// <summary>Raw action string ("Created", "Modified", "Deleted", …) — drives the action indicator.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Segoe MDL2 glyph representing the entity type.</summary>
    public string IconGlyph { get; init; } = string.Empty;
}
