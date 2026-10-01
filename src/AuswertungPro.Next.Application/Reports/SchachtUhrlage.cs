using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Textregel: die Uhrlage eines Anschlusses («12», «4», «4.5», «04:30», «7 Uhr») als
/// Winkel im Uhrzeigersinn ab dem Auslauf, 0 bis 360 Grad. Am Schacht ist 12 Uhr der Auslauf
/// (VSA) — genau die Ausrichtung des Grundrisses «Auslauf oben». SchachtPro schreibt die
/// Uhrzeit je Anschluss in seine Tabelle; die Handskizze des Uri-Formulars liefert sie ueber
/// den Skizzenparser. Unlesbares oder Werte ausserhalb 0 bis 12 ergeben <c>null</c>, nie 0.
/// </summary>
public static class SchachtUhrlage
{
    private static readonly Regex Muster = new(
        @"^\s*(?<h>\d{1,2})(?:(?<trenner>[.,:])(?<m>\d{1,2}))?\s*(?:Uhr|h)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Winkel in Grad ab dem Auslauf (12 Uhr = 0), oder <c>null</c>.</summary>
    public static double? Grad(string? uhr)
    {
        var m = Muster.Match(uhr ?? string.Empty);
        if (!m.Success)
            return null;

        var stunden = double.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        if (m.Groups["m"].Success)
        {
            var rest = m.Groups["m"].Value;
            var zahl = double.Parse(rest, CultureInfo.InvariantCulture);
            // «4:30» sind Minuten; «4.5» und «4,5» sind Zehntelstunden.
            stunden += m.Groups["trenner"].Value == ":" ? zahl / 60d : zahl / Math.Pow(10, rest.Length);
        }

        if (stunden < 0 || stunden > 12)
            return null;

        return stunden >= 12 ? 0d : stunden * 30d;
    }
}
