using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>Ein Foto in der Haltungs-Galerie.</summary>
public sealed record GalerieFoto(string Pfad, string Beschriftung);

/// <summary>
/// Sammelt alle Schadensfotos einer Haltung (FotoPaths der Protokolleintraege)
/// fuer die Galerie im Haltungs-Detail: aufgeloest gegen den Projekt-Root
/// (gleiche Logik wie die Hover-Vorschau), nur existierende Dateien,
/// dedupliziert, beschriftet mit Meter + Code, nach Meter sortiert.
/// </summary>
public static class HaltungFotoGalerieBuilder
{
    public static IReadOnlyList<GalerieFoto> Build(
        HaltungRecord? record,
        string? projectRoot,
        Func<string, bool>? fileExists = null)
    {
        var entries = record?.Protocol?.Current.Entries;
        if (entries is null || entries.Count == 0)
            return Array.Empty<GalerieFoto>();

        fileExists ??= File.Exists;

        var fotos = new List<(double Meter, GalerieFoto Foto)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry.FotoPaths.Count == 0)
                continue;

            var meter = entry.MeterStart ?? 0d;
            var beschriftung = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.0} m · {1}",
                meter,
                (entry.Code ?? string.Empty).Trim());

            foreach (var pfad in PhotoHoverPreviewLogic.ResolveExistingPhotos(entry.FotoPaths, projectRoot, fileExists))
            {
                if (!seen.Add(pfad))
                    continue; // erster Eintrag gewinnt die Beschriftung

                fotos.Add((meter, new GalerieFoto(pfad, beschriftung)));
            }
        }

        fotos.Sort((a, b) => a.Meter.CompareTo(b.Meter));
        return fotos.ConvertAll(f => f.Foto);
    }
}
