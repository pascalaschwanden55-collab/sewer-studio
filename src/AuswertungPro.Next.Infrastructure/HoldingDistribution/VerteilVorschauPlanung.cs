using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using static AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Schreibfreie Vorschau der Haltungs- und Schachtverteilung (Fenster «Verteilen»).
///
/// Sie verwendet dieselben Bausteine wie das Verteilen: Seitenlesen, Aufteilen von
/// Sammelberichten, <see cref="HoldingVideoSearch"/>, den Verzeichnisbaum und die Suche nach
/// einer inhaltsgleichen Datei im Ziel. Anders als das Verteilen legt sie nichts an und
/// startet keine Texterkennung: Ein Scan ohne Textebene heisst «wird beim Verteilen geprüft».
/// Bewusst ausserhalb von <see cref="HoldingFolderDistributor"/>, der nicht weiter wachsen darf.
/// </summary>
internal static class VerteilVorschauPlanung
{
    /// <summary>Platzhalter, wenn der Hauptordner erst beim Verteilen abgefragt wird.</summary>
    private const string VorschauPlatzhalterWurzel = "Hauptordner";

    internal sealed record VorschauRahmen(
        string? ZielWurzel,
        DistributionTargetConfig? Baum,
        DistributionVariant Ablage,
        Project? Projekt)
    {
        internal string Wurzel => string.IsNullOrWhiteSpace(ZielWurzel) ? VorschauPlatzhalterWurzel : ZielWurzel!;

        /// <summary>Zielordner relativ zum Hauptordner, so wie ihn das Fenster zeigt.</summary>
        internal string Anzeige(string ordner)
        {
            var relativ = Path.GetRelativePath(Wurzel, ordner);
            return relativ == "." ? "(Hauptordner)" : relativ;
        }
    }

    // ─── Haltungen (PDF) ───────────────────────────────────────────────────

    internal static IReadOnlyList<VerteilVorschauZeile> VorschauHaltungenPdf(
        IReadOnlyList<string> pdfFiles,
        string? xtfSourceFolder,
        string? videoSourceFolder,
        VorschauRahmen rahmen,
        Action<string> gelesen,
        CancellationToken abbruch)
    {
        var zeilen = new List<VerteilVorschauZeile>();
        var mitFilmen = !string.IsNullOrWhiteSpace(videoSourceFolder);
        var videoFiles = mitFilmen ? EnumerateVideoFiles(videoSourceFolder!, recursive: true) : Array.Empty<string>();
        var sidecarLinks = SicherLesen(() => BuildSidecarVideoLinkIndex(xtfSourceFolder, pdfFiles));
        var cdIndex = SicherLesen(() => BuildCdIndexVideoLinkIndex(xtfSourceFolder, pdfFiles));
        var search = new HoldingVideoSearchContext(
            videoSourceFolder ?? "",
            Recursive: true,
            videoFiles,
            sidecarLinks,
            sidecarLinks is null ? null : BuildSidecarHoldingByVideoIndex(sidecarLinks),
            cdIndex);

        var geplanteHaltungen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ohneHaltung = new List<string>();

        foreach (var pdfPath in pdfFiles)
        {
            abbruch.ThrowIfCancellationRequested();
            var name = Path.GetFileName(pdfPath);
            try
            {
                var pages = DistributionPdfAssignmentController.ReadPages(pdfPath);
                if (pages.Count > 0 && pages.All(static p => string.IsNullOrWhiteSpace(p.Text)))
                {
                    zeilen.Add(Scan(name));
                    continue;
                }

                var bildseiten = pages.Any(static p => string.IsNullOrWhiteSpace(p.Text));
                // Ohne Texterkennung: die Vorschau liest nur die Textebene.
                var chunks = SplitPdfIntoHoldings(pages, static p => ParsePdfPage(p.Text, p.SourcePath));
                if (chunks.Count == 0)
                {
                    var parsed = ParsePdf(string.Join("\n\n", pages.Select(static p => p.Text)));
                    if (!parsed.Success)
                    {
                        ohneHaltung.Add(pdfPath);
                        continue;
                    }

                    zeilen.Add(PlaneHaltung(parsed, pdfPath, null, search, mitFilmen, rahmen, geplanteHaltungen, bildseiten));
                    continue;
                }

                if (chunks.Count == 1 && pages.Count == chunks[0].Pages.Count)
                {
                    zeilen.Add(PlaneHaltung(chunks[0].Parsed, pdfPath, null, search, mitFilmen, rahmen, geplanteHaltungen, bildseiten));
                    continue;
                }

                foreach (var chunk in chunks)
                {
                    abbruch.ThrowIfCancellationRequested();
                    if (!chunk.Parsed.Success)
                    {
                        zeilen.Add(Zeile($"{name} (S. {BuildPageRange(chunk.Pages)})", null, null, null,
                            VerteilVorschauStatus.NichtZugeordnet, "Haltung oder Datum auf diesen Seiten nicht erkannt."));
                        continue;
                    }

                    zeilen.Add(PlaneHaltung(chunk.Parsed, pdfPath, chunk.Pages, search, mitFilmen, rahmen, geplanteHaltungen, bildseiten));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                zeilen.Add(Zeile(name, null, null, null, VerteilVorschauStatus.NichtZugeordnet,
                    "PDF nicht lesbar: " + ex.Message));
            }
            finally
            {
                gelesen(pdfPath);
            }
        }

        // Wie beim Verteilen: Was keine Haltung nennt, geht per Dateiname in einen Haltungsordner
        // dieses Laufs (z. B. ein Begleitprotokoll) — sonst bleibt es liegen.
        foreach (var pdfPath in ohneHaltung)
        {
            var name = Path.GetFileName(pdfPath);
            var ordner = DistributionPdfAssignmentController.MatchPdfToHolding(pdfPath, geplanteHaltungen);
            if (ordner is null)
            {
                zeilen.Add(Zeile(name, null, null, null, VerteilVorschauStatus.NichtZugeordnet,
                    "Haltung und Datum nicht erkannt; kein passender Haltungsordner in diesem Lauf."));
                continue;
            }

            var vorhanden = FindExistingIdenticalFile(ordner, pdfPath, IstPdf) is not null;
            zeilen.Add(Zeile(name, Path.GetFileName(ordner), rahmen.Anzeige(ordner), null,
                vorhanden ? VerteilVorschauStatus.SchonVorhanden : VerteilVorschauStatus.Bereit,
                vorhanden ? "Begleit-PDF liegt bereits inhaltsgleich im Ziel." : "Begleit-PDF zur Haltung (per Dateiname zugeordnet)."));
        }

        if (!string.IsNullOrWhiteSpace(xtfSourceFolder) && Directory.Exists(xtfSourceFolder))
            zeilen.AddRange(VorschauQuelldateien(xtfSourceFolder!, rahmen));

        return zeilen;
    }

    private static VerteilVorschauZeile PlaneHaltung(
        ParsedPdf parsed,
        string pdfPath,
        IReadOnlyList<int>? seiten,
        HoldingVideoSearchContext search,
        bool mitFilmen,
        VorschauRahmen rahmen,
        Dictionary<string, string> geplanteHaltungen,
        bool bildseiten)
    {
        var protokoll = seiten is null
            ? Path.GetFileName(pdfPath)
            : $"{Path.GetFileName(pdfPath)} (S. {BuildPageRange(seiten)})";
        var parsedRaw = parsed.Haltung ?? "UNKNOWN";
        var holdingRaw = PdfCorrectionMetadata.ResolveHolding(rahmen.Projekt, parsedRaw);
        if (string.IsNullOrWhiteSpace(holdingRaw))
            holdingRaw = parsedRaw;
        var holding = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(holdingRaw));
        var original = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(parsedRaw));
        if (parsed.Date is null)
            return Zeile(protokoll, holding, null, null, VerteilVorschauStatus.NichtZugeordnet, "Datum nicht gefunden.");

        var date = parsed.Date.Value;
        var dateStamp = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        VideoFindResult? video = null;
        VideoFindResult? gegen = null;
        if (mitFilmen)
        {
            var suche = HoldingVideoSearch.Find(
                search, rahmen.Projekt, parsed.VideoFile, holdingRaw, holding, original, dateStamp, pdfPath);
            video = suche.Video;
            holding = suche.Holding;
            gegen = HoldingVideoSearch.FindCounterInspection(search, holding, dateStamp);
        }

        var ordner = DistributionDirectoryTreeController.ResolveObjectFolder(
            rahmen.Wurzel,
            rahmen.Baum,
            new DistributionPatternContext(Datum: date, Gemeinde: DistributionDirectoryTreeController.GetMunicipality(rahmen.Projekt), Haltung: holding),
            "{Haltung}",
            rahmen.Ablage,
            "{Datum}_{Haltung}");
        if (!string.IsNullOrWhiteSpace(parsed.Haltung))
            geplanteHaltungen[HoldingIdNormalizer.NormalizeHaltungId(parsed.Haltung)] = ordner;

        var hinweise = new List<string>();
        if (seiten is not null)
            hinweise.Add("Teil eines Sammelberichts.");
        if (bildseiten)
            hinweise.Add("Enthält Bildseiten; ihre Zuordnung wird beim Verteilen per Texterkennung geprüft.");

        // Wird der Haltungsname im PDF korrigiert, entsteht eine andere Datei — das laesst sich
        // ohne Schreiben nicht vergleichen.
        bool? pdfVorhanden = PdfTextLayerRewriter.CanRewrite(parsedRaw, holdingRaw)
            ? null
            : seiten is null
                ? FindExistingIdenticalFile(ordner, pdfPath, IstPdf) is not null
                : FindeGleichenInhalt(ordner, BuildPdfPagesBytes(pdfPath, seiten), ".pdf") is not null;
        if (pdfVorhanden is null)
            hinweise.Add("Haltungsname wird im PDF korrigiert; ob es schon im Ziel liegt, wird beim Verteilen geprüft.");

        string? film = null;
        VerteilVorschauStatus status;
        if (!mitFilmen)
        {
            status = pdfVorhanden == true ? VerteilVorschauStatus.SchonVorhanden : VerteilVorschauStatus.Bereit;
            film = "(kein Filmordner gewählt)";
        }
        else
        {
            var gefunden = new List<string>();
            var alleFilmeVorhanden = true;
            if (video!.Status == VideoMatchStatus.Matched && video.VideoPath is not null)
            {
                gefunden.Add(Path.GetFileName(video.VideoPath));
                alleFilmeVorhanden &= FindExistingVideo(ordner, video.VideoPath) is not null;
            }

            if (gegen!.Status == VideoMatchStatus.Matched
                && gegen.VideoPath is not null
                && !string.Equals(gegen.VideoPath, video.VideoPath, StringComparison.OrdinalIgnoreCase))
            {
                gefunden.Add(Path.GetFileName(gegen.VideoPath) + " (Gegeninspektion)");
                alleFilmeVorhanden &= FindExistingVideo(ordner, gegen.VideoPath) is not null;
            }

            if (gefunden.Count > 0)
            {
                film = string.Join(" + ", gefunden);
                status = pdfVorhanden == true && alleFilmeVorhanden
                    ? VerteilVorschauStatus.SchonVorhanden
                    : VerteilVorschauStatus.FilmGefunden;
            }
            else if (video.Status == VideoMatchStatus.Ambiguous || gegen.Status == VideoMatchStatus.Ambiguous)
            {
                var kandidaten = video.Status == VideoMatchStatus.Ambiguous ? video.Candidates : gegen.Candidates;
                film = $"{(kandidaten.Count > 1 ? kandidaten.Count : 2)} Filme – bitte prüfen";
                status = VerteilVorschauStatus.FilmMehrdeutig;
                hinweise.Add("Passende Filme: " + string.Join(", ", kandidaten.Select(Path.GetFileName)));
            }
            else
            {
                film = "kein Film gefunden";
                status = VerteilVorschauStatus.FilmFehlt;
                if (pdfVorhanden == true)
                    hinweise.Add("Das Protokoll liegt bereits inhaltsgleich im Ziel.");
            }
        }

        return Zeile(protokoll, holding, rahmen.Anzeige(ordner), film, status, string.Join(" ", hinweise));
    }

    private static IEnumerable<VerteilVorschauZeile> VorschauQuelldateien(string xtfSourceFolder, VorschauRahmen rahmen)
    {
        var suche = EnumerateSidecarFiles(xtfSourceFolder, null);
        foreach (var problem in suche.Probleme)
            yield return Zeile(xtfSourceFolder, null, null, null, VerteilVorschauStatus.NichtZugeordnet, problem);

        foreach (var quelle in suche.Dateien)
        {
            var vorhanden = FindExistingIdenticalFile(
                rahmen.Wurzel,
                quelle,
                pfad => string.Equals(Path.GetExtension(pfad), Path.GetExtension(quelle), StringComparison.OrdinalIgnoreCase))
                is not null;
            yield return Zeile(Path.GetFileName(quelle), null, "(Hauptordner)", null,
                vorhanden ? VerteilVorschauStatus.SchonVorhanden : VerteilVorschauStatus.Bereit,
                "Quelldatei (XTF/M150/MDB/XML) wird mitkopiert.");
        }
    }

    // ─── Haltungen (KINS-TXT) ──────────────────────────────────────────────

    internal static IReadOnlyList<VerteilVorschauZeile> VorschauHaltungenTxt(
        IReadOnlyList<string> txtFiles,
        string? videoSourceFolder,
        VorschauRahmen rahmen,
        Action<string> gelesen,
        CancellationToken abbruch)
    {
        var zeilen = new List<VerteilVorschauZeile>();
        var mitFilmen = !string.IsNullOrWhiteSpace(videoSourceFolder);
        var videoFiles = mitFilmen ? EnumerateVideoFiles(videoSourceFolder!, recursive: true) : Array.Empty<string>();
        foreach (var txtPath in txtFiles)
        {
            abbruch.ThrowIfCancellationRequested();
            var name = Path.GetFileName(txtPath);
            try
            {
                foreach (var section in ParseTxtSections(txtPath))
                {
                    abbruch.ThrowIfCancellationRequested();
                    var haltung = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(section.HoldingRaw));
                    var dateStamp = section.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    // Die TXT-Verteilung kennt keine Sanierungsebene; sie legt immer im Objektordner ab.
                    var ordner = DistributionDirectoryTreeController.ResolveObjectFolder(
                        rahmen.Wurzel,
                        rahmen.Baum,
                        new DistributionPatternContext(Datum: section.Date, Gemeinde: DistributionDirectoryTreeController.GetMunicipality(rahmen.Projekt), Haltung: haltung),
                        "{Haltung}");
                    var protokoll = $"{name} – {section.HoldingRaw}";
                    if (!mitFilmen)
                    {
                        zeilen.Add(Zeile(protokoll, haltung, rahmen.Anzeige(ordner), "(kein Filmordner gewählt)",
                            VerteilVorschauStatus.Bereit, ""));
                        continue;
                    }

                    var video = !string.IsNullOrWhiteSpace(section.VideoFileName)
                        ? FindVideo(section.VideoFileName, videoSourceFolder!, haltung, dateStamp, true, videoFiles)
                        : FindVideoByHaltungDate(videoSourceFolder!, haltung, dateStamp, true, videoFiles);
                    if (video.Status != VideoMatchStatus.Matched)
                    {
                        var ausLink = TryFindVideoFromRecordLink(rahmen.Projekt, haltung, videoSourceFolder!, dateStamp, true, videoFiles);
                        if (ausLink.Status == VideoMatchStatus.Matched)
                            video = ausLink;
                    }

                    zeilen.Add(video.Status switch
                    {
                        VideoMatchStatus.Matched => Zeile(protokoll, haltung, rahmen.Anzeige(ordner),
                            Path.GetFileName(video.VideoPath), VerteilVorschauStatus.FilmGefunden, ""),
                        VideoMatchStatus.Ambiguous => Zeile(protokoll, haltung, rahmen.Anzeige(ordner),
                            $"{Math.Max(2, video.Candidates.Count)} Filme – bitte prüfen", VerteilVorschauStatus.FilmMehrdeutig,
                            "Passende Filme: " + string.Join(", ", video.Candidates.Select(Path.GetFileName))),
                        _ => Zeile(protokoll, haltung, rahmen.Anzeige(ordner), "kein Film gefunden",
                            VerteilVorschauStatus.FilmFehlt, ""),
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                zeilen.Add(Zeile(name, null, null, null, VerteilVorschauStatus.NichtZugeordnet, "TXT nicht lesbar: " + ex.Message));
            }
            finally
            {
                gelesen(txtPath);
            }
        }

        return zeilen;
    }

    // ─── Schächte ──────────────────────────────────────────────────────────

    internal static IReadOnlyList<VerteilVorschauZeile> VorschauSchaechte(
        IReadOnlyList<string> pdfFiles,
        VorschauRahmen rahmen,
        Action<string> gelesen,
        CancellationToken abbruch)
    {
        var zeilen = new List<VerteilVorschauZeile>();
        var geplant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pdfPath in pdfFiles)
        {
            abbruch.ThrowIfCancellationRequested();
            var name = Path.GetFileName(pdfPath);
            try
            {
                var pages = DistributionPdfAssignmentController.ReadPages(pdfPath);
                if (pages.Count > 0 && pages.All(static p => string.IsNullOrWhiteSpace(p.Text)))
                {
                    zeilen.Add(Scan(name));
                    continue;
                }

                var chunks = SplitPdfIntoShafts(pages, out var verwaist, ParseSchachtSeiteOhneOcr);
                if (chunks.Count == 0)
                {
                    var parsed = ParseSchachtPdf(string.Join("\n\n", pages.Select(static p => p.Text)));
                    zeilen.Add(parsed.Success
                        ? PlaneSchacht(parsed, pdfPath, null, rahmen, geplant)
                        : Zeile(name, null, null, null, VerteilVorschauStatus.NichtZugeordnet,
                            string.IsNullOrWhiteSpace(parsed.Message) ? "Kein Schachtprotokoll erkannt." : parsed.Message!));
                    continue;
                }

                if (chunks.Count == 1 && pages.Count == chunks[0].Pages.Count)
                {
                    zeilen.Add(PlaneSchacht(chunks[0].Parsed, pdfPath, null, rahmen, geplant));
                    continue;
                }

                foreach (var chunk in chunks)
                {
                    abbruch.ThrowIfCancellationRequested();
                    zeilen.Add(chunk.Parsed.Success
                        ? PlaneSchacht(chunk.Parsed, pdfPath, chunk.Pages, rahmen, geplant)
                        : Zeile($"{name} (S. {BuildPageRange(chunk.Pages)})", null, null, null,
                            VerteilVorschauStatus.NichtZugeordnet, "Schacht oder Datum auf diesen Seiten nicht erkannt."));
                }

                foreach (var seiten in verwaist)
                {
                    zeilen.Add(Zeile($"{name} (S. {BuildPageRange(seiten.Pages)})", seiten.Parsed.ShaftNumber, null, null,
                        VerteilVorschauStatus.NichtZugeordnet,
                        $"Schachtseite ohne eigenen Abschnitt: {seiten.Parsed.Message}"));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                zeilen.Add(Zeile(name, null, null, null, VerteilVorschauStatus.NichtZugeordnet, "PDF nicht lesbar: " + ex.Message));
            }
            finally
            {
                gelesen(pdfPath);
            }
        }

        return zeilen;
    }

    /// <summary>Schachtseite wie beim Verteilen, aber ohne Texterkennung.</summary>
    private static ParsedShaftPdf ParseSchachtSeiteOhneOcr(DistributionPdfPage page)
    {
        var parsed = ParseSchachtPdfPage(page.Text);
        if (parsed.Success || string.IsNullOrWhiteSpace(page.SourcePath) || !File.Exists(page.SourcePath))
            return parsed;

        return TryCompleteShaftDateFromSiblingProtocol(page.SourcePath, parsed)
               ?? TryParseSchachtPdfPageFromFormFields(page.SourcePath, page.PageNumber)
               ?? parsed;
    }

    private static VerteilVorschauZeile PlaneSchacht(
        ParsedShaftPdf parsed,
        string pdfPath,
        IReadOnlyList<int>? seiten,
        VorschauRahmen rahmen,
        HashSet<string> geplant)
    {
        var protokoll = seiten is null
            ? Path.GetFileName(pdfPath)
            : $"{Path.GetFileName(pdfPath)} (S. {BuildPageRange(seiten)})";
        if (string.IsNullOrWhiteSpace(parsed.ShaftNumber))
            return Zeile(protokoll, null, null, null, VerteilVorschauStatus.NichtZugeordnet, "Schachtnummer nicht gefunden.");
        if (parsed.Date is null)
            return Zeile(protokoll, parsed.ShaftNumber, null, null, VerteilVorschauStatus.NichtZugeordnet, "Datum nicht gefunden.");

        var parsedRaw = parsed.ShaftNumber.Trim();
        var schachtRaw = PdfCorrectionMetadata.ResolveShaft(rahmen.Projekt, parsedRaw);
        if (string.IsNullOrWhiteSpace(schachtRaw))
            schachtRaw = parsedRaw;
        var schacht = ProjectPathResolver.SanitizePathSegment(schachtRaw);
        var dateStamp = parsed.Date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var ordner = DistributionDirectoryTreeController.ResolveObjectFolder(
            rahmen.Wurzel,
            rahmen.Baum,
            new DistributionPatternContext(Datum: parsed.Date.Value, Gemeinde: DistributionDirectoryTreeController.GetMunicipality(rahmen.Projekt), Schachtnummer: schacht),
            "{Schachtnummer}",
            rahmen.Ablage,
            "{Datum}_{Schachtnummer}");

        if (!geplant.Add($"{dateStamp}|{schacht}"))
        {
            return Zeile(protokoll, schacht, rahmen.Anzeige(ordner), null, VerteilVorschauStatus.Bereit,
                "Wird an das Protokoll desselben Schachts angehängt.");
        }

        if (PdfTextLayerRewriter.CanRewrite(parsedRaw, schachtRaw))
        {
            return Zeile(protokoll, schacht, rahmen.Anzeige(ordner), null, VerteilVorschauStatus.Bereit,
                "Schachtnummer wird im PDF korrigiert; ob es schon im Ziel liegt, wird beim Verteilen geprüft.");
        }

        var vorhanden = seiten is null
            ? FindExistingIdenticalFile(ordner, pdfPath, IstPdf) is not null
            : FindeGleichenInhalt(ordner, BuildPdfPagesBytes(pdfPath, seiten), ".pdf") is not null;
        return Zeile(protokoll, schacht, rahmen.Anzeige(ordner), null,
            vorhanden ? VerteilVorschauStatus.SchonVorhanden : VerteilVorschauStatus.Bereit,
            vorhanden ? "Liegt bereits inhaltsgleich im Ziel." : seiten is null ? "" : "Teil eines Sammelberichts.");
    }

    // ─── Gemeinsame Helfer ─────────────────────────────────────────────────

    /// <summary>
    /// Wie <see cref="FindExistingIdenticalFile"/>, aber gegen einen Inhalt im Speicher —
    /// fuer Auszuege aus Sammelberichten, die die Vorschau nicht als Datei ablegen darf.
    /// </summary>
    internal static string? FindeGleichenInhalt(string ordner, byte[] inhalt, string endung)
    {
        if (!ImportSourcePathGuard.TryInspectDirectory(ordner, out var sichererOrdner, out var existiert, out _)
            || !existiert)
        {
            return null;
        }

        try
        {
            foreach (var kandidat in Directory.EnumerateFiles(sichererOrdner))
            {
                if (!string.Equals(Path.GetExtension(kandidat), endung, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!ImportSourcePathGuard.TryInspectFile(kandidat, out var sicher, out var da, out _) || !da)
                    continue;
                if (new FileInfo(sicher).Length != inhalt.Length)
                    continue;
                if (File.ReadAllBytes(sicher).AsSpan().SequenceEqual(inhalt))
                    return sicher;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ein unlesbarer Ordner ist kein Beleg fuer «schon vorhanden».
        }

        return null;
    }

    internal static VerteilVorschauZeile Zeile(
        string protokoll, string? objekt, string? ziel, string? film, VerteilVorschauStatus status, string hinweis)
        => new(protokoll, objekt, ziel, film, status, hinweis ?? "");

    internal static VerteilVorschauZeile Scan(string name)
        => Zeile(name, null, null, null, VerteilVorschauStatus.WirdGeprueft,
            "Scan ohne Textebene – wird beim Verteilen per Texterkennung gelesen.");

    private static bool IstPdf(string pfad)
        => string.Equals(Path.GetExtension(pfad), ".pdf", StringComparison.OrdinalIgnoreCase);

    private static T? SicherLesen<T>(Func<T> lesen) where T : class
    {
        try
        {
            return lesen();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Verteilvorschau] Hilfsindex nicht lesbar: {ex.Message}");
            return null;
        }
    }
}
