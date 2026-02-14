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

    [Fact]
    public void NonGenericFail_WithErrorAndCode_ReturnsFailedResponse()
    {
        // Act
        var response = ToolResponse.Fail("something failed", "ERR_001");

        // Assert
        response.Success.ShouldBeFalse();
        response.Error.ShouldBe("something failed");
        response.ErrorCode.ShouldBe("ERR_001");
    }

    [Fact]
    public void NonGenericFail_WithErrorOnly_ReturnsFailedResponseWithNullCode()
    {
        // Act
        var response = ToolResponse.Fail("something failed");

        // Assert
        response.Success.ShouldBeFalse();
        response.Error.ShouldBe("something failed");
        response.ErrorCode.ShouldBeNull();
    }
}
