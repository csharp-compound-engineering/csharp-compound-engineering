using CompoundDocs.Common.Logging;
using Moq;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Logging;

public class SensitiveDataMaskerTests
{
    private readonly SensitiveDataMasker _sut = new();
    private readonly Mock<ILogEventPropertyFactory> _propertyFactory = new();

    private static MessageTemplate EmptyTemplate =>
        new(Enumerable.Empty<MessageTemplateToken>());

    private LogEvent CreateLogEvent(params LogEventProperty[] properties) =>
        new(DateTimeOffset.Now, LogEventLevel.Information, null, EmptyTemplate, properties);

    private void SetupPropertyFactory()
    {
        _propertyFactory
            .Setup(f => f.CreateProperty(It.IsAny<string>(), It.IsAny<object?>(), false))
            .Returns((string name, object? value, bool destructureObjects) =>
                new LogEventProperty(name, new ScalarValue(value)));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("apikey")]
    [InlineData("api_key")]
    [InlineData("connectionstring")]
    [InlineData("connection_string")]
    [InlineData("credential")]
    [InlineData("auth")]
    public void Enrich_WithSensitiveProperty_MasksValue(string propertyName)
    {
        // Arrange
        SetupPropertyFactory();
        var logEvent = CreateLogEvent(
            new LogEventProperty(propertyName, new ScalarValue("sensitive-value")));

        // Act
        _sut.Enrich(logEvent, _propertyFactory.Object);

        // Assert
        var prop = logEvent.Properties[propertyName];
        ((ScalarValue)prop).Value.ShouldBe("***MASKED***");
    }

    [Fact]
    public void Enrich_WithNonSensitiveProperty_LeavesUnchanged()
    {
        // Arrange
        SetupPropertyFactory();
        var logEvent = CreateLogEvent(
            new LogEventProperty("username", new ScalarValue("john")));

        // Act
        _sut.Enrich(logEvent, _propertyFactory.Object);

        // Assert
        var prop = logEvent.Properties["username"];
        ((ScalarValue)prop).Value.ShouldBe("john");
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("PASSWORD")]
    [InlineData("API_KEY")]
    [InlineData("ApiKey")]
    [InlineData("TOKEN")]
    [InlineData("ConnectionString")]
    public void Enrich_CaseInsensitive_MasksRegardlessOfCase(string propertyName)
    {
        // Arrange
        SetupPropertyFactory();
        var logEvent = CreateLogEvent(
            new LogEventProperty(propertyName, new ScalarValue("sensitive-value")));

        // Act
        _sut.Enrich(logEvent, _propertyFactory.Object);

        // Assert
        var prop = logEvent.Properties[propertyName];
        ((ScalarValue)prop).Value.ShouldBe("***MASKED***");
    }

    [Fact]
    public void Enrich_WithMultipleSensitiveProperties_MasksAll()
    {
        // Arrange
        SetupPropertyFactory();
        var logEvent = CreateLogEvent(
            new LogEventProperty("password", new ScalarValue("pass123")),
            new LogEventProperty("token", new ScalarValue("tok456")),
            new LogEventProperty("apikey", new ScalarValue("key789")));

        // Act
        _sut.Enrich(logEvent, _propertyFactory.Object);

        // Assert
        ((ScalarValue)logEvent.Properties["password"]).Value.ShouldBe("***MASKED***");
        ((ScalarValue)logEvent.Properties["token"]).Value.ShouldBe("***MASKED***");
        ((ScalarValue)logEvent.Properties["apikey"]).Value.ShouldBe("***MASKED***");
    }

    [Fact]
    public void Enrich_WithNoProperties_DoesNothing()
    {
        // Arrange
        SetupPropertyFactory();
        var logEvent = CreateLogEvent();

        // Act
        _sut.Enrich(logEvent, _propertyFactory.Object);

        // Assert
        logEvent.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void Enrich_WithMixedProperties_OnlyMasksSensitive()
    {
        // Arrange
        SetupPropertyFactory();
        var logEvent = CreateLogEvent(
            new LogEventProperty("username", new ScalarValue("john")),
            new LogEventProperty("password", new ScalarValue("secret123")),
            new LogEventProperty("action", new ScalarValue("login")),
            new LogEventProperty("token", new ScalarValue("abc-def")));

        // Act
        _sut.Enrich(logEvent, _propertyFactory.Object);

        // Assert
        ((ScalarValue)logEvent.Properties["username"]).Value.ShouldBe("john");
        ((ScalarValue)logEvent.Properties["password"]).Value.ShouldBe("***MASKED***");
        ((ScalarValue)logEvent.Properties["action"]).Value.ShouldBe("login");
        ((ScalarValue)logEvent.Properties["token"]).Value.ShouldBe("***MASKED***");
    }
}

public class SensitiveDataDestructuringPolicyTests
{
    private readonly SensitiveDataDestructuringPolicy _sut = new();
    private readonly Mock<ILogEventPropertyValueFactory> _valueFactory = new();

    private void SetupValueFactory()
    {
        _valueFactory
            .Setup(f => f.CreatePropertyValue(It.IsAny<object?>(), false))
            .Returns((object? value, bool destructureObjects) =>
                new ScalarValue(value));
    }

    [Fact]
    public void TryDestructure_StringWithPassword_MasksAndReturnsTrue()
    {
        // Arrange
        SetupValueFactory();
        var input = "password=secretValue;other=data";

        // Act
        var result = _sut.TryDestructure(input, _valueFactory.Object, out var propertyValue);

        // Assert
        result.ShouldBeTrue();
        ((ScalarValue)propertyValue).Value.ShouldBe("password=***MASKED***;other=data");
    }

    [Fact]
    public void TryDestructure_StringWithConnectionStringPassword_MasksAndReturnsTrue()
    {
        // Arrange
        SetupValueFactory();
        var input = "Server=myserver;Database=mydb;Password=supersecret;";

        // Act
        var result = _sut.TryDestructure(input, _valueFactory.Object, out var propertyValue);

        // Assert
        result.ShouldBeTrue();
        var maskedValue = (string)((ScalarValue)propertyValue).Value!;
        maskedValue.ShouldContain("***MASKED***");
        maskedValue.ShouldNotContain("supersecret");
    }

    [Fact]
    public void TryDestructure_StringWithoutSensitiveData_ReturnsFalse()
    {
        // Arrange
        SetupValueFactory();
        var input = "This is a normal log message with no sensitive data";

        // Act
        var result = _sut.TryDestructure(input, _valueFactory.Object, out _);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDestructure_NonStringValue_ReturnsFalse()
    {
        // Arrange
        SetupValueFactory();
        var input = 42;

        // Act
        var result = _sut.TryDestructure(input, _valueFactory.Object, out _);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDestructure_PasswordWithSpaces_MasksCorrectly()
    {
        // Arrange
        SetupValueFactory();
        var input = "password = secretValue;next=item";

        // Act
        var result = _sut.TryDestructure(input, _valueFactory.Object, out var propertyValue);

        // Assert
        result.ShouldBeTrue();
        var maskedValue = (string)((ScalarValue)propertyValue).Value!;
        maskedValue.ShouldContain("***MASKED***");
        maskedValue.ShouldNotContain("secretValue");
    }

    [Fact]
    public void TryDestructure_MultiplePatterns_MasksAll()
    {
        // Arrange
        SetupValueFactory();
        var input = "password=first;Password=second;other=safe";

        // Act
        var result = _sut.TryDestructure(input, _valueFactory.Object, out var propertyValue);

        // Assert
        result.ShouldBeTrue();
        var maskedValue = (string)((ScalarValue)propertyValue).Value!;
        maskedValue.ShouldNotContain("first");
        maskedValue.ShouldNotContain("second");
        maskedValue.ShouldContain("other=safe");
    }
}
