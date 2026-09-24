using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Constants;

namespace PassKey.Desktop.Helpers;

/// <summary>
/// Presentation facts about <see cref="NoteCategory"/>: the localized name and the accent
/// colour used for the card border, the filter dots and the editor's category dot.
/// </summary>
/// <remarks>
/// These were static helpers on the notes <i>list</i> ViewModel, which forced the editor
/// panel and the filter flyout to reach into a list ViewModel just to colour a dot — and
/// made the list ViewModel impossible to replace without touching both views. They describe
/// the enum, not a screen, so they live in one place next to the other display helpers.
/// </remarks>
public static class NoteCategoryInfo
{
    private static readonly ResourceLoader s_res = new();

    /// <summary>The colour identifying a category, as "#RRGGBB".</summary>
    public static string GetColor(NoteCategory category) => category switch
    {
        NoteCategory.General => "#607D8B",
        NoteCategory.Personal => "#2196F3",
        NoteCategory.Work => "#FF9800",
        NoteCategory.Financial => "#4CAF50",
        NoteCategory.Medical => "#F44336",
        NoteCategory.Travel => "#9C27B0",
        NoteCategory.Education => "#00BCD4",
        NoteCategory.Legal => "#795548",
        NoteCategory.Technical => "#3F51B5",
        NoteCategory.Other => "#9E9E9E",
        _ => "#607D8B"
    };

    /// <summary>The localized display name of a category.</summary>
    public static string GetName(NoteCategory category) => category switch
    {
        NoteCategory.General => s_res.GetString("NoteCategoryGeneral"),
        NoteCategory.Personal => s_res.GetString("NoteCategoryPersonal"),
        NoteCategory.Work => s_res.GetString("NoteCategoryWork"),
        NoteCategory.Financial => s_res.GetString("NoteCategoryFinancial"),
        NoteCategory.Medical => s_res.GetString("NoteCategoryMedical"),
        NoteCategory.Travel => s_res.GetString("NoteCategoryTravel"),
        NoteCategory.Education => s_res.GetString("NoteCategoryEducation"),
        NoteCategory.Legal => s_res.GetString("NoteCategoryLegal"),
        NoteCategory.Technical => s_res.GetString("NoteCategoryTechnical"),
        NoteCategory.Other => s_res.GetString("NoteCategoryOther"),
        _ => s_res.GetString("NoteCategoryGeneral")
    };
}
