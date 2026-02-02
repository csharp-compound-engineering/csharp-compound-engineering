namespace CompoundDocs.GitSync;

public record ChangedFile
{
    public required string Path { get; init; }
    public required ChangeType ChangeType { get; init; }
}

public enum ChangeType
{
    Added,
    Modified,
    Deleted
}
