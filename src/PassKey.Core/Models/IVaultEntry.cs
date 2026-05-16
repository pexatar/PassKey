namespace PassKey.Core.Models;

/// <summary>
/// Common contract for all vault entry types (passwords, credit cards, identities, secure notes).
/// Used by generic ViewModels and services that need to operate uniformly across entry types
/// without coupling to concrete model classes.
/// </summary>
public interface IVaultEntry
{
    /// <summary>The unique identifier for this entry (assigned on creation).</summary>
    Guid Id { get; }

    /// <summary>The UTC timestamp of the last modification to this entry.</summary>
    DateTime ModifiedAt { get; set; }
}
