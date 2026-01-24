using CompoundDocs.McpServer.Tools;

namespace CompoundDocs.Tests.Unit.Models;

public class ToolResponseTests
{
    [Fact]
    public void GenericOk_SetsSuccessAndData()
    {
        var result = ToolResponse<string>.Ok("test data");

        result.Success.ShouldBeTrue();
        result.Data.ShouldBe("test data");
        result.Error.ShouldBeNull();
        result.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public void GenericFail_WithString_SetsErrorFields()
    {
        var result = ToolResponse<string>.Fail("error message", "ERROR_CODE");

        result.Success.ShouldBeFalse();
        result.Data.ShouldBeNull();
        result.Error.ShouldBe("error message");
        result.ErrorCode.ShouldBe("ERROR_CODE");
    }

    [Fact]
    public void GenericFail_WithToolError_SetsErrorFields()
    {
        var result = ToolResponse<string>.Fail(ToolErrors.EmptyQuery);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Query cannot be empty.");
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public void NonGenericOk_SetsSuccess()
    {
        var result = ToolResponse.Ok("done");

        result.Success.ShouldBeTrue();
        result.Message.ShouldBe("done");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void NonGenericFail_SetsErrorFields()
    {
        var result = ToolResponse.Fail(ToolErrors.OperationCancelled);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("The operation was cancelled.");
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }
}

public class ToolErrorsTests
{
    [Fact]
    public void EmptyQuery_HasCorrectCodeAndMessage()
    {
        ToolErrors.EmptyQuery.Code.ShouldBe("EMPTY_QUERY");
        ToolErrors.EmptyQuery.Message.ShouldBe("Query cannot be empty.");
    }

    [Fact]
    public void EmbeddingFailed_IncludesReason()
    {
        var error = ToolErrors.EmbeddingFailed("timeout");
        error.Code.ShouldBe("EMBEDDING_FAILED");
        error.Message.ShouldContain("timeout");
    }

    [Fact]
    public void SearchFailed_IncludesReason()
    {
        var error = ToolErrors.SearchFailed("index not found");
        error.Code.ShouldBe("SEARCH_FAILED");
        error.Message.ShouldContain("index not found");
    }

    [Fact]
    public void RagSynthesisFailed_IncludesReason()
    {
        var error = ToolErrors.RagSynthesisFailed("model unavailable");
        error.Code.ShouldBe("RAG_SYNTHESIS_FAILED");
        error.Message.ShouldContain("model unavailable");
    }

    [Fact]
    public void OperationCancelled_HasCorrectCodeAndMessage()
    {
        ToolErrors.OperationCancelled.Code.ShouldBe("OPERATION_CANCELLED");
    }

    [Fact]
    public void UnexpectedError_IncludesReason()
    {
        var error = ToolErrors.UnexpectedError("something broke");
        error.Code.ShouldBe("UNEXPECTED_ERROR");
        error.Message.ShouldContain("something broke");
    }
}
