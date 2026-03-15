namespace PassKey.Core.Models;

/// <summary>
/// Represents a single record in the vault activity log.
/// Activity log entries are stored in plaintext in the ActivityLog SQLite table
/// and are used to track access and modification events for audit purposes.
/// </summary>
public sealed class ActivityLogEntry
{
    /// <summary>Gets or sets the auto-incremented primary key for this log record.</summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the type of entity this log entry refers to.
    /// Typical values: <c>"Password"</c>, <c>"CreditCard"</c>, <c>"Identity"</c>, <c>"SecureNote"</c>, <c>"Vault"</c>.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier of the affected entity.
    /// Corresponds to the <c>Id</c> property of the relevant entry type.
    /// May be <see cref="Guid.Empty"/> for vault-level events (e.g., unlock, lock).
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Gets or sets a short description of the action that was performed.
    /// Typical values: <c>"Created"</c>, <c>"Updated"</c>, <c>"Deleted"</c>, <c>"Copied"</c>,
    /// <c>"Unlocked"</c>, <c>"Locked"</c>.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when this event was recorded.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
