namespace CompoundDocs.Cleanup;

public sealed class CleanupOptions
{
    public bool RunOnce { get; set; }
    public bool DryRun { get; set; }
    public int IntervalMinutes { get; set; } = 60;
    public int GracePeriodMinutes { get; set; }
}
