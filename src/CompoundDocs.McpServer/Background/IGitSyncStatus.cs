namespace CompoundDocs.McpServer.Background;

internal interface IGitSyncStatus
{
    DateTimeOffset? LastSuccessfulRun { get; }
    bool LastRunFailed { get; }
    int IntervalSeconds { get; }
}
