using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Lookup;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>Fehlende Bezugsobjekte nur für die Ausgabe nachlesen. Projekt und Originaldatei bleiben unverändert.</summary>
internal static class XtfQuellverbundErgaenzung
{
    private static readonly HashSet<string> Organisationsrollen = new(StringComparer.Ordinal)
        { "DatenherrRef", "DatenlieferantRef", "EigentuemerRef", "BetreiberRef", "Ausfuehrende_FirmaRef" };

    internal static Project Vorbereite(Project projekt, IReadOnlyList<string>? dateien)
    {
        var alle = projekt.Objektakten.SelectMany(a => a.Quellen).Where(q => q.System == "GeoShop-XTF").ToArray();
        var vorhanden = alle.Select(q => q.Kennung).ToHashSet(StringComparer.Ordinal);
        var fehlt = Fehlende(alle, vorhanden);
        if (fehlt.Count == 0) return projekt;
        var pfade = (dateien ?? alle.Select(q => q.Datei).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
            .Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (pfade.Length == 0)
            throw new XtfQuelleFehltException($"{fehlt.Count} Original-Bezugsobjekte fehlen im gespeicherten GeoShop-Verbund. Bitte die vollständige GeoShop-XTF zum Ergänzen auswählen. Die Kundendatei wird nur gelesen.");
        var ergaenzt = new Dictionary<string, ObjektQuellbeleg>(StringComparer.Ordinal);
        foreach (var pfad in pfade)
        {
            using var stream = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.Read);
            for (var tiefe = 0; tiefe < 8 && fehlt.Count > 0; tiefe++)
            {
                var neu = new Dictionary<string, ObjektQuellbeleg>(StringComparer.Ordinal);
                GeoShopXtfLeser.LiesDurchlauf(stream, (tid, _) => fehlt.Contains(tid), o =>
                {
                    if (!neu.TryAdd(o.Tid, new() { System = "GeoShop-XTF", Datei = Path.GetFullPath(pfad),
                        Modell = o.Modell, Klasse = o.Klasse, Kennung = o.Tid, Werte = new(o.Werte),
                        Referenzen = new(o.Refs), Strukturen = new(o.Strukturen) }))
                        throw new InvalidDataException($"Originalkennung {o.Tid} kommt mehrfach in der gewählten GeoShop-XTF vor. Keine eindeutige Ergänzung möglich.");
                }, CancellationToken.None);
                if (neu.Count == 0) break;
                foreach (var (tid, q) in neu) { ergaenzt.Add(tid, q); vorhanden.Add(tid); }
                fehlt = Fehlende(alle.Concat(ergaenzt.Values), vorhanden);
            }
        }
        if (fehlt.Count > 0)
            throw new InvalidDataException($"Die gewählte GeoShop-XTF ergänzt den Objektverbund nicht vollständig. Fehlende Originalkennungen: {string.Join(", ", fehlt.Order())}. Keine unvollständige XTF geschrieben.");
        // Keine Defaults, Save-Aufrufe oder Importregeln auf dem Kundenprojekt ausführen.
        var kopie = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(projekt, JsonProjectRepository.SerializerOptions), JsonProjectRepository.SerializerOptions)!;
        kopie.Objektakten[0].Quellen.AddRange(ergaenzt.Values);
        return kopie;
    }

    private static HashSet<string> Fehlende(IEnumerable<ObjektQuellbeleg> quellen, HashSet<string> vorhanden) =>
        quellen.SelectMany(q => q.Referenzen).Where(r => !Organisationsrollen.Contains(r.Key) && !vorhanden.Contains(r.Value))
            .Select(r => r.Value).ToHashSet(StringComparer.Ordinal);
}

internal sealed class XtfQuelleFehltException(string message) : InvalidOperationException(message);
