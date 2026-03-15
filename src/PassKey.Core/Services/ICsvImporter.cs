using PassKey.Core.Models;

namespace PassKey.Core.Services;

public interface ICsvImporter
{
    Vault ParseCsv(string csvContent);
}
