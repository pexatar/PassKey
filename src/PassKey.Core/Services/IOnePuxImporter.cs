using PassKey.Core.Models;

namespace PassKey.Core.Services;

public interface IOnePuxImporter
{
    /// <param name="exportDataJson">Content of the export.data file extracted from the .1pux ZIP</param>
    Vault ParseOnePux(string exportDataJson);
}
