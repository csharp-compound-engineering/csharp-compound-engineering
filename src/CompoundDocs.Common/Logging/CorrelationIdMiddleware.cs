namespace CompoundDocs.Common.Logging;

/// <summary>
/// Manages correlation IDs for request tracing.
/// </summary>
public sealed class CorrelationContext : IDisposable
{
    private static readonly AsyncLocal<string?> _currentValue = new();
    private readonly string? _previousId;

    public string CorrelationId { get; }

    public static string? Current => _currentValue.Value;

    public CorrelationContext(string? correlationId = null)
    {
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")[..8];
        _previousId = _currentValue.Value;
        _currentValue.Value = CorrelationId;
    }

    public static CorrelationContext Create(string? correlationId = null) =>
        new(correlationId);

    public void Dispose() =>
        _currentValue.Value = _previousId;
}
