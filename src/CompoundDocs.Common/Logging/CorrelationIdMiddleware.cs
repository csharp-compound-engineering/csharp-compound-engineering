using Serilog.Context;

namespace CompoundDocs.Common.Logging;

/// <summary>
/// Manages correlation IDs for request tracing.
/// </summary>
public sealed class CorrelationContext : IDisposable
{
    private readonly IDisposable _logContext;

    public string CorrelationId { get; }

    public CorrelationContext(string? correlationId = null)
    {
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")[..8];
        _logContext = LogContext.PushProperty("CorrelationId", CorrelationId);
    }

    public static CorrelationContext Create(string? correlationId = null)
    {
        return new CorrelationContext(correlationId);
    }

    public void Dispose()
    {
        _logContext.Dispose();
    }
}
