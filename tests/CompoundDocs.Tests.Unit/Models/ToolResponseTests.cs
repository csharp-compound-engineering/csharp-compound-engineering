using CompoundDocs.McpServer.Tools;

namespace CompoundDocs.Tests.Unit.Models;

public class ToolResponseTests
{
    [Fact]
    public void GenericOk_SetsSuccessAndData()
    {
        var result = ToolResponse<string>.Ok("test data");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("test data");
        result.Error.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void GenericFail_WithString_SetsErrorFields()
    {
        var result = ToolResponse<string>.Fail("error message", "ERROR_CODE");

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Error.Should().Be("error message");
        result.ErrorCode.Should().Be("ERROR_CODE");
    }

    [Fact]
    public void GenericFail_WithToolError_SetsErrorFields()
    {
        var result = ToolResponse<string>.Fail(ToolErrors.EmptyQuery);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Query cannot be empty.");
        result.ErrorCode.Should().Be("EMPTY_QUERY");
    }

    [Fact]
    public void NonGenericOk_SetsSuccess()
    {
        var result = ToolResponse.Ok("done");

        result.Success.Should().BeTrue();
        result.Message.Should().Be("done");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void NonGenericFail_SetsErrorFields()
    {
        var result = ToolResponse.Fail(ToolErrors.OperationCancelled);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("The operation was cancelled.");
        result.ErrorCode.Should().Be("OPERATION_CANCELLED");
    }
}

public class ToolErrorsTests
{
    [Fact]
    public void EmptyQuery_HasCorrectCodeAndMessage()
    {
        ToolErrors.EmptyQuery.Code.Should().Be("EMPTY_QUERY");
        ToolErrors.EmptyQuery.Message.Should().Be("Query cannot be empty.");
    }

    [Fact]
    public void EmbeddingFailed_IncludesReason()
    {
        var error = ToolErrors.EmbeddingFailed("timeout");
        error.Code.Should().Be("EMBEDDING_FAILED");
        error.Message.Should().Contain("timeout");
    }

    [Fact]
    public void SearchFailed_IncludesReason()
    {
        var error = ToolErrors.SearchFailed("index not found");
        error.Code.Should().Be("SEARCH_FAILED");
        error.Message.Should().Contain("index not found");
    }

    [Fact]
    public void RagSynthesisFailed_IncludesReason()
    {
        var error = ToolErrors.RagSynthesisFailed("model unavailable");
        error.Code.Should().Be("RAG_SYNTHESIS_FAILED");
        error.Message.Should().Contain("model unavailable");
    }

    [Fact]
    public void OperationCancelled_HasCorrectCodeAndMessage()
    {
        ToolErrors.OperationCancelled.Code.Should().Be("OPERATION_CANCELLED");
    }

    [Fact]
    public void UnexpectedError_IncludesReason()
    {
        var error = ToolErrors.UnexpectedError("something broke");
        error.Code.Should().Be("UNEXPECTED_ERROR");
        error.Message.Should().Contain("something broke");
    }
}
