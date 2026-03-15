using PassKey.Core.Models;

namespace PassKey.Core.Services;

public interface IBitwardenImporter
{
    Vault ParseBitwarden(string jsonContent);
}
