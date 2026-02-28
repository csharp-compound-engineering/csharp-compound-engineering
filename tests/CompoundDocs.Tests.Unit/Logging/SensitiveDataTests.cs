using CompoundDocs.Common.Logging;

namespace CompoundDocs.Tests.Unit.Logging;

public class SensitiveDataMaskingServicePropertyTests
{
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
    public void IsSensitivePropertyName_WithSensitiveName_ReturnsTrue(string propertyName)
    {
        SensitiveDataMaskingService.IsSensitivePropertyName(propertyName).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("PASSWORD")]
    [InlineData("API_KEY")]
    [InlineData("ApiKey")]
    [InlineData("TOKEN")]
    [InlineData("ConnectionString")]
    public void IsSensitivePropertyName_CaseInsensitive_ReturnsTrue(string propertyName)
    {
        SensitiveDataMaskingService.IsSensitivePropertyName(propertyName).ShouldBeTrue();
    }

    [Theory]
    [InlineData("username")]
    [InlineData("action")]
    [InlineData("email")]
    [InlineData("name")]
    public void IsSensitivePropertyName_WithNonSensitiveName_ReturnsFalse(string propertyName)
    {
        SensitiveDataMaskingService.IsSensitivePropertyName(propertyName).ShouldBeFalse();
    }

    [Fact]
    public void IsSensitivePropertyName_WithSubstringMatch_ReturnsTrue()
    {
        SensitiveDataMaskingService.IsSensitivePropertyName("my_api_key_header").ShouldBeTrue();
        SensitiveDataMaskingService.IsSensitivePropertyName("user_password_hash").ShouldBeTrue();
    }
}

public class SensitiveDataMaskingServiceValueTests
{
    [Fact]
    public void MaskSensitiveValues_StringWithPassword_Masks()
    {
        var input = "password=secretValue;other=data";
        var result = SensitiveDataMaskingService.MaskSensitiveValues(input);

        result.ShouldContain("***MASKED***");
        result.ShouldNotContain("secretValue");
    }

    [Fact]
    public void MaskSensitiveValues_ConnectionStringPassword_Masks()
    {
        var input = "Server=myserver;Database=mydb;Password=supersecret;";
        var result = SensitiveDataMaskingService.MaskSensitiveValues(input);

        result.ShouldContain("***MASKED***");
        result.ShouldNotContain("supersecret");
    }

    [Fact]
    public void MaskSensitiveValues_NoSensitiveData_ReturnsUnchanged()
    {
        var input = "This is a normal log message with no sensitive data";
        var result = SensitiveDataMaskingService.MaskSensitiveValues(input);

        result.ShouldBe(input);
    }

    [Fact]
    public void MaskSensitiveValues_PasswordWithSpaces_Masks()
    {
        var input = "password = secretValue;next=item";
        var result = SensitiveDataMaskingService.MaskSensitiveValues(input);

        result.ShouldContain("***MASKED***");
        result.ShouldNotContain("secretValue");
    }

    [Fact]
    public void MaskSensitiveValues_MultiplePatterns_MasksAll()
    {
        var input = "password=first;Password=second;other=safe";
        var result = SensitiveDataMaskingService.MaskSensitiveValues(input);

        result.ShouldNotContain("first");
        result.ShouldNotContain("second");
        result.ShouldContain("other=safe");
    }
}
