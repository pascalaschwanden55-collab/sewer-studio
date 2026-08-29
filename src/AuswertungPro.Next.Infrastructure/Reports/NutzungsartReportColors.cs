using System;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>Zusammengehoerige Akzent- und Hintergrundfarbe einer Nutzungsart.</summary>
internal readonly record struct NutzungsartReportColorPair(string Accent, string Light);

/// <summary>
/// Eine gemeinsame Farbregel fuer alle PDF-Berichte. Normbegriffe und ihre alten
/// Schreibweisen erhalten dadurch in jedem Bericht dieselbe Kennzeichnung.
/// </summary>
internal static class NutzungsartReportColors
{
    private const string SchmutzAccent = "#7A6242";
    private const string SchmutzLight = "#F5F0E8";
    private const string WasserAccent = "#4A7FA5";
    private const string WasserLight = "#EBF2F7";
    private const string MischAccent = "#8E4A6E";
    private const string MischLight = "#F5ECF1";
    private const string NeutralAccent = "#7A8A94";
    private const string NeutralLight = "#F2F4F5";

    public static NutzungsartReportColorPair Resolve(string? value)
    {
        var normalized = NutzungsartVokabular.Normalisieren(value).ToUpperInvariant();

        if (normalized.Contains("SCHMUTZ", StringComparison.Ordinal))
            return new NutzungsartReportColorPair(SchmutzAccent, SchmutzLight);

        if (normalized.Contains("NIEDERSCHLAG", StringComparison.Ordinal)
            || normalized.Contains("REGEN", StringComparison.Ordinal)
            || normalized.Contains("RAIN", StringComparison.Ordinal)
            || normalized.Contains("METEOR", StringComparison.Ordinal)
            || normalized.Contains("REIN", StringComparison.Ordinal))
        {
            return new NutzungsartReportColorPair(WasserAccent, WasserLight);
        }

        if (normalized.Contains("MISCH", StringComparison.Ordinal))
            return new NutzungsartReportColorPair(MischAccent, MischLight);

        return new NutzungsartReportColorPair(NeutralAccent, NeutralLight);
    }

    public static string ResolveLight(string? accent) => accent switch
    {
        SchmutzAccent => SchmutzLight,
        WasserAccent => WasserLight,
        MischAccent => MischLight,
        _ => NeutralLight
    };
}
