using System.Text.RegularExpressions;

namespace CompoundDocs.Common.Logging;

/// <summary>
/// Utility for detecting and masking sensitive data in log output.
/// </summary>
public static partial class SensitiveDataMaskingService
{
    private static readonly string[] _sensitivePropertyNames =
    [
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "connectionstring",
        "connection_string",
        "credential",
        "auth"
    ];

    public static bool IsSensitivePropertyName(string name) =>
        _sensitivePropertyNames.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));

    public static string MaskSensitiveValues(string value)
    {
        value = PasswordPattern().Replace(value, "$1***MASKED***$3");
        value = ConnectionStringPattern().Replace(value, "$1***MASKED***$3");
        return value;
    }

    [GeneratedRegex(@"(password\s*=\s*)([^;]+)(;?)", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"(Password=)([^;]+)(;?)", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPattern();
}
