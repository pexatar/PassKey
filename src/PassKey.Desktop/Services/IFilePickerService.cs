namespace PassKey.Desktop.Services;

/// <summary>
/// Wraps the WinRT <c>FileSavePicker</c> and <c>FileOpenPicker</c> APIs for use in
/// an unpackaged WinUI 3 application, handling window-handle initialisation automatically.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Opens a Save File dialog and returns the selected path, or <c>null</c> if the user cancelled.
    /// </summary>
    /// <param name="suggestedName">Default file name shown in the dialog.</param>
    /// <param name="extension">File extension filter, e.g. <c>".pkbak"</c>.</param>
    /// <param name="extensionDescription">Human-readable description for the extension filter.</param>
    /// <returns>The chosen file path, or <c>null</c> if cancelled.</returns>
    Task<string?> PickSaveFileAsync(string suggestedName, string extension, string extensionDescription);

    /// <summary>
    /// Opens an Open File dialog and returns the selected path, or <c>null</c> if the user cancelled.
    /// </summary>
    /// <param name="extension">File extension filter, e.g. <c>".pkbak"</c>.</param>
    /// <param name="extensionDescription">Human-readable description for the extension filter.</param>
    /// <returns>The chosen file path, or <c>null</c> if cancelled.</returns>
    Task<string?> PickOpenFileAsync(string extension, string extensionDescription);

    /// <summary>
    /// Opens an Open File dialog accepting any of several extensions (e.g. a Bitwarden
    /// export that may be <c>.json</c> or a <c>.zip</c> with attachments — FU3).
    /// </summary>
    /// <param name="extensions">File extension filters, each including the leading dot.</param>
    /// <param name="extensionDescription">Human-readable description for the filter.</param>
    /// <returns>The chosen file path, or <c>null</c> if cancelled.</returns>
    Task<string?> PickOpenFileAsync(IReadOnlyList<string> extensions, string extensionDescription);
}
