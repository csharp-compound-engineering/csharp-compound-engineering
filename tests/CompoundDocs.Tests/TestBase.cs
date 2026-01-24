namespace CompoundDocs.Tests.Infrastructure;

/// <summary>
/// Base class for unit tests providing common test utilities and patterns.
/// </summary>
public abstract class TestBase : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the mock repository for creating and verifying mocks.
    /// </summary>
    protected MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    /// <summary>
    /// Creates a mock with default (strict) behavior.
    /// </summary>
    /// <typeparam name="T">The type to mock.</typeparam>
    /// <returns>A new mock instance.</returns>
    protected Mock<T> CreateMock<T>() where T : class
        => MockRepository.Create<T>();

    /// <summary>
    /// Creates a mock with loose behavior (returns defaults for unconfigured members).
    /// </summary>
    /// <typeparam name="T">The type to mock.</typeparam>
    /// <returns>A new mock instance with loose behavior.</returns>
    protected Mock<T> CreateLooseMock<T>() where T : class
        => new(MockBehavior.Loose);

    /// <summary>
    /// Verifies all strict mocks were called as expected.
    /// </summary>
    protected void VerifyAllMocks()
        => MockRepository.VerifyAll();

    /// <summary>
    /// Creates a cancellation token that will cancel after the specified timeout.
    /// Default is 30 seconds for unit tests.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>A cancellation token.</returns>
    protected static CancellationToken CreateTestCancellationToken(TimeSpan? timeout = null)
    {
        var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
        return cts.Token;
    }

    /// <summary>
    /// Disposes of test resources. Override to add custom cleanup.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Verify all strict mocks on dispose
                try
                {
                    MockRepository.VerifyAll();
                }
                catch (MockException)
                {
                    // Swallow verification errors in dispose - they should be caught by the test
                }
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Disposes of test resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Base class for async unit tests with proper async disposal support.
/// </summary>
public abstract class AsyncTestBase : TestBase, IAsyncLifetime
{
    /// <summary>
    /// Override to perform async initialization before each test.
    /// </summary>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Override to perform async cleanup after each test.
    /// </summary>
    public virtual Task DisposeAsync() => Task.CompletedTask;
}
