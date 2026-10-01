using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.Infrastructure.Import;
using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Schreibfreie Vorschau fuer das Fenster «Verteilen». Waehlt die Quelldateien genau wie
/// der jeweilige Verteilweg aus und plant jede Datei mit dessen Bausteinen.
/// </summary>
public sealed class VerteilVorschauService : IVerteilVorschau
{
    public VerteilVorschauErgebnis Plane(
        VerteilVorschauAnfrage anfrage,
        IProgress<VerteilVorschauFortschritt>? fortschritt,
        CancellationToken abbruch)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        if (!anfrage.Quelle.IstGewaehlt)
            return VerteilVorschauErgebnis.Leer;

        var zeilen = new List<VerteilVorschauZeile>();
        var hinweise = new List<string>();
        var rahmen = new VerteilVorschauPlanung.VorschauRahmen(anfrage.Zielordner, anfrage.Baum, anfrage.Ablage, anfrage.Projekt);
        if (string.IsNullOrWhiteSpace(anfrage.Zielordner))
            hinweise.Add("Kein gespeichertes Projekt: Der Hauptordner wird beim Verteilen abgefragt; «schon vorhanden» ist deshalb noch nicht prüfbar.");

        var dateien = WaehleDateien(anfrage, zeilen);
        var gesamt = dateien.Count;
        var erledigt = 0;
        fortschritt?.Report(new VerteilVorschauFortschritt(0, gesamt, null));
        void Gelesen(string pfad)
            => fortschritt?.Report(new VerteilVorschauFortschritt(++erledigt, gesamt, pfad));

        switch (anfrage.Art)
        {
            case VerteilArt.Haltungen when anfrage.Quelle.IstTxt:
                zeilen.AddRange(VerteilVorschauPlanung.VorschauHaltungenTxt(dateien, anfrage.FilmOrdner, rahmen, Gelesen, abbruch));
                break;
            case VerteilArt.Haltungen:
                var xtfOrdner = anfrage.Quelle.Ordner
                                ?? (dateien.Count > 0 ? Path.GetDirectoryName(dateien[0]) : null);
                zeilen.AddRange(VerteilVorschauPlanung.VorschauHaltungenPdf(
                    dateien, xtfOrdner, anfrage.FilmOrdner, rahmen, Gelesen, abbruch));
                if (string.IsNullOrWhiteSpace(anfrage.FilmOrdner))
                    hinweise.Add("Noch kein Filmordner gewählt: Die Filme werden erst nach der Wahl gesucht.");
                break;
            case VerteilArt.Schaechte:
                zeilen.AddRange(VerteilVorschauPlanung.VorschauSchaechte(dateien, rahmen, Gelesen, abbruch));
                break;
            case VerteilArt.Dichtheit:
                zeilen.AddRange(DichtheitVorschau.Plane(dateien, rahmen, Gelesen, abbruch));
                hinweise.Add("Der Abgleich mit dem amtlichen Kataster (Ablage unter «keine_Zuordnung») wird beim Verteilen geprüft.");
                break;
        }

        var quelldateien = zeilen.Count(static z => z.Hinweis.StartsWith("Quelldatei", StringComparison.Ordinal));
        return new VerteilVorschauErgebnis(gesamt + quelldateien, zeilen, hinweise);
    }

    /// <summary>
    /// Dieselbe Dateiauswahl wie beim Verteilen. Abgelehnte Einzeldateien und uebersprungene
    /// fremde PDFs kommen als eigene Zeile in die Vorschau, statt still zu fehlen.
    /// </summary>
    private static List<string> WaehleDateien(VerteilVorschauAnfrage anfrage, List<VerteilVorschauZeile> zeilen)
    {
        var quelle = anfrage.Quelle;
        if (quelle.IstTxt)
        {
            if (!quelle.IstEinzeldateien)
            {
                return Directory.Exists(quelle.Ordner)
                    ? Common.SafeFileEnumeration.EnumerateFilesSafe(quelle.Ordner!, "kiDVDaten*.txt", recursive: true).ToList()
                    : Fehlt(zeilen, quelle.Ordner!);
            }

            return quelle.Dateien
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Where(File.Exists)
                .Where(p => string.Equals(Path.GetExtension(p), ".txt", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        List<string> kandidaten;
        if (quelle.IstEinzeldateien)
        {
            kandidaten = quelle.Dateien.ToList();
        }
        else if (!Directory.Exists(quelle.Ordner))
        {
            return Fehlt(zeilen, quelle.Ordner!);
        }
        else
        {
            kandidaten = Common.SafeFileEnumeration.EnumerateFilesSafe(quelle.Ordner!, "*.pdf", recursive: true).ToList();
            if (anfrage.Art != VerteilArt.Schaechte)
                kandidaten = kandidaten.Where(static p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (anfrage.Art == VerteilArt.Schaechte)
        {
            // Wie ShaftDistributionService: eindeutig fremde PDFs fallen vorher heraus — hier ohne Texterkennung.
            var fremd = kandidaten.Where(p => File.Exists(p) && !ShaftPdfRelevance.ShouldProcess(p, static (_, _) => null)).ToList();
            foreach (var pfad in fremd)
            {
                zeilen.Add(VerteilVorschauPlanung.Zeile(Path.GetFileName(pfad), null, null, null,
                    VerteilVorschauStatus.NichtZugeordnet, "Kein Schachtprotokoll – wird übersprungen."));
            }

            kandidaten = kandidaten.Except(fremd, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var auswahl = DistributionPdfSelection.Pruefe(kandidaten, ohneSplitTeile: anfrage.Art != VerteilArt.Dichtheit);
        foreach (var abgelehnt in auswahl.Abgelehnt)
        {
            zeilen.Add(VerteilVorschauPlanung.Zeile(Path.GetFileName(abgelehnt.SourcePdfPath), null, null, null,
                VerteilVorschauStatus.NichtZugeordnet, abgelehnt.Message));
        }

        return anfrage.Art == VerteilArt.Schaechte
            ? ShaftPdfSelectionExpander.Expand(auswahl.Gueltig)
            : auswahl.Gueltig;
    }

    private static List<string> Fehlt(List<VerteilVorschauZeile> zeilen, string ordner)
    {
        zeilen.Add(VerteilVorschauPlanung.Zeile(ordner, null, null, null, VerteilVorschauStatus.NichtZugeordnet,
            "Quellordner nicht gefunden."));
        return [];
    }
}
