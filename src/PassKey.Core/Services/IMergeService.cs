using PassKey.Core.Models;

namespace PassKey.Core.Services;

public interface IMergeService
{
    ImportResult MergeInto(Vault target, Vault source, ImportMergeStrategy strategy);
}
