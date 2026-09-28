using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Schreibfreie Vorschau der Dichtheitsverteilung. Dieselbe Reihenfolge wie
/// <see cref="DichtheitDistributionController"/> (Behälterprüfung, seitenweise Haltungen,
/// Rückfallkette), aber ohne Texterkennung und ohne Katasterabgleich: ob eine Haltung im
/// amtlichen Kataster fehlt (Ablage unter «keine_Zuordnung»), zeigt erst das Verteilen.
/// </summary>
internal static class DichtheitVorschau
{
    internal static IReadOnlyList<VerteilVorschauZeile> Plane(
        IReadOnlyList<string> pdfFiles,
        VerteilVorschauPlanung.VorschauRahmen rahmen,
        Action<string> gelesen,
        CancellationToken abbruch)
    {
        var zeilen = new List<VerteilVorschauZeile>();
        foreach (var pdfPath in pdfFiles)
        {
            abbruch.ThrowIfCancellationRequested();
            var name = Path.GetFileName(pdfPath);
            try
            {
                var pages = DistributionPdfAssignmentController.ReadPages(pdfPath);
                if (pages.Count > 0 && pages.All(static p => string.IsNullOrWhiteSpace(p.Text)))
                {
                    zeilen.Add(VerteilVorschauPlanung.Scan(name));
                    continue;
                }

                var text = string.Join("\n\n", pages.Select(static p => p.Text));
                var behaelter = BehaelterPruefungParser.Erkenne(text);
                if (behaelter is not null)
                {
                    zeilen.Add(Behaelter(name, pdfPath, behaelter, rahmen));
                    continue;
                }

                var seiten = DistributionPdfAssignmentController.ExtractDichtheitPerPage(
                    pages, rahmen.Projekt, rahmen.Wurzel, cadastre: null);
                var verschiedene = seiten
                    .Where(static s => !string.IsNullOrWhiteSpace(s.HaltungId))
                    .Select(static s => s.HaltungId!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                if (verschiedene > 1)
                {
                    foreach (var seite in seiten)
                    {
                        abbruch.ThrowIfCancellationRequested();
                        zeilen.Add(Seite(name, pdfPath, seite, rahmen));
                    }

                    continue;
                }

                zeilen.Add(Ganz(name, pdfPath, text, seiten, rahmen));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                zeilen.Add(VerteilVorschauPlanung.Zeile(name, null, null, null, VerteilVorschauStatus.NichtZugeordnet,
                    "PDF nicht lesbar: " + ex.Message));
            }
            finally
            {
                gelesen(pdfPath);
            }
        }

        return zeilen;
    }

    private static VerteilVorschauZeile Seite(
        string name, string pdfPath, DichtheitPageAssignment seite, VerteilVorschauPlanung.VorschauRahmen rahmen)
    {
        var protokoll = $"{name} (S. {seite.MainPage})";
        if (string.IsNullOrWhiteSpace(seite.HaltungId))
            return VerteilVorschauPlanung.Zeile(protokoll, null, null, null, VerteilVorschauStatus.NichtZugeordnet, "Haltung nicht erkannt.");

        var haltung = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(seite.HaltungId!));
        DateTime? datum = DateTime.TryParseExact(seite.DateStamp, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;
        var ordner = Ordner(rahmen, haltung, datum);
        var vorhanden = VerteilVorschauPlanung.FindeGleichenInhalt(
            ordner, Distributor.BuildPdfPagesBytes(pdfPath, seite.PageNumbers), ".pdf") is not null;
        return VerteilVorschauPlanung.Zeile(protokoll, haltung, rahmen.Anzeige(ordner), null,
            vorhanden ? VerteilVorschauStatus.SchonVorhanden : VerteilVorschauStatus.Bereit,
            vorhanden ? "Liegt bereits inhaltsgleich im Ziel." : $"{seite.PageNumbers.Count} Seite(n) aus Sammelbericht.");
    }

    private static VerteilVorschauZeile Ganz(
        string name,
        string pdfPath,
        string text,
        IReadOnlyList<DichtheitPageAssignment> seiten,
        VerteilVorschauPlanung.VorschauRahmen rahmen)
    {
        var haltungId = seiten.Count == 1 ? seiten[0].HaltungId : null;
        if (string.IsNullOrWhiteSpace(haltungId))
        {
            var (a, b) = DichtheitShaftParser.TryExtractShafts(text);
            if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b))
                haltungId = DistributionPdfAssignmentController.ResolveHoldingOrder(a, b, rahmen.Projekt, rahmen.Wurzel);
        }

        if (string.IsNullOrWhiteSpace(haltungId))
        {
            var parsed = Distributor.ParsePdf(text);
            if (parsed.Success && !string.IsNullOrWhiteSpace(parsed.Haltung))
                haltungId = parsed.Haltung;
        }

        if (string.IsNullOrWhiteSpace(haltungId))
            haltungId = ShaftCandidateScanner.TryExtractFromShafts(text);

        if (string.IsNullOrWhiteSpace(haltungId))
        {
            return VerteilVorschauPlanung.Zeile(name, null, null, null, VerteilVorschauStatus.NichtZugeordnet,
                "Haltung nicht erkannt (oberer/unterer Schacht nicht gefunden). Beim Verteilen folgen noch Texterkennung und Katasterabgleich.");
        }

        var datum = HoldingTextParser.TryFindInspectionDate(text);
        if (datum is null
            && rahmen.Baum is not null
            && seiten.Count > 0
            && DateTime.TryParseExact(seiten[0].DateStamp, "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var seitenDatum))
        {
            datum = seitenDatum;
        }

        var haltung = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(haltungId!));
        var ordner = Ordner(rahmen, haltung, datum);
        var vorhanden = Distributor.FindExistingIdenticalFile(
            ordner, pdfPath, static p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase)) is not null;
        return VerteilVorschauPlanung.Zeile(name, haltung, rahmen.Anzeige(ordner), null,
            vorhanden ? VerteilVorschauStatus.SchonVorhanden : VerteilVorschauStatus.Bereit,
            vorhanden ? "Liegt bereits inhaltsgleich im Ziel." : "");
    }

    private static VerteilVorschauZeile Behaelter(
        string name, string pdfPath, BehaelterPruefung behaelter, VerteilVorschauPlanung.VorschauRahmen rahmen)
    {
        if (string.IsNullOrWhiteSpace(behaelter.Objekt))
        {
            return VerteilVorschauPlanung.Zeile(name, null, null, null, VerteilVorschauStatus.NichtZugeordnet,
                "Behälterprüfung erkannt, aber kein Prüfobjekt lesbar – wird nicht verteilt.");
        }

        var objekt = ProjectPathResolver.SanitizePathSegment(behaelter.Objekt!);
        var ordner = Ordner(rahmen, objekt, behaelter.Datum);
        var vorhanden = Distributor.FindExistingIdenticalFile(
            ordner, pdfPath, static p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase)) is not null;
        return VerteilVorschauPlanung.Zeile(name, objekt, rahmen.Anzeige(ordner), null,
            vorhanden ? VerteilVorschauStatus.SchonVorhanden : VerteilVorschauStatus.Bereit,
            vorhanden ? "Liegt bereits inhaltsgleich im Ziel." : "Behälterprüfung (eigener Bauwerksordner).");
    }

    // Dichtheit kennt keine Sanierungsebene: dieselbe Aufloesung wie im Verteiler.
    private static string Ordner(VerteilVorschauPlanung.VorschauRahmen rahmen, string haltung, DateTime? datum)
        => DistributionDirectoryTreeController.ResolveObjectFolder(
            rahmen.Wurzel,
            rahmen.Baum,
            new DistributionPatternContext(
                Datum: datum,
                Gemeinde: DistributionDirectoryTreeController.GetMunicipality(rahmen.Projekt),
                Haltung: haltung),
            "{Haltung}");
}
