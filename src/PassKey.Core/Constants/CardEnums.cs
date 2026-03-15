namespace PassKey.Core.Constants;

/// <summary>
/// Categorises the intended use or ownership context of a saved payment card.
/// </summary>
public enum CardCategory
{
    /// <summary>A personal card used for everyday private expenses.</summary>
    Personal,

    /// <summary>A corporate or business card used for work-related expenses.</summary>
    Work,

    /// <summary>A card primarily used for travel bookings (flights, hotels, car hire).</summary>
    Travel,

    /// <summary>A virtual or dedicated card used for online purchases.</summary>
    Online
}

/// <summary>
/// Accent colour applied to the skeuomorphic card rendering in <c>CreditCardControl</c>.
/// Each value maps to a predefined dark material colour suitable for contrast against white card text.
/// </summary>
public enum CardColor
{
    /// <summary>Charcoal grey (#37474F) — the default colour for unassigned cards.</summary>
    Default,

    /// <summary>Deep blue (#1565C0).</summary>
    Blue,

    /// <summary>Deep red (#C62828).</summary>
    Red,

    /// <summary>Dark green (#2E7D32).</summary>
    Green,

    /// <summary>Dark purple (#6A1B9A).</summary>
    Purple,

    /// <summary>Deep orange (#E65100).</summary>
    Orange,

    /// <summary>Teal (#00838F).</summary>
    Teal,

    /// <summary>Deep pink (#AD1457).</summary>
    Pink,

    /// <summary>Amber gold (#F9A825).</summary>
    Gold,

    /// <summary>Near-black (#212121).</summary>
    Black
}

/// <summary>
/// Identifies the payment card network, detected automatically from the Primary Account Number (PAN)
/// via BIN (Bank Identification Number) prefix matching and Luhn checksum validation.
/// </summary>
public enum CardType
{
    /// <summary>
    /// Card network could not be determined from the PAN prefix,
    /// or the card number has not yet been entered.
    /// </summary>
    Unknown,

    /// <summary>Visa — PANs starting with digit 4.</summary>
    Visa,

    /// <summary>
    /// Mastercard — PANs starting with 51–55 or in the range 2221–2720.
    /// </summary>
    MasterCard,

    /// <summary>American Express — PANs starting with 34 or 37. 15-digit PANs.</summary>
    Amex,

    /// <summary>Discover — PANs starting with 6011, 622126–622925, 644–649, or 65.</summary>
    Discover,

    /// <summary>JCB (Japan Credit Bureau) — PANs starting with 3528–3589.</summary>
    JCB,

    /// <summary>Maestro — PANs starting with 6304, 6759, 6761–6763.</summary>
    Maestro,

    /// <summary>Diners Club — PANs starting with 300–305, 36, or 38. 14-digit PANs.</summary>
    DinersClub
}
