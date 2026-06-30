using System.Globalization;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Validiert einen einzelnen Beobachtungsparameter anhand seines Typs und erlaubter Werte.
/// Logik aus ObservationParameterViewModel.Validate extrahiert, verhaltensneutral.
/// </summary>
public static class ObservationParameterValidator
{
    /// <summary>
    /// Validiert den Wert eines Parameters.
    /// Gibt true zurueck wenn gueltig, false mit Fehlermeldung in <paramref name="error"/> wenn ungueltig.
    /// </summary>
    public static bool Validate(
        string paramName,
        string? paramType,
        bool required,
        IReadOnlyList<string>? allowedValues,
        string? value,
        out string error)
    {
        error = string.Empty;
        var v = value?.Trim() ?? string.Empty;

        if (required && v.Length == 0)
        {
            error = $"Parameter '{paramName}' ist erforderlich.";
            return false;
        }

        if (v.Length == 0)
            return true;

        var isEnum = string.Equals(paramType, "enum", StringComparison.OrdinalIgnoreCase);
        var isNumber = string.Equals(paramType, "number", StringComparison.OrdinalIgnoreCase);
        var isClock = string.Equals(paramType, "clock", StringComparison.OrdinalIgnoreCase);

        if (isEnum && allowedValues is { Count: > 0 } && !allowedValues.Contains(v, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Parameter '{paramName}' hat einen ungueltigen Wert.";
            return false;
        }

        if (isNumber)
        {
            var normalized = v.Replace(',', '.');
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                error = $"Parameter '{paramName}' muss numerisch sein.";
                return false;
            }
        }

        if (isClock)
        {
            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var clockValue)
                || clockValue < 0
                || clockValue > 12)
            {
                error = $"Parameter '{paramName}' muss zwischen 00 und 12 liegen.";
                return false;
            }
        }

        return true;
    }
}
