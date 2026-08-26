using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DocumentFormat.OpenXml.Packaging;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Erzeugt die Word-Datei eines Eigentuemerdossiers aus der Vorlage
/// "Export_Vorlage\Eigentuemerdossier.docx".
///
/// Eine bereits vorhandene Datei wird nie ueberschrieben — Pascal ergaenzt in
/// dieser Datei von Hand, ein stilles Ueberschreiben wuerde seine Arbeit
/// vernichten. Stattdessen entsteht ein freier Name.
/// </summary>
public sealed class DossierWordTemplateExportService : IDossierWordExportService
{
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    /// <summary>Das feste Firmenlogo, ausgeliefert neben der Word-Vorlage.</summary>
    public const string LogoFileName = "Dossier_Logo.png";

    /// <summary>Das feste Wappen, ausgeliefert neben der Word-Vorlage.</summary>
    public const string CoatOfArmsFileName = "Dossier_Wappen.png";

    /// <summary>Breite des Uebersichtsplans im Dokument.</summary>
    public const double PlanMaxWidthCm = 15.0;

    /// <summary>
    /// Hoehe/Breite der Planflaeche aus dem Referenzdossier. Word passt das
    /// Planbild genau in diese Flaeche ein; das verhindert eine Zusatzseite.
    /// </summary>
    private const double PlanTemplateHeightToWidth = 7_741_920d / 5_402_580d;

    /// <summary>
    /// Rechnet eine Breite in die feste Hoehe der Word-Vorlagenflaeche um.
    /// Die Einheit bleibt gleich (cm zu cm, Pixel zu Pixel).
    /// </summary>
    public static double PlanHeightForWidth(double width)
        => Math.Max(0, width) * PlanTemplateHeightToWidth;

    /// <summary>
    /// Der Text, den eine Wiederholzeile ohne Daten traegt. Er steht hier, weil
    /// Export UND Vorschau denselben verwenden muessen — sonst zeigt die
    /// Vorschau etwas anderes als das fertige Dossier.
    /// </summary>
    public static string EmptyRowText(string repeatKey) => repeatKey switch
    {
        "Haltungen" => "Keine Leitungen zugeordnet",
        // Diese beiden Tabellen sind Ausfuellbereiche. Eine leere Datenzeile
        // bleibt professioneller und vor allem direkt beschreibbar; ein
        // erzeugter Hinweis waere sichtbarer Text ohne eigenes Fachfeld.
        "Eigentuemer" => string.Empty,
        "Themen" => string.Empty,
        _ => string.Empty
    };

    private readonly Func<string> _resolveTemplatePath;

    public DossierWordTemplateExportService(Func<string>? resolveTemplatePath = null)
        => _resolveTemplatePath = resolveTemplatePath ?? DefaultTemplatePath;

    /// <summary>Standardpfad der ausgelieferten Vorlage.</summary>
    public static string DefaultTemplatePath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Export_Vorlage",
            DossierWordTemplate.TemplateFileName);

    public Task<DossierWordExportResult> ExportAsync(
        DossierExportRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var templatePath = _resolveTemplatePath();
        if (!File.Exists(templatePath))
        {
            return Task.FromResult(new DossierWordExportResult(
                false,
                null,
                $"Die Word-Vorlage fehlt: '{templatePath}'. "
                + "Sie lässt sich in den Einstellungen des Dossier-Bereichs neu erzeugen."));
        }

        try
        {
            var guard = new ProjectWritePathGuard(request.ProjectRoot);
            var folder = guard.EnsureSafeDirectoryTarget(request.TargetFolder);
            Directory.CreateDirectory(folder);

            var fileName = DossierFolderPlanner.PlanFreeFileName(
                DossierFolderPlanner.WordFileName,
                candidate => File.Exists(Path.Combine(folder, candidate)));

            var targetPath = guard.EnsureSafeFileTarget(Path.Combine(folder, fileName));

            // Erst neben das Ziel schreiben, dann veroeffentlichen: ein Abbruch
            // mitten im Fuellen darf keine halbe Datei hinterlassen.
            var tempPath = targetPath + ".tmp";
            File.Copy(templatePath, tempPath, overwrite: true);

            IReadOnlyList<string> missingImages;
            try
            {
                // Einmal gebaut und zweimal gebraucht: fuer die Thementexte, die
                // dieselben Platzhalter tragen duerfen, und fuer die Vorlage.
                var values = BuildValues(request);

                using (var document = WordprocessingDocument.Open(tempPath, isEditable: true))
                {
                    // Zuerst die weggelassenen Kapitel: was nicht mehr da ist,
                    // muss auch nicht gefuellt werden.
                    foreach (var kapitel in request.Dossier.HiddenChapters ?? new())
                        DocxChapterRemover.Remove(document, kapitel);

                    // Das Deckblatt füllt Seite 1 bereits. Der zusätzliche
                    // Umbruch vor "Änderungswesen" erzeugte deshalb eine
                    // vollständig leere Seite 2.
                    DocxKnownBlankPageRemover.Apply(document);

                    // Die Vorlage setzt zwischen allen Buchstaben zusätzlichen
                    // Abstand und vor jede Verzeichniszeile 18 Punkt Luft.
                    // Kompakte Arial-Zeilen lesen sich deutlich ruhiger; die
                    // Word-Felder und ihre echten Seitenzahlen bleiben dabei
                    // vollständig erhalten.
                    DocxTocLayoutFormatter.Apply(document);

                    // Erst JETZT steht fest, wie viele Kapitel das Verzeichnis
                    // fuehrt: ein weggelassenes hat seine Zeile mitgenommen.
                    // Die Zusatzpunkte klonen die bereits vereinheitlichte
                    // Verzeichniszeile. Ihre eigene Zeichenformatierung wird
                    // danach aufgetragen und nicht wieder auf Schwarz gesetzt.
                    DocxTocAttachmentWriter.Apply(
                        document,
                        request.Dossier.TocAttachments,
                        ZaehleVerzeichniszeilen(document) + 1);

                    // Beschriftungen wie "Datum: {{Datum}}" muessen geaendert
                    // werden, solange der Platzhalter noch erkennbar ist. Die
                    // dabei gemerkten Zeichenformate werden vom Textfueller auf
                    // den fertigen Absatz uebertragen.
                    var literalFormatting = DocxLiteralTextReplacer.ApplyBeforePlaceholderFill(
                        document,
                        request.Dossier.TextOverrides,
                        request.Dossier.FieldStyles);

                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Haltungen",
                        BuildHoldingRows(request.Snapshot),
                        EmptyRowText("Haltungen"));

                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Eigentuemer",
                        BuildOwnerRows(request.Dossier),
                        EmptyRowText("Eigentuemer"));

                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Themen",
                        BuildTopicRows(request.Area, request.Dossier, values),
                        EmptyRowText("Themen"));

                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Aenderungen",
                        BuildChangeRows(request.Dossier),
                        EmptyRowText("Aenderungen"));

                    // Bilder VOR dem Textfueller: sonst wuerde der Textfueller
                    // "{{@Logo}}" als unbekannten Textplatzhalter leeren und das
                    // Bild fehlte im fertigen Dossier ohne jede Meldung.
                    missingImages = DocxImagePlaceholderFiller.Fill(
                            document, BuildImagePlacements(request, templatePath))
                        .Where(name => !HatFestEingebettetesBild(document, name))
                        .Where(name => !string.Equals(
                                name,
                                "Uebersichtsplan",
                                StringComparison.OrdinalIgnoreCase)
                            || !string.IsNullOrWhiteSpace(request.Dossier.OverviewPlanPath))
                        .ToList();

                    DocxPlaceholderFiller.Fill(document, values, literalFormatting);

                    // Im echten Word-Verzeichnis wird nur der Titel ersetzt.
                    // Nummer und Seitenzahl bleiben Felder; die gleichnamige
                    // Kapitelüberschrift wird direkt danach ebenfalls geändert.
                    DocxTocEntryEditor.Apply(
                        document,
                        request.Dossier.TextOverrides,
                        request.Dossier.FieldStyles);

                    // Danach die eigene Seitenzahl: Wo eine steht, ersetzt sie
                    // das Word-Feld. Alle uebrigen Kapitel behalten ihre
                    // Automatik.
                    DocxTocPageEditor.Apply(document, request.Dossier.TocChapterPages);

                    // Die Vorlage bestimmt weiterhin Groessen, Abstaende,
                    // Tabellen und Fusszeile. Nur die Schriftfamilie ist fuer
                    // alle sichtbaren Texte verbindlich Arial.
                    DocxPlaceholderFiller.SetArial(document);

                    document.MainDocumentPart?.Document?.Save();
                }

                File.Move(tempPath, targetPath);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }

            var message = $"Word-Datei erstellt: {Path.GetFileName(targetPath)}";
            var hint = BuildMissingImagesHint(missingImages);
            if (hint.Length > 0)
                message += "  (Hinweis: " + hint + ")";

            return Task.FromResult(new DossierWordExportResult(true, targetPath, message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DossierWordExportResult(
                false, null, $"Die Word-Datei konnte nicht erstellt werden: {ex.Message}"));
        }
    }

    /// <summary>Baut die Platzhalterwerte. Oeffentlich, damit sie testbar sind.</summary>
    /// <summary>
    /// Die Werte aller Textplatzhalter. Oeffentlich, weil die Seitenvorschau
    /// dieselbe Quelle verwenden MUSS — eine Vorschau, die andere Werte zeigt
    /// als der Export, waere schlimmer als gar keine.
    /// </summary>
    /// <summary>
    /// Legt die eigenen Angaben des Dossiers ueber die berechneten. Ein leerer
    /// Eintrag zaehlt: die Stelle bleibt dann bewusst leer.
    /// </summary>
    private static Dictionary<string, string> MitEigenenWerten(
        Dictionary<string, string> werte, DossierDefinition dossier)
    {
        foreach (var (name, wert) in dossier.FieldOverrides ?? new())
        {
            if (!string.IsNullOrWhiteSpace(name))
                werte[name.Trim()] = wert ?? string.Empty;
        }

        return werte;
    }

    private static Dictionary<string, string> MitFormaten(
        Dictionary<string, string> values,
        DossierDefinition dossier)
    {
        foreach (var (key, ranges) in dossier.FieldStyles ?? new())
        {
            if (string.IsNullOrWhiteSpace(key) || !values.TryGetValue(key, out var text))
                continue;

            values[key + DossierTopicTextFormatting.StyleRangesSuffix] =
                DossierTopicTextFormatting.Encode(
                    DossierTopicTextFormatting.Normalize(text, ranges));
        }

        return values;
    }

    public static Dictionary<string, string> BuildValues(
        DossierExportRequest request,
        DossierTocAttachmentStart? tocStart = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var d = request.Dossier;
        var resolved = DossierFieldResolver.Resolve(request.Area, d);
        var snapshot = request.Snapshot;
        var today = DateTime.Now;
        var verzeichnisStart = tocStart ?? new DossierTocAttachmentStart(4, 5);

        return MitFormaten(MitEigenenWerten(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Gebietstitel"] = resolved.AreaTitle,
                ["Parzellen"] = d.ParcelNumbers,
                ["Parzellen_Zeile"] = BuildParcelLine(d.ParcelNumbers),
                ["Hausnummern"] = d.HouseNumbers,
                ["Adresse_Zeile"] = BuildAddressLine(d),
                ["Adresse"] = d.Address,
                ["PLZ"] = d.PostalCode,
                ["Ort"] = d.Town,
                ["Eigentuemer"] = d.OwnerName,
                ["Eigentuemer_Block"] = BuildCoverOwnerBlock(d),
                ["Eigentuemer_Detail"] = BuildOwnerDetail(d),
                ["Kontakt"] = d.ContactName,
                ["Telefon"] = d.ContactPhone,
                ["EMail"] = d.ContactMail,
                ["Objektbewohner"] = d.Occupancy,
                ["Datum"] = today.ToString("dd.MM.yyyy", Ch),
                ["Datum_Lang"] = today.ToString("dd. MMMM yyyy", Ch),
                ["Revision"] = d.Revision,
                // Kein Rueckfall auf Environment.UserName: "lieber leer als
                // falsch" — der Windows-Benutzername gehoert nicht in ein
                // Dokument fuer den Eigentuemer.
                ["Autoren"] = string.IsNullOrWhiteSpace(request.Area.Authors)
                ? string.Empty
                : request.Area.Authors.Trim(),
                ["Dossier_Name"] = d.Name,

                ["Ausfuehrungstermin"] = resolved.ExecutionDate,
                ["Ansprechpartner"] = resolved.ContactPerson,
                ["Unternehmer"] = resolved.Contractor,
                ["Bauleitung"] = resolved.SiteManagement,
                ["Behinderungen"] = resolved.Obstructions,
                ["Bauvorgang"] = d.ConstructionProcess,
                ["Hausanschluss"] = resolved.HouseConnectionText,
                ["Meteorwasser"] = resolved.StormWaterText,
                ["Bemerkungen"] = d.Remarks,
                ["Beilagen"] = d.Attachments,
                ["Rueckmeldung"] = BuildResponseText(resolved.ResponseDeadline),
                ["Fusszeile"] = resolved.FooterLine,

                ["Anzahl_Haltungen"] = snapshot.HoldingCount.ToString(CultureInfo.InvariantCulture),
                ["Laenge_Total"] = FormatLength(snapshot.LengthTotal),
                ["Kosten_Total"] = FormatChf(snapshot.NetCostTotal),
                ["Kosten_Hinweis"] = snapshot.NetCostTotal > 0m
                ? "Kostenangaben ohne MWST, Stand " + today.ToString("dd.MM.yyyy", Ch) + "."
                : string.Empty,
                ["Gebiet_Ort"] = request.Area.AreaLocation,
                ["Ort_Zeile"] = BuildTownLine(d),
                ["Projekt_Nr"] = request.Area.ProjectNumber,
                ["Gezeichnet"] = request.Area.DrawnBy,
                ["Aktennotiz"] = d.FileNote,
                ["Haltungen_Text"] = BuildHoldingsText(snapshot),
                ["Schaechte_Text"] = BuildShaftsText(snapshot),
                [DossierTopicComponentListComposer.ValueKey] = BuildNumberedComponentsText(snapshot),
                ["Uebersichtsplan_BreiteCm"] = PlanWidthCm(d)
                .ToString("0.###", CultureInfo.InvariantCulture),
                ["Anzahl_Schaechte"] = snapshot.ShaftCount.ToString(CultureInfo.InvariantCulture),
                // Die Vorschau übergibt den aus ihren sichtbaren Word-Zeilen
                // berechneten Start. Ohne Vorschau gilt die unveränderte Vorlage;
                // der Export zählt nach dem Entfernen ausgeblendeter Kapitel
                // nochmals direkt im echten Word-Dokument.
                ["Verzeichnis_Beilagen"] = DossierTocAttachments.Build(
                d.TocAttachments,
                verzeichnisStart.FirstNumber,
                verzeichnisStart.FirstPageNumber),
                ["Haltungen_Summe"] = BuildHoldingsSummary(snapshot, today)
            }, d), d);
    }

    /// <summary>
    /// Die ausgewaehlten Leitungen als Textblock — eine Zeile je Leitung.
    ///
    /// Die Vorlage kann sie stattdessen auch als Tabelle fuehren; dafuer gibt es
    /// die Wiederholzeile <c>{{#Haltungen}}</c>. Der Textblock ist der Weg, der
    /// ohne Tabellenbau in Word auskommt.
    /// </summary>
    /// <summary>PLZ und Ort als eine Zeile, z.B. "6487 Göschenen".</summary>
    /// <summary>
    /// Die echten Zeilen, die das Word-Verzeichnis bereits führt. Nur ein
    /// strukturell lesbarer PAGEREF-Eintrag zählt; ein leerer Absatz oder der
    /// gleich formatierte Platzhalter darf die Nummer nicht verschieben.
    /// </summary>
    private static int ZaehleVerzeichniszeilen(WordprocessingDocument document)
    {
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return 0;

        return body
            .Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Count(absatz => DocxTocEntryReader.Read(absatz) is not null);
    }

    private static string BuildTownLine(DossierDefinition dossier)
        => string.Join(" ", new[] { dossier.PostalCode, dossier.Town }
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim()));

    private static string BuildHoldingsText(DossierSnapshot snapshot)
    {
        if (snapshot.Holdings.Count == 0)
            return "Keine Leitungen zugeordnet.";

        return string.Join(Environment.NewLine,
            snapshot.Holdings.Select(BuildHoldingDescription));
    }

    /// <summary>
    /// Die Schaechte der Liegenschaft als Textblock. Ohne Schaechte bleibt die
    /// Zeile leer statt einen Hinweis zu drucken — im Dossier steht dann
    /// einfach nichts, so wie bei jeder anderen leeren Angabe.
    /// </summary>
    private static string BuildShaftsText(DossierSnapshot snapshot)
    {
        if (snapshot.Shafts.Count == 0)
            return string.Empty;

        var zeilen = new List<string>
        {
            snapshot.Shafts.Count == 1
                ? "1 Schacht:"
                : snapshot.Shafts.Count + " Schächte:"
        };

        foreach (var schacht in snapshot.Shafts)
            zeilen.Add(BuildShaftDescription(schacht));

        return string.Join(Environment.NewLine, zeilen);
    }

    private static string BuildNumberedComponentsText(DossierSnapshot snapshot)
    {
        var lines = new List<string>();
        var number = 1;

        foreach (var holding in snapshot.Holdings)
            lines.Add($"{number++}. Haltung {BuildHoldingDescription(holding)}".TrimEnd());

        foreach (var shaft in snapshot.Shafts)
            lines.Add($"{number++}. Schacht {BuildShaftDescription(shaft)}".TrimEnd());

        return lines.Count == 0
            ? "Keine Leitungen zugeordnet."
            : string.Join("\n", lines);
    }

    private static string BuildHoldingDescription(DossierHoldingLine holding)
    {
        var parts = new List<string> { holding.HoldingName };

        if (!string.IsNullOrWhiteSpace(holding.Street))
            parts.Add(holding.Street);

        var length = FormatLength(holding.LengthMeters);
        if (!string.IsNullOrWhiteSpace(length))
            parts.Add(length);

        var condition = FormatConditionInline(holding.ConditionClass);
        if (!string.IsNullOrWhiteSpace(condition))
            parts.Add(condition);

        if (!string.IsNullOrWhiteSpace(holding.Measures))
            parts.Add(holding.Measures);

        if (holding.NetCost > 0m)
            parts.Add(FormatChf(holding.NetCost));

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Ein Schacht als Aufzaehlungszeile. Dieselben Angaben wie die Tabelle im
    /// Cockpit — Funktion statt Laenge, denn eine Laenge hat ein Schacht nicht.
    /// Was fehlt, bleibt weg statt als Strich zu erscheinen.
    /// </summary>
    private static string BuildShaftDescription(DossierShaftLine shaft)
    {
        var parts = new List<string> { shaft.Number };

        if (!string.IsNullOrWhiteSpace(shaft.Street))
            parts.Add(shaft.Street);

        if (!string.IsNullOrWhiteSpace(shaft.Funktion))
            parts.Add(shaft.Funktion);

        var condition = FormatConditionInline(shaft.ConditionClass);
        if (!string.IsNullOrWhiteSpace(condition))
            parts.Add(condition);

        if (!string.IsNullOrWhiteSpace(shaft.Measures))
            parts.Add(shaft.Measures);

        if (shaft.NetCost > 0m)
            parts.Add(FormatChf(shaft.NetCost));

        return string.Join(" · ", parts);
    }

    private static string BuildHoldingsSummary(DossierSnapshot snapshot, DateTime today)
    {
        if (snapshot.Holdings.Count == 0)
            return string.Empty;

        var teile = new List<string>
        {
            snapshot.HoldingCount == 1 ? "1 Leitung" : snapshot.HoldingCount + " Leitungen"
        };

        var laenge = FormatLength(snapshot.LengthTotal);
        if (!string.IsNullOrWhiteSpace(laenge))
            teile.Add("Gesamtlänge " + laenge);

        if (snapshot.NetCostTotal > 0m)
        {
            teile.Add("Kostenschätzung " + FormatChf(snapshot.NetCostTotal)
                + " (ohne MWST, Stand " + today.ToString("dd.MM.yyyy", Ch) + ")");
        }

        return string.Join(" · ", teile);
    }

    /// <summary>
    /// Die Themen der Tabelle "Informationen": Gebietsstandard, vom Dossier
    /// ueberschrieben. Die Regel selbst liegt im
    /// <see cref="DossierTopicResolver"/> — hier wird nur abgebildet.
    /// </summary>
    public static List<IReadOnlyDictionary<string, string>> BuildTopicRows(
        DossierAreaSettings area,
        DossierDefinition dossier,
        IReadOnlyDictionary<string, string>? values = null)
        => DossierTopicResolver.Resolve(area, dossier)
            .Select(thema =>
            {
                var formatiert = values is null
                    ? new DossierTopicTextFormatting.FormattedText(
                        thema.Text,
                        DossierTopicTextFormatting.EffectiveRanges(thema))
                    : DossierTopicComponentListComposer.Compose(thema, values);

                return (IReadOnlyDictionary<string, string>)
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Thema"] = thema.Title,
                        ["Thema" + DossierTopicTextFormatting.StyleRangesSuffix] =
                            DossierTopicTextFormatting.Encode(
                                DossierTopicTitleEditing.Styles(
                                    dossier,
                                    DossierTopicTitleEditing.SourceTitle(thema),
                                    thema.Title)),
                        ["Text"] = formatiert.Text,

                        // Alte Dossiers mit einer Farbe fuer die ganze Zeile
                        // bleiben lesbar. Neue Eintraege tragen genaue Bereiche.
                        ["Text" + DocxPlaceholderFiller.FarbSuffix] =
                            thema.StyleRanges is { Count: > 0 }
                                ? string.Empty
                                : thema.ColorHex ?? string.Empty,
                        ["Text" + DossierTopicTextFormatting.StyleRangesSuffix] =
                            DossierTopicTextFormatting.Encode(formatiert.StyleRanges)
                    };
            })
            .ToList();

    public static List<IReadOnlyDictionary<string, string>> BuildChangeRows(
        DossierDefinition dossier)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        foreach (var change in (dossier.Changes ?? new List<DossierChangeRow>())
            .Where(change => change is not null))
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            DossierRowTextFormatting.AddValue(
                row, "Version", change.Version,
                DossierRowTextFormatting.Styles(change.FieldStyles, "Version"));
            DossierRowTextFormatting.AddValue(
                row, "Datum", change.Date,
                DossierRowTextFormatting.Styles(change.FieldStyles, "Date"));
            DossierRowTextFormatting.AddValue(
                row, "Visum", change.Visum,
                DossierRowTextFormatting.Styles(change.FieldStyles, "Visum"));
            DossierRowTextFormatting.AddValue(
                row, "Aenderung", change.Change,
                DossierRowTextFormatting.Styles(change.FieldStyles, "Change"));
            rows.Add(row);
        }

        return rows;
    }

    public static List<IReadOnlyDictionary<string, string>> BuildHoldingRows(
        DossierSnapshot snapshot)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();

        foreach (var holding in snapshot.Holdings)
        {
            rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Haltung"] = holding.HoldingName,
                ["Strasse"] = holding.Street,
                ["Laenge"] = FormatLength(holding.LengthMeters),
                ["Zustand"] = FormatCondition(holding.ConditionClass),
                ["Massnahme"] = holding.Measures,
                ["Kosten"] = holding.NetCost > 0m ? FormatChf(holding.NetCost) : "—"
            });
        }

        return rows;
    }

    /// <summary>
    /// Die Zeilen der Tabelle "Eigentumsverhaeltnisse". Oeffentlich, damit sie
    /// testbar sind.
    /// </summary>
    public static List<IReadOnlyDictionary<string, string>> BuildOwnerRows(
        DossierDefinition dossier)
        => DossierOwnerRowBuilder.Build(dossier);

    /// <summary>
    /// Auf dem Deckblatt stehen die klassischen Felder "Eigentuemer"/"Adresse
    /// des Eigentuemers", sofern eines von beiden gefuellt ist — sonst gehen
    /// sie verloren, sobald die Tabellenzeilen einen gekuerzten Namen tragen.
    /// Erst wenn beide leer sind, gelten stattdessen die Namen aller
    /// Eigentuemerzeilen untereinander.
    /// </summary>
    private static string BuildCoverOwnerBlock(DossierDefinition dossier)
    {
        var legacy = JoinLines(dossier.OwnerName, dossier.OwnerAddress);
        if (legacy.Length > 0)
            return legacy;

        var names = dossier.Owners
            .Select(owner => owner.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Split('\n')[0].Trim())
            .ToList();

        return string.Join("\n", names);
    }

    /// <summary>
    /// Logo und Wappen liegen fest neben der Word-Vorlage; der Uebersichtsplan
    /// gehoert zur einzelnen Liegenschaft. Ein relativer Planpfad wird am
    /// Projektordner aufgeloest.
    /// </summary>
    private static List<DocxImagePlacement> BuildImagePlacements(
        DossierExportRequest request,
        string templatePath)
    {
        var placements = new List<DocxImagePlacement>();
        var templateFolder = Path.GetDirectoryName(templatePath);

        if (!string.IsNullOrWhiteSpace(templateFolder))
        {
            placements.Add(new DocxImagePlacement(
                "Logo", Path.Combine(templateFolder, LogoFileName), MaxWidthCm: 4.5));
            placements.Add(new DocxImagePlacement(
                "Wappen", Path.Combine(templateFolder, CoatOfArmsFileName), MaxWidthCm: 2.0));
        }

        // Auch ohne gewaehlte Datei wird die Bildmarke verarbeitet. So kann
        // der Fueller den grossen schwebenden Planrahmen der Vorlage entfernen;
        // sonst liegt er in Word ueber Kapitel 2 und 3.
        var plan = ResolvePlanPath(request) ?? string.Empty;
        var width = PlanWidthCm(request.Dossier);
        placements.Add(new DocxImagePlacement(
            "Uebersichtsplan",
            plan,
            MaxWidthCm: width,
            HeightCm: PlanHeightForWidth(width),
            RemoveParagraphWhenMissing: true,
            FitWithinBounds: true));

        return placements;
    }

    /// <summary>
    /// Baut den Hinweistext fuer nicht eingesetzte Bilder — in Klartext, nicht
    /// mit den technischen Platzhalternamen. Pascal soll das VOR dem Versand
    /// merken, nicht erst beim Eigentuemer.
    /// </summary>
    private static string BuildMissingImagesHint(IReadOnlyList<string> missingPlaceholders)
        => string.Join(" ", missingPlaceholders.Select(DescribeMissingImage));

    private static bool HatFestEingebettetesBild(
        WordprocessingDocument document,
        string name)
        => document.MainDocumentPart?.Document?.Body?
            .Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>()
            .Any(properties => string.Equals(
                properties.Name?.Value,
                name,
                StringComparison.OrdinalIgnoreCase)) == true;

    private static string DescribeMissingImage(string placeholderName) => placeholderName switch
    {
        "Logo" => "Firmenlogo nicht gefunden.",
        "Wappen" => "Wappen nicht gefunden.",
        "Uebersichtsplan" => "Werkleitungsplan nicht gefunden – Kapitel 1 bleibt leer.",
        _ => placeholderName + " nicht gefunden."
    };

    /// <summary>
    /// Der Pfad des Uebersichtsplans, relative Angaben am Projektordner
    /// aufgeloest. Oeffentlich, weil die Vorschau denselben Pfad braucht — sonst
    /// zeigt sie eine leere Stelle, wo das Dossier ein Bild traegt.
    /// </summary>
    /// <summary>
    /// Die Breite des Uebersichtsplans im Dokument. Ohne eigene Angabe gilt die
    /// Breite der Vorlage; unsinnige Werte werden auf das Blatt begrenzt.
    /// </summary>
    public static double PlanWidthCm(DossierDefinition dossier)
        => dossier?.OverviewPlanWidthCm is { } cm && cm > 0
            ? Math.Min(cm, PlanMaxWidthCm)
            : PlanMaxWidthCm;

    public static string? ResolvePlanPath(DossierExportRequest request)
    {
        var configured = request.Dossier.OverviewPlanPath;
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        try
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(request.ProjectRoot, configured);
        }
        catch
        {
            // Ein unsinniger Pfad darf das Dossier nicht verhindern; die Stelle
            // bleibt dann leer.
            return null;
        }
    }

    private static string Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    /// <summary>
    /// Nur Strasse und Hausnummer. Ort und Postleitzahl stehen in der Vorlage
    /// als eigene Zeile <c>{{Ort_Zeile}}</c> darunter — beides hier zu
    /// wiederholen hiesse, den Ort auf dem Deckblatt zweimal zu drucken.
    /// </summary>
    private static string BuildAddressLine(DossierDefinition d)
        => string.Join(
            " ",
            new[] { d.Address, d.HouseNumbers }.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>
    /// "Parzelle 439" oder "Parzellen 439, 440". Das Feld ist Freitext; als
    /// mehrere gilt es erst, wenn wirklich ein Trenner oder eine zweite
    /// Nummerngruppe darin steht.
    /// </summary>
    private static string BuildParcelLine(string? parzellen)
    {
        if (string.IsNullOrWhiteSpace(parzellen))
            return string.Empty;

        var text = parzellen.Trim();
        var mehrere = text.IndexOfAny(new[] { ',', ';', '+', '/', '&' }) >= 0
            || System.Text.RegularExpressions.Regex.Matches(text, @"\d+").Count > 1;

        return (mehrere ? "Parzellen " : "Parzelle ") + text;
    }

    private static string BuildOwnerDetail(DossierDefinition d)
    {
        var parts = new List<string>();

        var owner = JoinInline(d.OwnerName, d.OwnerAddress);
        if (owner.Length > 0)
            parts.Add(owner);

        if (!string.IsNullOrWhiteSpace(d.ContactName))
            parts.Add("Zuständigkeit: " + d.ContactName.Trim());

        if (!string.IsNullOrWhiteSpace(d.ContactPhone))
            parts.Add("Tel.: " + d.ContactPhone.Trim());

        if (!string.IsNullOrWhiteSpace(d.ContactMail))
            parts.Add("E-Mail: " + d.ContactMail.Trim());

        if (!string.IsNullOrWhiteSpace(d.Occupancy))
            parts.Add("Objektbewohner: " + d.Occupancy.Trim());

        return string.Join("\n", parts);
    }

    private static string BuildResponseText(string deadline)
        => string.IsNullOrWhiteSpace(deadline)
            ? "Rückmeldung erbeten."
            : "Rückmeldung bis " + deadline.Trim() + ":";

    private static string JoinLines(params string?[] parts)
        => string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string JoinInline(params string?[] parts)
        => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string FormatChf(decimal value)
        => value.ToString("#,##0.00", Ch);

    private static string FormatLength(double? meters)
        => meters is null or 0d ? "—" : meters.Value.ToString("0.00", Ch) + " m";

    /// <summary>
    /// Zustandsklasse fuer den Eigentuemer lesbar machen. "ohne" heisst, dass
    /// keine Klasse hinterlegt ist — das wird als Strich gezeigt und nicht als
    /// Klasse 0 ausgegeben, die "dringend" bedeuten wuerde.
    /// </summary>
    /// <summary>
    /// Die Zustandsklasse fuer eine TABELLENZELLE. Ohne Wert ein Strich: eine
    /// leere Zelle liesse offen, ob nichts erfasst oder nichts noetig ist.
    /// </summary>
    private static string FormatCondition(string conditionClass) => conditionClass switch
    {
        "0" => "Z0 – sofort",
        "1" => "Z1 – kurzfristig",
        "2" => "Z2 – mittelfristig",
        "3" => "Z3 – langfristig",
        "4" => "Z4 – kein Mangel",
        _ => "—"
    };

    /// <summary>
    /// Die Zustandsklasse fuer eine AUFZAEHLUNGSZEILE. Ohne Wert bleibt sie
    /// weg. In einer Zeile aus mit Punkten verbundenen Angaben haengt ein
    /// Strich sonst nackt hinten dran — "Schacht 33458 · —" stand so in den
    /// Briefen, weil Schaechte im Bestand keine Zustandsklasse tragen.
    /// </summary>
    private static string FormatConditionInline(string conditionClass)
    {
        var text = FormatCondition(conditionClass);
        return text == "—" ? string.Empty : text;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ein liegen gebliebener .tmp ist harmlos; ein Folgefehler hier
            // wuerde die eigentliche Ursache verdecken.
        }
    }
}
