using CommunityToolkit.Mvvm.ComponentModel;

namespace PassKey.Desktop.Services;

/// <summary>
/// Manages the ViewModel-first navigation stack for the application shell.
/// Supports push, pop, and replace operations over a stack of <see cref="ObservableObject"/> ViewModels.
/// </summary>
public interface INavigationStack
{
    /// <summary>Gets the ViewModel currently at the top of the navigation stack, or <c>null</c> if the stack is empty.</summary>
    ObservableObject? CurrentViewModel { get; }

    /// <summary>Raised whenever the top-of-stack ViewModel changes (push, pop, replace, or clear).</summary>
    event Action<ObservableObject?>? CurrentChanged;

    /// <summary>Resolves a new instance of <typeparamref name="T"/> from the DI container and pushes it onto the stack.</summary>
    /// <typeparam name="T">The ViewModel type to navigate to.</typeparam>
    void NavigateTo<T>() where T : ObservableObject;

    /// <summary>Pushes an existing ViewModel instance onto the stack.</summary>
    /// <param name="viewModel">The ViewModel to push.</param>
    void NavigateTo(ObservableObject viewModel);

    /// <summary>Pops the top-most ViewModel from the stack, returning to the previous one.</summary>
    void GoBack();

    /// <summary>Replaces the top-most entry with a new instance of <typeparamref name="T"/> resolved from DI.</summary>
    /// <typeparam name="T">The ViewModel type to replace with.</typeparam>
    void Replace<T>() where T : ObservableObject;

    /// <summary>Replaces the top-most entry with the provided ViewModel instance.</summary>
    /// <param name="viewModel">The ViewModel to place at the top of the stack.</param>
    void Replace(ObservableObject viewModel);

    /// <summary>Removes all ViewModels from the stack.</summary>
    void Clear();

    /// <summary>Gets a value indicating whether there is at least one entry below the current top.</summary>
    bool CanGoBack { get; }
}
