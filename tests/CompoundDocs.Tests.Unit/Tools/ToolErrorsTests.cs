using CompoundDocs.McpServer.Tools;

namespace CompoundDocs.Tests.Unit.Tools;

public class ToolErrorsTests
{
    // ToolError record tests

    [Fact]
    public void ToolError_Constructor_SetsCode()
    {
        // Arrange & Act
        var error = new ToolError("C", "M");

        // Assert
        error.Code.ShouldBe("C");
    }

    [Fact]
    public void ToolError_Constructor_SetsMessage()
    {
        // Arrange & Act
        var error = new ToolError("C", "M");

        // Assert
        error.Message.ShouldBe("M");
    }

    [Fact]
    public void ToolError_Equality_SameValues_AreEqual()
    {
        // Arrange
        var error1 = new ToolError("C", "M");
        var error2 = new ToolError("C", "M");

        // Act & Assert
        error1.ShouldBe(error2);
    }

    [Fact]
    public void ToolError_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var error1 = new ToolError("C1", "M1");
        var error2 = new ToolError("C2", "M2");

        // Act & Assert
        error1.ShouldNotBe(error2);
    }

    // ToolErrors static fields

    [Fact]
    public void EmptyQuery_HasCorrectCode()
    {
        // Arrange & Act
        var error = ToolErrors.EmptyQuery;

        // Assert
        error.Code.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public void EmptyQuery_HasCorrectMessage()
    {
        // Arrange & Act
        var error = ToolErrors.EmptyQuery;

        // Assert
        error.Message.ShouldBe("Query cannot be empty.");
    }

    [Fact]
    public void OperationCancelled_HasCorrectCode()
    {
        // Arrange & Act
        var error = ToolErrors.OperationCancelled;

        // Assert
        error.Code.ShouldBe("OPERATION_CANCELLED");
    }

    [Fact]
    public void OperationCancelled_HasCorrectMessage()
    {
        // Arrange & Act
        var error = ToolErrors.OperationCancelled;

        // Assert
        error.Message.ShouldBe("The operation was cancelled.");
    }

    // ToolErrors factory methods

    [Fact]
    public void EmbeddingFailed_IncludesReason()
    {
        // Arrange
        var reason = "timeout";

        // Act
        var error = ToolErrors.EmbeddingFailed(reason);

        // Assert
        error.Code.ShouldBe("EMBEDDING_FAILED");
        error.Message.ShouldContain(reason);
    }

    [Fact]
    public void SearchFailed_IncludesReason()
    {
        // Arrange
        var reason = "no index";

        // Act
        var error = ToolErrors.SearchFailed(reason);

        // Assert
        error.Code.ShouldBe("SEARCH_FAILED");
        error.Message.ShouldContain(reason);
    }

    [Fact]
    public void RagSynthesisFailed_IncludesReason()
    {
        // Arrange
        var reason = "model error";

        // Act
        var error = ToolErrors.RagSynthesisFailed(reason);

        // Assert
        error.Code.ShouldBe("RAG_SYNTHESIS_FAILED");
        error.Message.ShouldContain(reason);
    }

    [Fact]
    public void UnexpectedError_IncludesReason()
    {
        // Arrange
        var reason = "null ref";

        // Act
        var error = ToolErrors.UnexpectedError(reason);

        // Assert
        error.Code.ShouldBe("UNEXPECTED_ERROR");
        error.Message.ShouldContain(reason);
    }

    [Fact]
    public void FactoryMethods_ReturnNewInstances()
    {
        // Arrange & Act
        var error1 = ToolErrors.EmbeddingFailed("reason");
        var error2 = ToolErrors.EmbeddingFailed("reason");

        // Assert
        ReferenceEquals(error1, error2).ShouldBe(false);
    }

    [Fact]
    public void StaticFields_AreSameInstance()
    {
        // Arrange & Act
        var error1 = ToolErrors.EmptyQuery;
        var error2 = ToolErrors.EmptyQuery;

        // Assert
        ReferenceEquals(error1, error2).ShouldBe(true);
    }
}
