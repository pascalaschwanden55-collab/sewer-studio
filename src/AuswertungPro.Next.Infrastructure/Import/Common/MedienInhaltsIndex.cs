using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Bestimmt, welche Dateien denselben Inhalt haben — sparsam und wiederholbar.
///
/// Zwei Regeln halten die Kosten klein:
///
/// 1. <b>Groesse zuerst.</b> Dateien verschiedener Groesse koennen nicht gleich sein.
///    Nur bei gleicher Groesse wird wirklich gelesen. In einem Ordner mit 46 Videos
///    heisst das in der Regel: null Lesevorgaenge.
/// 2. <b>Einmal je Lauf.</b> Ein Zwischenspeicher haelt Groesse, Zeitstempel und Pruefsumme
///    fest. Ohne ihn wuerde bei 239 Haltungen derselbe Film hunderte Male gehasht.
///
/// Aendert sich eine Datei waehrend des Laufs (Groesse oder Zeitstempel weichen vom
/// gemerkten Stand ab), gilt sie als Befund — nicht als Kopie. Eine Gleichheit, die
/// waehrend des Vergleichs zerfaellt, ist keine Gleichheit.
///
/// Die Instanz gehoert zu EINEM Importlauf und ist nicht threadsicher.
/// </summary>
public sealed class MedienInhaltsIndex : IMedienInhaltsIndex
{
    private sealed record Stand(long Groesse, DateTime GeaendertUtc, string? Pruefsumme, string? Fehler);

    private readonly Dictionary<string, Stand> _bekannt = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<MedienKandidat> Pruefe(IReadOnlyList<string> pfade)
    {
        ArgumentNullException.ThrowIfNull(pfade);

        var staende = new List<(string Pfad, Stand Stand)>();
        foreach (var pfad in pfade.Distinct(StringComparer.OrdinalIgnoreCase))
            staende.Add((pfad, LiesStand(pfad)));

        // Nur Dateien gleicher Groesse koennen denselben Inhalt haben. Alle anderen
        // bekommen einen Schluessel, der sie sicher voneinander trennt.
        var gleicheGroesse = staende
            .Where(s => s.Stand.Fehler is null)
            .GroupBy(s => s.Stand.Groesse)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(s => s.Pfad))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ergebnis = new List<MedienKandidat>(staende.Count);
        foreach (var (pfad, stand) in staende)
        {
            if (stand.Fehler is not null)
            {
                ergebnis.Add(new MedienKandidat(pfad, null, stand.Fehler));
                continue;
            }

            if (!gleicheGroesse.Contains(pfad))
            {
                ergebnis.Add(new MedienKandidat(
                    pfad,
                    "g:" + stand.Groesse.ToString(CultureInfo.InvariantCulture) + ":" + pfad.ToUpperInvariant()));
                continue;
            }

            var (pruefsumme, fehler) = LiesPruefsumme(pfad, stand);
            ergebnis.Add(fehler is null
                ? new MedienKandidat(pfad, "h:" + pruefsumme)
                : new MedienKandidat(pfad, null, fehler));
        }

        return ergebnis;
    }

    private Stand LiesStand(string pfad)
    {
        if (_bekannt.TryGetValue(pfad, out var gemerkt))
        {
            var jetzt = Messe(pfad);
            if (jetzt.Fehler is not null)
                return jetzt;

            // Waehrend des Laufs veraendert: kein Vergleich mehr moeglich.
            if (jetzt.Groesse != gemerkt.Groesse || jetzt.GeaendertUtc != gemerkt.GeaendertUtc)
            {
                var geaendert = gemerkt with
                {
                    Pruefsumme = null,
                    Fehler = "Datei hat sich waehrend des Imports geaendert"
                };
                _bekannt[pfad] = geaendert;
                return geaendert;
            }

            return gemerkt;
        }

        var neu = Messe(pfad);
        _bekannt[pfad] = neu;
        return neu;
    }

    private static Stand Messe(string pfad)
    {
        try
        {
            var info = new FileInfo(pfad);
            if (!info.Exists)
                return new Stand(0, default, null, "Datei nicht vorhanden");

            return new Stand(info.Length, info.LastWriteTimeUtc, null, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return new Stand(0, default, null, ex.Message);
        }
    }

    private (string? Pruefsumme, string? Fehler) LiesPruefsumme(string pfad, Stand stand)
    {
        if (stand.Pruefsumme is not null)
            return (stand.Pruefsumme, null);

        try
        {
            using var strom = File.OpenRead(pfad);
            var hash = Convert.ToHexString(SHA256.HashData(strom));

            // Nach dem Lesen erneut messen: Ein Film, der waehrend des Hashens noch
            // geschrieben wurde, darf nicht als Kopie durchgehen.
            var nachher = Messe(pfad);
            if (nachher.Fehler is not null)
                return (null, nachher.Fehler);
            if (nachher.Groesse != stand.Groesse || nachher.GeaendertUtc != stand.GeaendertUtc)
                return (null, "Datei hat sich waehrend des Imports geaendert");

            _bekannt[pfad] = stand with { Pruefsumme = hash };
            return (hash, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return (null, ex.Message);
        }
    }
}
