using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Erstellt den zusammengefassten "Primaere_Schaeden"-Text aus einer Liste von
/// Protokoll-Eintraegen. Identische Kern-Logik aus WinCan und IBAK konsolidiert.
/// Streckenschaden-Marker (A01, B02) werden zum echten VSA-Code aufgeloest.
/// </summary>
internal static class PrimaryDamagesTextBuilder
{
    /// <summary>
    /// Baut den Primaere_Schaeden-Text und gibt ihn zurueck.
    /// </summary>
    /// <param name="entries">Protokoll-Eintraege der Haltung.</param>
    /// <param name="skipAePrefix">
    /// Wenn <see langword="true"/>, werden IBAK-Header-Codes (AEC, AED, AEF) uebersprungen.
    /// Bei WinCan-Import bleibt dies <see langword="false"/>.
    /// </param>
    /// <returns>Deduplizierter Text oder <see langword="null"/> wenn keine Eintraege.</returns>
    public static string? Build(IReadOnlyList<ProtocolEntry> entries, bool skipAePrefix)
    {
        if (entries.Count == 0)
            return null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        foreach (var entry in entries)
        {
            var rawCode = (entry.Code ?? "").Trim().ToUpperInvariant();

            // Optional: IBAK-Header-Codes (Stammdaten) nicht als Schaeden aufnehmen
            if (skipAePrefix && rawCode.StartsWith("AE", StringComparison.OrdinalIgnoreCase))
                continue;

            var desc = entry.Beschreibung?.Trim();
            if (string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(rawCode))
                continue;

            // Streckenschaden-Marker zum echten VSA-Code aufloesen
            var code = ContinuousDefectCodeResolver.ResolveEffectiveCode(rawCode, desc, out var resolvedDesc);

            // Deduplicate nach effektivem Code + Meterposition
            if (code.Length > 0)
            {
                var meterKey = entry.MeterStart.HasValue ? entry.MeterStart.Value.ToString("F2") : "";
                var key = $"{code}|{meterKey}";
                if (!seen.Add(key))
                    continue;
            }

            var line = "";
            if (entry.MeterStart.HasValue)
                line = $"{entry.MeterStart.Value:0.00}m ";

            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(resolvedDesc))
                line += $"{code} {resolvedDesc}";
            else if (!string.IsNullOrWhiteSpace(code))
                line += code;
            else
                line += resolvedDesc;

            lines.Add(line.TrimEnd());
        }

        if (lines.Count == 0)
            return null;

        return XtfPrimaryDamageFormatter.DeduplicateText(string.Join("\n", lines));
    }
}
