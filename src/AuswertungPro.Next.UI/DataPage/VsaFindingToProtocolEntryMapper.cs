using System;
using System.Collections.Generic;
using System.Globalization;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
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

            var clock = DataPageProtocolObservationMapper.ApplyClockTextFallback(
                VsaFindingClockResolver.Resolve(f),
                entry,
                f.Raw);

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1)
                || !string.IsNullOrWhiteSpace(f.Quantifizierung2)
                || clock.Start.HasValue
                || clock.End.HasValue)
            {
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                    ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                };

                if (clock.Start.HasValue)
                    parameters["vsa.uhr.von"] = clock.Start.Value.ToString(CultureInfo.InvariantCulture);
                if (clock.End.HasValue)
                    parameters["vsa.uhr.bis"] = clock.End.Value.ToString(CultureInfo.InvariantCulture);

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
}
