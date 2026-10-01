using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Liest Uhrlagen aus einem VSA-Fund, ohne alte als Schadenlage gespeicherte
/// WinCan-Meterwerte erneut als Uhrzeit zu uebernehmen.
/// </summary>
public static class VsaFindingClockResolver
{
    public static VsaFindingClockPositions Resolve(VsaFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        var start = Normalize(finding.SchadenlageAnfang);
        var end = Normalize(finding.SchadenlageEnde);

        // Der alte WinCan-Mapper kopierte MeterStart und MeterEnd 1:1 in die
        // beiden Schadenlage-Felder. Wenn beide Paare identisch sind, ist die
        // Herkunft eindeutig und die Werte sind keine Uhrlage.
        if (Same(finding.SchadenlageAnfang, finding.MeterStart)
            && finding.SchadenlageEnde.HasValue
            && finding.MeterEnd.HasValue
            && Same(finding.SchadenlageEnde, finding.MeterEnd))
        {
            return default;
        }

        return new VsaFindingClockPositions(start, end);
    }

    private static int? Normalize(double? value)
    {
        if (!value.HasValue || !double.IsFinite(value.Value))
            return null;

        var rounded = Math.Round(value.Value);
        return rounded is >= 1 and <= 12
               && Math.Abs(value.Value - rounded) < 0.0001
            ? (int)rounded
            : null;
    }

    private static bool Same(double? left, double? right)
        => left.HasValue
           && right.HasValue
           && Math.Abs(left.Value - right.Value) < 0.0001;
}

public readonly record struct VsaFindingClockPositions(int? Start, int? End);
