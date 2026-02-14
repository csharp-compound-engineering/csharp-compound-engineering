using CompoundDocs.McpServer.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Resilience;

public sealed class ResiliencePoliciesTests
{
    private static IOptions<ResilienceOptions> CreateDefaultOptions()
        => Microsoft.Extensions.Options.Options.Create(new ResilienceOptions());

    private static ILogger<ResiliencePolicies> CreateNullLogger()
        => NullLogger<ResiliencePolicies>.Instance;

    [Fact]
    public void Constructor_NullOptions_ShouldThrowArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ResiliencePolicies(null!, CreateNullLogger()));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_NullLogger_ShouldThrowArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ResiliencePolicies(CreateDefaultOptions(), null!));

        exception.ParamName.ShouldBe("logger");
    }

    [Fact]
    public void Constructor_ValidArguments_ShouldCreateAllPipelines()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());

        sut.OllamaPipeline.ShouldNotBeNull();
        sut.DatabasePipeline.ShouldNotBeNull();
        sut.DefaultPipeline.ShouldNotBeNull();
    }

    [Fact]
    public void OllamaPipeline_ShouldNotBeNull()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());

        sut.OllamaPipeline.ShouldNotBeNull();
        sut.OllamaPipeline.ShouldBeAssignableTo<ResiliencePipeline>();
    }

    [Fact]
    public void DatabasePipeline_ShouldNotBeNull()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());

        sut.DatabasePipeline.ShouldNotBeNull();
        sut.DatabasePipeline.ShouldBeAssignableTo<ResiliencePipeline>();
    }

    [Fact]
    public void DefaultPipeline_ShouldNotBeNull()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());

        sut.DefaultPipeline.ShouldNotBeNull();
        sut.DefaultPipeline.ShouldBeAssignableTo<ResiliencePipeline>();
    }

    [Fact]
    public async Task ExecuteWithOllamaResilienceAsync_ShouldExecuteOperationAndReturnResult()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());
        const string expected = "ollama-result";

        var result = await sut.ExecuteWithOllamaResilienceAsync(
            ct => Task.FromResult(expected));

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task ExecuteWithDatabaseResilienceAsync_ShouldExecuteOperationAndReturnResult()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());
        const int expected = 42;

        var result = await sut.ExecuteWithDatabaseResilienceAsync(
            ct => Task.FromResult(expected));

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task ExecuteWithDefaultResilienceAsync_ShouldExecuteOperationAndReturnResult()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());
        var expected = new List<string> { "a", "b", "c" };

        var result = await sut.ExecuteWithDefaultResilienceAsync(
            ct => Task.FromResult(expected));

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task ExecuteWithOllamaResilienceAsync_ShouldPropagateCancellationToken()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());
        CancellationToken receivedToken = default;
        using var cts = new CancellationTokenSource();

        await sut.ExecuteWithOllamaResilienceAsync(ct =>
        {
            receivedToken = ct;
            return Task.FromResult("done");
        }, cts.Token);

        receivedToken.ShouldNotBe(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteWithDatabaseResilienceAsync_ShouldPropagateCancellationToken()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());
        CancellationToken receivedToken = default;
        using var cts = new CancellationTokenSource();

        await sut.ExecuteWithDatabaseResilienceAsync(ct =>
        {
            receivedToken = ct;
            return Task.FromResult("done");
        }, cts.Token);

        receivedToken.ShouldNotBe(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteWithDefaultResilienceAsync_ShouldPropagateCancellationToken()
    {
        var sut = new ResiliencePolicies(CreateDefaultOptions(), CreateNullLogger());
        CancellationToken receivedToken = default;
        using var cts = new CancellationTokenSource();

        await sut.ExecuteWithDefaultResilienceAsync(ct =>
        {
            receivedToken = ct;
            return Task.FromResult("done");
        }, cts.Token);

        receivedToken.ShouldNotBe(CancellationToken.None);
    }
}
