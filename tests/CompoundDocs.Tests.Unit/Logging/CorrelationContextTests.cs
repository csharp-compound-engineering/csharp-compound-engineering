using CompoundDocs.Common.Logging;

namespace CompoundDocs.Tests.Unit.Logging;

public class CorrelationContextTests
{
    [Fact]
    public void Constructor_NoId_GeneratesCorrelationId()
    {
        // Arrange & Act
        using var context = new CorrelationContext();

        // Assert
        context.CorrelationId.ShouldNotBeNull();
        context.CorrelationId.ShouldNotBeEmpty();
    }

    [Fact]
    public void Constructor_NoId_GeneratesEightCharId()
    {
        // Arrange & Act
        using var context = new CorrelationContext();

        // Assert
        context.CorrelationId.Length.ShouldBe(8);
    }

    [Fact]
    public void Constructor_WithCustomId_UsesProvidedId()
    {
        // Arrange
        var customId = "myid";

        // Act
        using var context = new CorrelationContext(customId);

        // Assert
        context.CorrelationId.ShouldBe("myid");
    }

    [Fact]
    public void Create_NoId_ReturnsContextWithGeneratedId()
    {
        // Arrange & Act
        using var context = CorrelationContext.Create();

        // Assert
        context.ShouldNotBeNull();
        context.CorrelationId.ShouldNotBeNull();
        context.CorrelationId.Length.ShouldBe(8);
    }

    [Fact]
    public void Create_WithCustomId_ReturnsContextWithProvidedId()
    {
        // Arrange & Act
        using var context = CorrelationContext.Create("custom");

        // Assert
        context.CorrelationId.ShouldBe("custom");
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var context = new CorrelationContext();

        // Act & Assert
        Should.NotThrow(() => context.Dispose());
    }

    [Fact]
    public void Constructor_GeneratesUniqueIds()
    {
        // Arrange & Act
        using var context1 = new CorrelationContext();
        using var context2 = new CorrelationContext();

        // Assert
        context1.CorrelationId.ShouldNotBe(context2.CorrelationId);
    }

    [Fact]
    public void Current_ReturnsActiveCorrelationId()
    {
        // Arrange & Act
        using var context = new CorrelationContext("test-id");

        // Assert
        CorrelationContext.Current.ShouldBe("test-id");
    }

    [Fact]
    public void Current_WhenNoContext_ReturnsNull()
    {
        CorrelationContext.Current.ShouldBeNull();
    }

    [Fact]
    public void Dispose_RestoresPreviousValue()
    {
        // Arrange
        using var outer = new CorrelationContext("outer-id");

        // Act
        var inner = new CorrelationContext("inner-id");
        CorrelationContext.Current.ShouldBe("inner-id");
        inner.Dispose();

        // Assert
        CorrelationContext.Current.ShouldBe("outer-id");
    }
}
