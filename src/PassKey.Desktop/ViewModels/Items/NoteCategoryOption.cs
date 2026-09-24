using PassKey.Core.Constants;
using PassKey.Desktop.Helpers;

namespace PassKey.Desktop.ViewModels.Items;

/// <summary>
/// One selectable category in the editor's category picker: the value plus its localized
/// name, so the picker can be filled by a binding rather than built item by item in code.
/// </summary>
/// <remarks>
/// The shared instances in <see cref="All"/> are what let the picker bind its selection by
/// reference: a selected option is the same object the list holds.
/// </remarks>
public sealed class NoteCategoryOption
{
    private NoteCategoryOption(NoteCategory category)
    {
        Category = category;
        Name = NoteCategoryInfo.GetName(category);
    }

    /// <summary>The category this option selects.</summary>
    public NoteCategory Category { get; }

    /// <summary>The localized display name.</summary>
    public string Name { get; }

    /// <summary>Every category, in declaration order, as shared instances.</summary>
    public static IReadOnlyList<NoteCategoryOption> All { get; } =
        [.. Enum.GetValues<NoteCategory>().Select(c => new NoteCategoryOption(c))];

    /// <summary>Returns the shared option for a category.</summary>
    public static NoteCategoryOption For(NoteCategory category)
        => All.First(o => o.Category == category);
}
