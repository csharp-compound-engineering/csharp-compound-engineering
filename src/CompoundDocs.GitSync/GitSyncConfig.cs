namespace CompoundDocs.GitSync;

public class GitSyncConfig
{
    public string CloneBaseDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "compound-docs-repos");
}
