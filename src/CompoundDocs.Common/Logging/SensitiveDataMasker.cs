using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;

namespace CompoundDocs.Common.Logging;

/// <summary>
/// Masks sensitive data in log messages.
/// </summary>
public sealed partial class SensitiveDataMasker : ILogEventEnricher
{
    private static readonly string[] SensitivePropertyNames =
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

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToMask = logEvent.Properties
            .Where(p => SensitivePropertyNames.Any(s =>
                p.Key.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var prop in propertiesToMask)
        {
            logEvent.AddOrUpdateProperty(
                propertyFactory.CreateProperty(prop.Key, "***MASKED***"));
        }
    }
}

/// <summary>
/// Destructuring policy that masks sensitive data.
/// </summary>
public sealed partial class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly Regex PasswordRegex = PasswordPattern();
    private static readonly Regex ConnectionStringRegex = ConnectionStringPattern();

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        if (value is string str)
        {
            var masked = MaskSensitiveData(str);
            if (masked != str)
            {
                result = propertyValueFactory.CreatePropertyValue(masked);
                return true;
            }
        }

        result = null!;
        return false;
    }

    private static string MaskSensitiveData(string value)
    {
        // Mask password patterns
        value = PasswordRegex.Replace(value, "$1***MASKED***$3");

        // Mask connection string passwords
        value = ConnectionStringRegex.Replace(value, "$1***MASKED***$3");

        return value;
    }

    [GeneratedRegex(@"(password\s*=\s*)([^;]+)(;?)", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"(Password=)([^;]+)(;?)", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPattern();
}
