using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>
/// Sucht im PDF-Dateinamen nach einem Namen, den das Projekt bereits kennt.
///
/// Hintergrund: <see cref="ProtocolNameResolver"/> akzeptiert nur Dateinamen, die
/// selbst eine reine Haltungs-/Schachtnummer sind. Herstellerexporte stellen aber
/// regelmaessig etwas voran ("Section_8_892037-74091.pdf"). Diese Dateien fielen
/// vorher stillschweigend heraus — im Projekt Hellgasse alle 38 Haltungsprotokolle.
///
/// Der Dienst ist bewusst streng: Ein Treffer entsteht nur gegen einen bereits
/// importierten Namen, er muss an einer Ziffern-Grenze stehen, und bei zwei
/// verschiedenen Treffern gibt es KEINE Zuordnung.
/// </summary>
public sealed class ImportPdfReferenceResolver : IImportPdfReferenceResolver
{
    public ImportPdfReference? Resolve(
        string fileName,
        IReadOnlyCollection<string> haltungsnamen,
        IReadOnlyCollection<string> schachtnummern)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var name = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // Haltungen zuerst: Eine Haltungsnummer enthaelt ihre Schachtnummern, der
        // laengere und damit spezifischere Treffer muss gewinnen.
        var haltung = FindeEindeutigenTreffer(name, haltungsnamen, ErzeugeHaltungsvarianten);
        if (haltung is not null)
            return new ImportPdfReference(ImportPdfReferenceKind.Haltung, haltung);

        var schacht = FindeEindeutigenTreffer(name, schachtnummern, kandidat => new[] { kandidat });
        if (schacht is not null)
            return new ImportPdfReference(ImportPdfReferenceKind.Schacht, schacht);

        return null;
    }

    /// <summary>
    /// Liefert den einzigen bekannten Namen, der im Dateinamen vorkommt.
    /// Mehrere VERSCHIEDENE Namen ergeben bewusst null: ein Sammel-PDF darf nicht
    /// willkuerlich einer der genannten Haltungen zugeschlagen werden.
    /// </summary>
    private static string? FindeEindeutigenTreffer(
        string dateiname,
        IReadOnlyCollection<string> bekannteNamen,
        Func<string, IEnumerable<string>> variantenBilder)
    {
        var treffer = new List<string>();

        foreach (var bekannt in bekannteNamen)
        {
            if (string.IsNullOrWhiteSpace(bekannt))
                continue;

            if (variantenBilder(bekannt).Any(variante => KommtVor(dateiname, variante)))
                treffer.Add(bekannt);
        }

        if (treffer.Count == 0)
            return null;

        // Enthaelt ein Treffer alle anderen, ist er der spezifischere (z.B. die
        // Haltung gegen einen ihrer Schaechte). Sonst bleibt es mehrdeutig.
        var laengster = treffer.OrderByDescending(t => t.Length).First();
        var alleEnthalten = treffer.All(t =>
            laengster.Contains(t, StringComparison.OrdinalIgnoreCase));

        return alleEnthalten ? laengster : null;
    }

    /// <summary>
    /// Eine Haltung darf im Dateinamen auch mit vertauschten Schaechten stehen
    /// (A-B statt B-A) — dieselbe Toleranz hat der name-basierte Verteiler.
    /// </summary>
    private static IEnumerable<string> ErzeugeHaltungsvarianten(string haltung)
    {
        yield return haltung;

        var teile = haltung.Split('-');
        if (teile.Length == 2)
            yield return teile[1] + "-" + teile[0];
    }

    /// <summary>
    /// Teilstring-Suche mit Ziffern-Grenze: "74091" darf nicht in "174091" treffen,
    /// sonst haengt ein fremdes Protokoll am falschen Schacht.
    /// </summary>
    private static bool KommtVor(string dateiname, string gesucht)
    {
        if (string.IsNullOrEmpty(gesucht))
            return false;

        var start = 0;
        while (start <= dateiname.Length - gesucht.Length)
        {
            var index = dateiname.IndexOf(gesucht, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;

            var davor = index == 0 ? (char?)null : dateiname[index - 1];
            var ende = index + gesucht.Length;
            var danach = ende >= dateiname.Length ? (char?)null : dateiname[ende];

            if (!IstZifferGrenzeVerletzt(davor) && !IstZifferGrenzeVerletzt(danach))
                return true;

            start = index + 1;
        }

        return false;
    }

    private static bool IstZifferGrenzeVerletzt(char? zeichen)
        => zeichen is not null && char.IsDigit(zeichen.Value);
}
