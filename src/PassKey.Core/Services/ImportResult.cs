namespace PassKey.Core.Services;

public sealed record ImportResult
{
    public int PasswordsImported { get; init; }
    public int CardsImported { get; init; }
    public int IdentitiesImported { get; init; }
    public int NotesImported { get; init; }
    public int Skipped { get; init; }
    public int Overwritten { get; init; }
    public List<string> Warnings { get; init; } = [];
}
