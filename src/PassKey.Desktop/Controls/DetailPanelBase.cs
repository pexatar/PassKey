using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace PassKey.Desktop.Controls;

/// <summary>
/// Base for the editor panels shown beside a section's list. The panel instance is stable;
/// the ViewModel it shows is replaced once per editing session.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why (R4, R5).</b> Previously the panel view was rebuilt from code on every selection
/// and attached itself to a single shared ViewModel that was never released, so the list had
/// to null the property and reassign the same instance just to provoke a notification. Here
/// the list hands over a new ViewModel per session through a dependency property, the panel
/// re-reads its bindings, and nothing has to be poked into refreshing.
/// </para>
/// </remarks>
public abstract partial class DetailPanelBase : BoundViewBase
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(ObservableObject),
            typeof(DetailPanelBase),
            new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>The ViewModel of the current editing session, or <see langword="null"/> when closed.</summary>
    public ObservableObject? ViewModel
    {
        get => (ObservableObject?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (DetailPanelBase)d;

        panel.ReleaseTrackedSubscriptions();
        panel.DataContext = e.NewValue;
        panel.OnViewModelReplaced(e.OldValue as ObservableObject, e.NewValue as ObservableObject);
        panel.RefreshBindings();
    }

    /// <summary>
    /// Called after the session ViewModel has been replaced, with subscriptions already
    /// released and the bindings about to be refreshed. Subclasses apply the state that is
    /// not expressible as a binding, such as initial focus.
    /// </summary>
    protected virtual void OnViewModelReplaced(ObservableObject? oldViewModel, ObservableObject? newViewModel)
    {
    }
}
