using System;
using System.Collections.Generic;
using System.Globalization;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Bildet importierte VsaFindings auf ProtocolEntry-Objekte ab. Reine Logik —
/// die Katalog-Titelaufloesung wird als Delegate (<paramref name="resolveTitle"/>)
/// hereingereicht, damit der Mapper ohne ServiceProvider testbar bleibt.
/// </summary>
public static class VsaFindingToProtocolEntryMapper
{
    /// <summary>
    /// Erzeugt aus den Findings Protokolleintraege. <paramref name="resolveTitle"/>
    /// liefert (optional) einen Katalog-Titel zu einem Schadenscode; wird nur genutzt,
    /// wenn die Roh-Beschreibung leer oder zu kurz ist.
    /// </summary>
    public static IReadOnlyList<ProtocolEntry> BuildEntries(
        IEnumerable<VsaFinding> findings,
        Func<string, string?> resolveTitle)
    {
        var list = new List<ProtocolEntry>();
        foreach (var f in findings)
        {
            var mStart = f.MeterStart;
            var mEnd = f.MeterEnd;
            var time = ProtocolTimeParser.ParseMpegTime(f.MPEG) ?? (f.Timestamp?.TimeOfDay);

            var beschreibung = f.Raw?.Trim() ?? string.Empty;
            // Beschreibung aus dem VSA-Katalog aufloesen, wenn Raw leer oder nur Kuerzel
            var code = f.KanalSchadencode?.Trim() ?? string.Empty;
            if ((string.IsNullOrWhiteSpace(beschreibung) || beschreibung.Length <= 3) &&
                !string.IsNullOrWhiteSpace(code))
            {
                var title = resolveTitle(code);
                if (!string.IsNullOrWhiteSpace(title))
                    beschreibung = title;
            }

            var entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = beschreibung,
                MeterStart = mStart,
                MeterEnd = mEnd,
                IsStreckenschaden = mStart.HasValue && mEnd.HasValue && mEnd >= mStart,
                Mpeg = f.MPEG,
                Zeit = time,
                Source = ProtocolEntrySource.Imported
            };

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1)
                || !string.IsNullOrWhiteSpace(f.Quantifizierung2)
                || TryFormatClock(f.SchadenlageAnfang) is not null
                || TryFormatClock(f.SchadenlageEnde) is not null)
            {
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                    ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                };

                var uhrVon = TryFormatClock(f.SchadenlageAnfang);
                var uhrBis = TryFormatClock(f.SchadenlageEnde);
                if (!string.IsNullOrWhiteSpace(uhrVon))
                    parameters["vsa.uhr.von"] = uhrVon;
                if (!string.IsNullOrWhiteSpace(uhrBis))
                    parameters["vsa.uhr.bis"] = uhrBis;

                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = parameters,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static string? TryFormatClock(double? value)
        => value is > 0 and <= 12
            ? value.Value.ToString("0.##", CultureInfo.InvariantCulture)
            : null;
}
