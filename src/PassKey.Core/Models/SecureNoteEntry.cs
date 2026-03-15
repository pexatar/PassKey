using PassKey.Core.Constants;

namespace PassKey.Core.Models;

/// <summary>
/// Represents an encrypted free-text note in the PassKey vault.
/// Stored as part of the encrypted vault blob in the VaultData SQLite table.
/// </summary>
public sealed class SecureNoteEntry
{
    /// <summary>Gets or sets the unique identifier for this note.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the display title of the note.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the full text content of the note (plaintext, encrypted at rest).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the category used for visual organisation and colour coding.</summary>
    public NoteCategory Category { get; set; } = NoteCategory.General;

    /// <summary>Gets or sets a value indicating whether this note is pinned to the top of the list.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this note was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp of the last modification to this note.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
