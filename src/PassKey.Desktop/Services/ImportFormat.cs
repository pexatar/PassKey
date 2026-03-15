namespace PassKey.Desktop.Services;

/// <summary>
/// Identifies the source format of a credential import file.
/// </summary>
public enum ImportFormat
{
    /// <summary>Generic comma-separated values export (e.g. from Chrome, Firefox, or a spreadsheet).</summary>
    Csv,

    /// <summary>KeePass 2.x database file (<c>.kdbx</c>), parsed via KeePassLib.</summary>
    Kdbx,

    /// <summary>1Password encrypted export bundle (<c>.1pux</c> ZIP archive).</summary>
    OnePux,

    /// <summary>Bitwarden JSON export (unencrypted or password-protected).</summary>
    Bitwarden
}
