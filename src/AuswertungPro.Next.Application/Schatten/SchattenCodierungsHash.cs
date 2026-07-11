using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Schatten;

/// <summary>
/// Fingerabdruck der CODIERUNG einer Haltung (nicht der Auswertung). Aendert sich der
/// Hash gegenueber dem gespeicherten Schatten-Ergebnis, ist das Ergebnis "veraltet".
/// Reihenfolge der Findings ist bewusst egal (sortiert), damit Import-Reihenfolgen
/// keinen falschen Staleness-Alarm ausloesen.
/// </summary>
public static class SchattenCodierungsHash
{
    public static string Compute(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var teile = record.VsaFindings
            .Select(f => string.Join('|',
                (f.KanalSchadencode ?? "").Trim().ToUpperInvariant(),
                (f.Quantifizierung1 ?? "").Trim(),
                (f.Quantifizierung2 ?? "").Trim(),
                FormatMeter(f.MeterStart ?? f.SchadenlageAnfang),
                FormatMeter(f.MeterEnd ?? f.SchadenlageEnde)))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        // Fallback-Quelle der Bewertung + Kontext, der in Noten/Empfehlung einfliesst.
        teile.Add("PS:" + record.GetFieldValue("Primaere_Schaeden").Trim());
        teile.Add("CTX:" + string.Join('|',
            record.GetFieldValue("Haltungslaenge_m").Trim(),
            record.GetFieldValue("DN_mm").Trim(),
            record.GetFieldValue("Rohrmaterial").Trim()));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', teile)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string FormatMeter(double? m)
        => m.HasValue ? m.Value.ToString("0.###", CultureInfo.InvariantCulture) : "";
}
