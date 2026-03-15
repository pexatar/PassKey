using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PassKey.Desktop.Services;

/// <summary>
/// Wraps the WinRT <see cref="FileSavePicker"/> and <see cref="FileOpenPicker"/> for use in an
/// unpackaged WinUI 3 application. Unpackaged apps must associate the picker with a window handle
/// via <see cref="InitializeWithWindow.Initialize"/> (IInitializeWithWindow) before calling any
/// picker method; this is done automatically in <see cref="InitializePicker"/>.
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    /// <summary>
    /// Shows a Save File dialog for the given file type.
    /// </summary>
    /// <param name="suggestedName">Default file name pre-filled in the dialog.</param>
    /// <param name="extension">File extension including the leading dot (e.g., ".json").</param>
    /// <param name="extensionDescription">Human-readable label for the file type shown in the filter drop-down.</param>
    /// <returns>
    /// The absolute path chosen by the user, or null if the dialog was cancelled.
    /// </returns>
    public async Task<string?> PickSaveFileAsync(string suggestedName, string extension, string extensionDescription)
    {
        var picker = new FileSavePicker();
        InitializePicker(picker);
        picker.SuggestedFileName = suggestedName;
        picker.FileTypeChoices.Add(extensionDescription, [extension]);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    /// <summary>
    /// Shows an Open File dialog filtered to the given file extension.
    /// </summary>
    /// <param name="extension">File extension including the leading dot (e.g., ".csv").</param>
    /// <param name="extensionDescription">Human-readable label for the file type shown in the filter drop-down.</param>
    /// <returns>
    /// The absolute path of the selected file, or null if the dialog was cancelled.
    /// </returns>
    public async Task<string?> PickOpenFileAsync(string extension, string extensionDescription)
    {
        var picker = new FileOpenPicker();
        InitializePicker(picker);
        picker.FileTypeFilter.Add(extension);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    /// <summary>
    /// Associates the picker with the main window handle (HWND) using the
    /// <c>IInitializeWithWindow</c> COM interface. Required for unpackaged WinUI 3 apps;
    /// without this call the picker throws a COM access-denied exception.
    /// </summary>
    /// <param name="picker">The picker object to initialize (either <see cref="FileSavePicker"/> or <see cref="FileOpenPicker"/>).</param>
    private static void InitializePicker(object picker)
    {
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
    }
}
