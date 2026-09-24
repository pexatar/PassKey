using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Constants;
using PassKey.Core.Models;
using PassKey.Desktop.Helpers;

namespace PassKey.Desktop.ViewModels.Items;

/// <summary>
/// One row of the secure notes list: title, content preview, category, relative date and
/// pin state, all ready to display and all announcing their own changes.
/// </summary>
/// <remarks>
/// Every property here replaces a line that used to run inside
/// <c>NotesList_ContainerContentChanging</c>, walking the visual tree by child index to paint
/// a recycled container. That traversal broke silently whenever the template changed and
/// could not know when an entry had been edited.
/// </remarks>
public sealed partial class SecureNoteItemViewModel : EntryItemViewModel<SecureNoteEntry>
{
    /// <summary>Maximum characters of note body shown under the title.</summary>
    private const int PreviewLength = 80;

    private static readonly ResourceLoader s_res = new();

    /// <summary>Wraps the supplied note.</summary>
    public SecureNoteItemViewModel(SecureNoteEntry model) : base(model)
    {
    }

    /// <summary>The note's title.</summary>
    public string Title => Model.Title;

    /// <summary>The note's category, bound through the brush converter for the accent bar.</summary>
    public NoteCategory Category => Model.Category;

    /// <summary>The localized category name shown under the preview.</summary>
    public string CategoryName => NoteCategoryInfo.GetName(Model.Category);

    /// <summary>Whether the note is pinned above the others.</summary>
    public bool IsPinned => Model.IsPinned;

    /// <summary>Last modification as a relative date ("2 ore fa").</summary>
    public string RelativeDate => RelativeDateFormatter.Format(Model.ModifiedAt);

    /// <summary>First line of the note body, flattened and trimmed to fit one row.</summary>
    public string Preview
    {
        get
        {
            var flattened = Model.Content.ReplaceLineEndings(" ");
            return flattened.Length > PreviewLength
                ? flattened[..PreviewLength] + "..."
                : flattened;
        }
    }

    /// <summary>Screen-reader status for the row; empty when the note is not pinned.</summary>
    public string PinnedStatus => Model.IsPinned ? s_res.GetString("NotePinnedStatus") : string.Empty;

    /// <inheritdoc/>
    public override void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(CategoryName));
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(RelativeDate));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(PinnedStatus));
    }
}
