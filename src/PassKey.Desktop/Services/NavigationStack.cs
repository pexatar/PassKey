using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace PassKey.Desktop.Services;

/// <summary>
/// ViewModel-first navigation stack backed by a <see cref="Stack{T}"/> of
/// <see cref="ObservableObject"/> instances. The main content area binds to
/// <see cref="CurrentViewModel"/> and renders the appropriate view via a DataTemplate selector.
/// Navigation is performed by type (<see cref="NavigateTo{T}"/>) or by instance
/// (<see cref="NavigateTo(ObservableObject)"/>); the DI container resolves transient view model
/// instances on demand.
/// </summary>
public sealed class NavigationStack : INavigationStack
{
    private readonly Stack<ObservableObject> _stack = new();
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of <see cref="NavigationStack"/>.
    /// </summary>
    /// <param name="services">DI service provider used to resolve view model instances by type.</param>
    public NavigationStack(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>Gets the view model at the top of the stack, or null if the stack is empty.</summary>
    public ObservableObject? CurrentViewModel => _stack.Count > 0 ? _stack.Peek() : null;

    /// <summary>Gets a value indicating whether there is more than one entry on the stack (i.e., back navigation is possible).</summary>
    public bool CanGoBack => _stack.Count > 1;

    /// <summary>Raised whenever the current top-of-stack view model changes.</summary>
    public event Action<ObservableObject?>? CurrentChanged;

    /// <summary>
    /// Resolves a view model of type <typeparamref name="T"/> from the DI container and pushes
    /// it onto the stack, making it the new <see cref="CurrentViewModel"/>.
    /// </summary>
    /// <typeparam name="T">The view model type to navigate to. Must be registered with the DI container.</typeparam>
    public void NavigateTo<T>() where T : ObservableObject
    {
        var vm = _services.GetRequiredService<T>();
        NavigateTo(vm);
    }

    /// <summary>
    /// Pushes the given <paramref name="viewModel"/> onto the stack and raises <see cref="CurrentChanged"/>.
    /// </summary>
    /// <param name="viewModel">The view model instance to push.</param>
    public void NavigateTo(ObservableObject viewModel)
    {
        _stack.Push(viewModel);
        CurrentChanged?.Invoke(viewModel);
    }

    /// <summary>
    /// Pops the current view model and raises <see cref="CurrentChanged"/> with the new top.
    /// Has no effect if there is only one entry remaining (prevents navigating below the root).
    /// </summary>
    public void GoBack()
    {
        if (_stack.Count > 1)
        {
            _stack.Pop();
            CurrentChanged?.Invoke(CurrentViewModel);
        }
    }

    /// <summary>
    /// Resolves a view model of type <typeparamref name="T"/> from the DI container and replaces
    /// the current top-of-stack entry without adding a back entry.
    /// </summary>
    /// <typeparam name="T">The view model type to navigate to.</typeparam>
    public void Replace<T>() where T : ObservableObject
    {
        var vm = _services.GetRequiredService<T>();
        Replace(vm);
    }

    /// <summary>
    /// Pops the current top entry (if any) and pushes <paramref name="viewModel"/> in its place,
    /// raising <see cref="CurrentChanged"/>. Back navigation does not return to the replaced entry.
    /// </summary>
    /// <param name="viewModel">The view model instance to substitute.</param>
    public void Replace(ObservableObject viewModel)
    {
        if (_stack.Count > 0)
            _stack.Pop();
        _stack.Push(viewModel);
        CurrentChanged?.Invoke(viewModel);
    }

    /// <summary>
    /// Empties the navigation stack and raises <see cref="CurrentChanged"/> with null.
    /// </summary>
    public void Clear()
    {
        _stack.Clear();
        CurrentChanged?.Invoke(null);
    }
}
