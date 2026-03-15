namespace PassKey.Core.Constants;

/// <summary>
/// Classifies a secure note by topic.
/// Each category maps to a distinct Fluent UI icon and a unique accent colour
/// in the note list view to aid visual scanning.
/// </summary>
public enum NoteCategory
{
    /// <summary>General-purpose notes with no specific topic. Default category. Accent: blue-grey (#607D8B).</summary>
    General,

    /// <summary>Private personal notes (diary entries, personal reminders, etc.). Accent: blue (#2196F3).</summary>
    Personal,

    /// <summary>Work-related notes (meeting notes, project information, credentials, etc.). Accent: orange (#FF9800).</summary>
    Work,

    /// <summary>Financial notes (bank account details, investment information, PIN codes, etc.). Accent: green (#4CAF50).</summary>
    Financial,

    /// <summary>Medical notes (prescriptions, doctor contact details, health records, etc.). Accent: red (#F44336).</summary>
    Medical,

    /// <summary>Travel notes (itineraries, booking references, visa information, etc.). Accent: purple (#9C27B0).</summary>
    Travel,

    /// <summary>Educational notes (course details, academic credentials, study materials, etc.). Accent: cyan (#00BCD4).</summary>
    Education,

    /// <summary>Legal notes (contract summaries, legal document references, etc.). Accent: brown (#795548).</summary>
    Legal,

    /// <summary>Technical notes (server credentials, API keys, configuration notes, etc.). Accent: indigo (#3F51B5).</summary>
    Technical,

    /// <summary>Miscellaneous notes that do not fit into any other category. Accent: grey (#9E9E9E).</summary>
    Other
}
