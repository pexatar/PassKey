using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Desktop.Controls;
using PassKey.Desktop.ViewModels;
using PassKey.Desktop.ViewModels.Items;

namespace PassKey.Desktop.Views;

/// <summary>
/// Secure note editor panel: title and category, an edit/preview toggle over the body, and a
/// footer with delete, pin, cancel and save.
/// </summary>
/// <remarks>
/// <para>
/// Every field is a two-way binding and every button is bound to a command, so this file no
/// longer copies values between the controls and the ViewModel. The <c>_updatingFromVm</c>
/// flag that guarded those copies — and made the caret jump — has no reason to exist.
/// </para>
/// <para>
/// What is left is what a binding cannot express: where the keyboard goes when a session
/// opens, and the screen-reader announcements. The subscriptions behind the announcements are
/// registered through <see cref="BoundViewBase.TrackPropertyChanged"/>, which owns their
/// removal.
/// </para>
/// </remarks>
public sealed partial class SecureNoteDetailView : DetailPanelBase
{
    private readonly ResourceLoader _resourceLoader = new();
    private bool _initialized;

    public SecureNoteDetailView()
    {
        InitializeComponent();
        _initialized = true;
    }

    /// <summary>The editing session's ViewModel, typed for the compiled bindings.</summary>
    public SecureNoteDetailViewModel? Vm => ViewModel as SecureNoteDetailViewModel;

    /// <summary>The categories offered by the picker.</summary>
    public IReadOnlyList<NoteCategoryOption> CategoryOptions => NoteCategoryOption.All;

    /// <inheritdoc/>
    protected override void RefreshBindings()
    {
        if (_initialized) Bindings.Update();
    }

    /// <inheritdoc/>
    protected override void StopBindingTracking()
    {
        if (_initialized) Bindings.StopTracking();
    }

    /// <inheritdoc/>
    protected override void OnViewModelReplaced(ObservableObject? oldViewModel, ObservableObject? newViewModel)
    {
        if (newViewModel is not SecureNoteDetailViewModel vm) return;

        TrackPropertyChanged(vm, OnSessionPropertyChanged);

        // Focus lands on the body for an existing note and on the title for a new one.
        // Deferred because the panel is still being wired when the session is handed over.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!ReferenceEquals(Vm, vm)) return;
            if (vm.IsEditMode) ContentBox.Focus(FocusState.Programmatic);
            else TitleBox.Focus(FocusState.Programmatic);
        });
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SecureNoteDetailViewModel.HasUnsavedChanges):
                if (Vm?.HasUnsavedChanges == true)
                    Announce(_resourceLoader.GetString("NoteUnsavedChanges"));
                break;

            case nameof(SecureNoteDetailViewModel.IsSaving):
                Announce(Vm?.IsSaving == true
                    ? _resourceLoader.GetString("NoteSavingAnnounce")
                    : _resourceLoader.GetString("NoteSavedAnnounce"));
                break;
        }
    }

    private void ContentBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        Announce(string.Format(
            _resourceLoader.GetString("NoteCharWordAnnounce"),
            Vm.CharacterCount, Vm.WordCount));
    }

    private void Announce(string message)
    {
        A11yAnnouncer.Text = "";
        A11yAnnouncer.Text = message;
    }
}
