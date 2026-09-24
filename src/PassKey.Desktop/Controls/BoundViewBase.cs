using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PassKey.Desktop.Controls;

/// <summary>
/// Base for views whose link to their ViewModel is declarative, guaranteeing the symmetric
/// half of that link: everything attached while the view lives is released when it unloads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why (MEM-01, R5).</b> The section views are recreated on every navigation while their
/// ViewModels live as long as the shell. Each new view attached a handler and none ever
/// detached, so every visit left another unreachable view tree alive, still reacting to the
/// ViewModel. Compiled bindings have the same shape — they register on the ViewModel too —
/// so both are released here rather than in each view's code.
/// </para>
/// <para>
/// A view is never reloaded after unloading in this app: the shell builds a new instance for
/// every navigation. Releasing on <see cref="FrameworkElement.Unloaded"/> is therefore final,
/// which is what makes it safe to release the compiled bindings as well.
/// </para>
/// </remarks>
public abstract partial class BoundViewBase : UserControl
{
    private readonly List<(INotifyPropertyChanged Source, PropertyChangedEventHandler Handler)> _tracked = [];
    private bool _released;

    /// <summary>Registers the unload hook that releases everything this view attached.</summary>
    protected BoundViewBase()
    {
        Unloaded += OnBoundViewUnloaded;
    }

    /// <summary>
    /// Subscribes to a ViewModel and records the subscription so the matching unsubscribe
    /// cannot be forgotten. The only supported way for a subclass to attach a handler.
    /// </summary>
    protected void TrackPropertyChanged(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        source.PropertyChanged += handler;
        _tracked.Add((source, handler));
    }

    /// <summary>
    /// Drops every tracked subscription. Called on unload, and by a subclass before it binds
    /// to a different ViewModel. Safe to call repeatedly.
    /// </summary>
    protected void ReleaseTrackedSubscriptions()
    {
        foreach (var (source, handler) in _tracked)
            source.PropertyChanged -= handler;

        _tracked.Clear();
    }

    private void OnBoundViewUnloaded(object sender, RoutedEventArgs e)
    {
        if (_released) return;
        _released = true;

        Unloaded -= OnBoundViewUnloaded;
        ReleaseTrackedSubscriptions();
        StopBindingTracking();
    }

    /// <summary>
    /// Forwards to <c>Bindings.StopTracking()</c>. The generated bindings object belongs to
    /// the class that owns the XAML, so the base cannot reach it directly.
    /// </summary>
    protected abstract void StopBindingTracking();

    /// <summary>
    /// Forwards to <c>Bindings.Update()</c>, re-evaluating the compiled bindings after the
    /// ViewModel they read has been replaced.
    /// </summary>
    protected abstract void RefreshBindings();
}
