using CommunityToolkit.Mvvm.ComponentModel;
using PassKey.Core.Models;

namespace PassKey.Desktop.ViewModels.Items;

/// <summary>
/// Base for the observable row objects that list templates bind to: it wraps one vault entry
/// and exposes its values ready to display, announcing every change.
/// </summary>
/// <typeparam name="TEntry">The wrapped vault entry type.</typeparam>
/// <remarks>
/// <para>
/// <b>Why a wrapper instead of making the models observable.</b> The models in
/// <c>PassKey.Core</c> are the encryption and serialization contract, shared with the tests
/// and with the browser host process; keeping them plain data protects that contract from
/// coupling to the interface. The row object is also the natural home for the values that
/// are computed for display only — preview text, localized category name, relative date —
/// which were previously written onto recycled containers by hand.
/// </para>
/// <para>
/// <b>Why it matters.</b> An in-place edit changes the very object the list already holds, so
/// no collection change is raised and a hand-painted row keeps showing the previous values.
/// That is the whole of FUN-02: a working save that looked broken. A row that announces its
/// own changes removes the need for any refresh trick.
/// </para>
/// </remarks>
public abstract partial class EntryItemViewModel<TEntry> : ObservableObject
    where TEntry : class, IVaultEntry
{
    /// <summary>Wraps the supplied entry.</summary>
    protected EntryItemViewModel(TEntry model) => Model = model;

    /// <summary>The wrapped entry. The row never copies its values: it reads through to this.</summary>
    public TEntry Model { get; }

    /// <summary>The wrapped entry's identifier, used to match rows across a rebuild.</summary>
    public Guid Id => Model.Id;

    /// <summary>
    /// Heading shown above this row when it opens a section, empty when it does not. Assigned
    /// by the list, which is the only thing that knows a row's neighbours.
    /// </summary>
    /// <remarks>
    /// Empty rather than null on purpose: this is bound straight to a TextBlock, and assigning
    /// null to <c>TextBlock.Text</c> throws.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSectionHeader))]
    public partial string SectionHeader { get; set; } = string.Empty;

    /// <summary>Whether this row opens a section. Drives the heading's visibility.</summary>
    public bool HasSectionHeader => SectionHeader.Length > 0;

    /// <summary>
    /// Re-reads every displayed value from the model and raises the notifications.
    /// Called after a save, when the underlying entry has been edited in place.
    /// </summary>
    public abstract void Refresh();
}
