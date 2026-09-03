using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using ClosedXML.Excel;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>
/// Fuellt die Export-Vorlagen mit den Projektdaten.
///
/// Das Aussehen liegt vollstaendig in der Vorlage: Logo, Diagramme,
/// Kennzahlenbloecke mit Formeln, bedingte Formatierung, Titelband, Kopfzeile,
/// Druckeinrichtung und eine gestaltete Musterzeile. Dieser Dienst schreibt nur
/// Werte, kopiert den Stil der Musterzeile nach unten und setzt die Zeilenhoehe.
///
/// Insbesondere faerbt er NICHTS ein. Die Ampelfarben kommen aus der bedingten
/// Formatierung der Vorlage - nur so folgt die Farbe auch dann noch dem Wert,
/// wenn jemand die fertige Datei in Excel von Hand nachbearbeitet. Eine fest
/// gesetzte Fuellung wuerde stehenbleiben und den falschen Zustand behaupten.
/// </summary>
public sealed class ExcelTemplateExportService : IExcelExportService
{
    private readonly Action<XLWorkbook, string> _saveWorkbook;

    public ExcelTemplateExportService()
        : this(static (workbook, path) => workbook.SaveAs(path))
    {
    }

    /// <summary>
    /// Testnaht fuer einen kontrolliert abbrechenden Schreibvorgang. Der echte
    /// Dienst verwendet immer ClosedXML und schreibt ebenfalls nur in die
    /// vorbereitete Temp-Datei.
    /// </summary>
    internal ExcelTemplateExportService(Action<XLWorkbook, string> saveWorkbook)
    {
        _saveWorkbook = saveWorkbook ?? throw new ArgumentNullException(nameof(saveWorkbook));
    }

    public Result ExportToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow)
        => ExportToTemplate(
            project,
            templatePath,
            outputPath,
            headerRow,
            startRow,
            projectFilePath: null);

    public Result ExportToTemplate(
        Project project,
        string templatePath,
        string outputPath,
        int headerRow,
        int startRow,
        string? projectFilePath)
        => ExportToTemplate(
            project,
            templatePath,
            outputPath,
            headerRow,
            startRow,
            projectFilePath,
            CancellationToken.None);

    public Result ExportToTemplate(
        Project project,
        string templatePath,
        string outputPath,
        int headerRow,
        int startRow,
        string? projectFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            PruefeEingaben(project, templatePath, outputPath);

            var limitFailure = ExcelTemplateExportLimit.RejectIfExceeded(
                project.Data.Count, "Haltungen", "EXP-EXCEL-LIMIT");
            if (limitFailure is not null)
                return limitFailure;

            if (headerRow <= 0) headerRow = ExcelVorlagenLayout.KopfZeile;
            if (startRow <= 0) startRow = ExcelVorlagenLayout.ErsteDatenZeile;

            cancellationToken.ThrowIfCancellationRequested();
            using var wb = new XLWorkbook(templatePath);
            cancellationToken.ThrowIfCancellationRequested();
            var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Haltungen", StringComparison.OrdinalIgnoreCase))
                     ?? wb.Worksheet(1);

            var headerToCol = ReadHeaderColumns(ws, headerRow);
            var fieldToCol = BaueFeldZuordnung(headerToCol);
            if (fieldToCol.Count == 0)
                throw new InvalidOperationException("Keine passenden Spalten im Excel-Template gefunden (Header-Zeile pruefen).");

            var spaltenzahl = headerToCol.Values.DefaultIfEmpty(1).Max();
            LeereAlteDaten(ws, startRow, spaltenzahl);

            var ordered = project.Data
                .OrderBy(r => TryInt(r.GetFieldValue("NR")) ?? int.MaxValue)
                .ThenBy(r => r.GetFieldValue(FieldKeys.HoldingName) ?? "")
                .ToList();

            fieldToCol.TryGetValue(FieldKeys.RecommendedRehabilitationMeasures, out var massnahmenSpalte);
            fieldToCol.TryGetValue(FieldKeys.Link, out var linkSpalte);

            var row = startRow;
            var runningNr = 1;
            foreach (var rec in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UebernimmMusterStil(ws, startRow, row, spaltenzahl);

                if (fieldToCol.TryGetValue("NR", out var nrCol))
                {
                    var nr = (rec.GetFieldValue("NR") ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(nr))
                        nr = runningNr.ToString(CultureInfo.InvariantCulture);
                    SchreibeText(ws.Cell(row, nrCol), nr);
                }

                foreach (var field in FieldCatalog.ColumnOrder)
                {
                    if (string.Equals(field, "NR", StringComparison.Ordinal)
                        || !fieldToCol.TryGetValue(field, out var col)
                        || col == linkSpalte)
                        continue;

                    var def = FieldCatalog.Get(field);
                    var value = rec.GetFieldValue(field);

                    if (string.Equals(field, FieldKeys.PipeMaterial, StringComparison.Ordinal))
                        value = ExcelMaterialLangform.Auflösen(value);

                    if (def.Type is FieldType.Decimal or FieldType.Int)
                        SchreibeZahl(ws, row, col, headerRow, value);
                    else
                        SchreibeText(ws.Cell(row, col), value);
                }

                if (linkSpalte > 0)
                    SchreibeVerweis(
                        ws.Cell(row, linkSpalte),
                        rec.GetFieldValue(FieldKeys.Link),
                        projectFilePath);

                SetzeZeilenhoehe(ws, row, massnahmenSpalte,
                    massnahmenSpalte > 0 ? rec.GetFieldValue(FieldKeys.RecommendedRehabilitationMeasures) : null);

                row++;
                runningNr++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            SchliesseBlattAb(ws, project, "Haltungen", headerRow, startRow, row - 1, spaltenzahl);
            SpeichereGeprueftUndVeroeffentliche(wb, outputPath, ws.Name, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail("EXP-EXCEL", ex.Message);
        }
    }

    public Result ExportSchaechteToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow)
        => ExportSchaechteToTemplate(
            project,
            templatePath,
            outputPath,
            headerRow,
            startRow,
            projectFilePath: null);

    public Result ExportSchaechteToTemplate(
        Project project,
        string templatePath,
        string outputPath,
        int headerRow,
        int startRow,
        string? projectFilePath)
        => ExportSchaechteToTemplate(
            project,
            templatePath,
            outputPath,
            headerRow,
            startRow,
            projectFilePath,
            CancellationToken.None);

    public Result ExportSchaechteToTemplate(
        Project project,
        string templatePath,
        string outputPath,
        int headerRow,
        int startRow,
        string? projectFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            PruefeEingaben(project, templatePath, outputPath);

            var limitFailure = ExcelTemplateExportLimit.RejectIfExceeded(
                project.SchaechteData.Count, "Schaechte", "EXP-EXCEL-SCHACHT-LIMIT");
            if (limitFailure is not null)
                return limitFailure;

            if (headerRow <= 0) headerRow = ExcelVorlagenLayout.KopfZeile;
            if (startRow <= 0) startRow = ExcelVorlagenLayout.ErsteDatenZeile;

            cancellationToken.ThrowIfCancellationRequested();
            using var wb = new XLWorkbook(templatePath);
            cancellationToken.ThrowIfCancellationRequested();
            var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Schaechte", StringComparison.OrdinalIgnoreCase))
                     ?? wb.Worksheet(1);

            var headerToCol = ReadHeaderColumns(ws, headerRow);
            if (headerToCol.Count == 0)
                throw new InvalidOperationException("Keine Spalten in der Schacht-Vorlage gefunden (Header-Zeile pruefen).");

            var spaltenzahl = headerToCol.Values.Max();
            LeereAlteDaten(ws, startRow, spaltenzahl);

            var massnahmenSpalte = FindeSpalte(headerToCol, "massnahmen");
            var linkSpalte = FindeSpalte(headerToCol, "link");

            var row = startRow;
            var runningNr = 1;
            foreach (var rec in project.SchaechteData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UebernimmMusterStil(ws, startRow, row, spaltenzahl);

                foreach (var pair in headerToCol)
                {
                    if (ExcelSchachtFeldzuordnung.IstLink(pair.Key))
                        continue;

                    var value = ExcelSchachtFeldzuordnung.Lese(rec, pair.Key);
                    if (ExcelSchachtFeldzuordnung.IstLaufendeNummer(pair.Key)
                        && string.IsNullOrWhiteSpace(value))
                    {
                        value = runningNr.ToString(CultureInfo.InvariantCulture);
                    }

                    if (ExcelSchachtFeldzuordnung.IstZahl(pair.Key))
                        SchreibeZahl(ws, row, pair.Value, headerRow, value);
                    else
                        SchreibeText(ws.Cell(row, pair.Value), value);
                }

                if (linkSpalte > 0)
                    SchreibeVerweis(
                        ws.Cell(row, linkSpalte),
                        ExcelSchachtFeldzuordnung.Lese(
                            rec,
                            headerToCol.First(pair => pair.Value == linkSpalte).Key),
                        projectFilePath);

                SetzeZeilenhoehe(ws, row, massnahmenSpalte,
                    massnahmenSpalte > 0
                        ? ExcelSchachtFeldzuordnung.Lese(
                            rec,
                            headerToCol.First(pair => pair.Value == massnahmenSpalte).Key)
                        : null);

                row++;
                runningNr++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            SchliesseBlattAb(ws, project, "Schächte", headerRow, startRow, row - 1, spaltenzahl);
            SpeichereGeprueftUndVeroeffentliche(wb, outputPath, ws.Name, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail("EXP-EXCEL-SCHACHT", ex.Message);
        }
    }

    // ── gemeinsame Bausteine ────────────────────────────────────────────────

    private static void PruefeEingaben(Project project, string templatePath, string outputPath)
    {
        if (project is null) throw new ArgumentNullException(nameof(project));
        if (string.IsNullOrWhiteSpace(templatePath)) throw new ArgumentException("Template-Pfad fehlt.", nameof(templatePath));
        if (!File.Exists(templatePath)) throw new FileNotFoundException("Excel-Vorlage nicht gefunden.", templatePath);
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output-Pfad fehlt.", nameof(outputPath));

        var vorlage = Path.GetFullPath(templatePath);
        var ausgabe = Path.GetFullPath(outputPath);
        if (string.Equals(vorlage, ausgabe, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Die Excel-Vorlage darf nicht zugleich das Ausgabeziel sein. Die Vorlage wurde nicht verändert.");
        }
    }

    /// <summary>
    /// Schreibt im Zielordner zuerst eine eindeutige Temp-Datei, oeffnet diese
    /// zur Kontrolle erneut und veroeffentlicht erst danach per Umbenennung.
    /// Dadurch wird nie eine halbfertige Arbeitsmappe unter dem Zielnamen sichtbar.
    /// </summary>
    private void SpeichereGeprueftUndVeroeffentliche(
        XLWorkbook workbook,
        string outputPath,
        string erwartetesBlatt,
        CancellationToken cancellationToken)
    {
        string? temporaer = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ausgabe = Path.GetFullPath(outputPath);
            var ordner = Path.GetDirectoryName(ausgabe);
            if (string.IsNullOrWhiteSpace(ordner))
                throw new InvalidOperationException($"Ungültiger Excel-Ausgabepfad: {outputPath}");

            Directory.CreateDirectory(ordner);
            temporaer = Path.Combine(
                ordner,
                $".{Path.GetFileName(ausgabe)}.{Guid.NewGuid():N}.tmp.xlsx");

            cancellationToken.ThrowIfCancellationRequested();
            _saveWorkbook(workbook, temporaer);
            cancellationToken.ThrowIfCancellationRequested();
            PruefeGespeicherteArbeitsmappe(temporaer, erwartetesBlatt);
            cancellationToken.ThrowIfCancellationRequested();
            BestaetigeAufDatentraeger(temporaer);
            cancellationToken.ThrowIfCancellationRequested();

            // Temp und Ziel liegen absichtlich im selben Ordner. Das Umbenennen
            // ist damit auf NTFS atomar und zeigt nie eine teilweise Datei.
            File.Move(temporaer, ausgabe, overwrite: true);
            temporaer = null;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaer) && File.Exists(temporaer))
            {
                try
                {
                    File.Delete(temporaer);
                }
                catch
                {
                    // Die Aufraeumung darf den eigentlichen Exportfehler nicht verdecken.
                }
            }
        }
    }

    private static void PruefeGespeicherteArbeitsmappe(string path, string erwartetesBlatt)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new InvalidDataException("Die erzeugte Excel-Datei ist leer.");

        using (var archiv = ZipFile.OpenRead(path))
        {
            if (archiv.GetEntry("[Content_Types].xml") is null
                || archiv.GetEntry("xl/workbook.xml") is null
                || !archiv.Entries.Any(entry =>
                    entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                    && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("Die erzeugte Datei ist keine vollstaendige Excel-Arbeitsmappe.");
            }
        }

        using var pruefung = new XLWorkbook(path);
        if (!pruefung.Worksheets.Any(
                sheet => string.Equals(sheet.Name, erwartetesBlatt, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                $"Die erzeugte Excel-Datei enthält das Blatt '{erwartetesBlatt}' nicht.");
        }
    }

    private static void BestaetigeAufDatentraeger(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Uebertraegt den Stil der Musterzeile. Die Vorlage gestaltet nur EINE
    /// Datenzeile; alles darunter erbt von ihr. Sonst muesste die Vorlage
    /// tausende leere, aber formatierte Zellen mitschleppen.
    /// </summary>
    private static void UebernimmMusterStil(IXLWorksheet ws, int musterZeile, int zielZeile, int spaltenzahl)
    {
        if (zielZeile == musterZeile)
            return;

        for (var c = 1; c <= spaltenzahl; c++)
            ws.Cell(zielZeile, c).Style = ws.Cell(musterZeile, c).Style;
    }

    private static void LeereAlteDaten(IXLWorksheet ws, int startRow, int spaltenzahl)
    {
        var letzte = ws.LastRowUsed()?.RowNumber() ?? startRow;
        if (letzte < startRow)
            return;

        ws.Range(startRow, 1, letzte, spaltenzahl).Clear(XLClearOptions.Contents);
    }

    private static void SetzeZeilenhoehe(IXLWorksheet ws, int row, int massnahmenSpalte, string? text)
    {
        var breite = massnahmenSpalte > 0 ? ws.Column(massnahmenSpalte).Width : 30d;
        ws.Row(row).Height = ExcelZeilenhoehe.Berechne(text, breite);
    }

    /// <summary>
    /// Schreibt einen anklickbaren Verweis. Der volle Pfad bleibt als Hinweis
    /// erhalten; scheitert der Verweis (etwa bei einem ungueltigen Pfad), steht
    /// der Pfad wenigstens als Text da statt gar nichts.
    /// </summary>
    private static void SchreibeVerweis(
        IXLCell zelle,
        string? ziel,
        string? projectFilePath)
    {
        var pfad = (ziel ?? "").Trim();
        LeereZelle(zelle);
        if (string.IsNullOrWhiteSpace(pfad))
            return;

        var aufgeloestesZiel = LoeseVerweiszielAuf(pfad, projectFilePath);
        if (string.IsNullOrWhiteSpace(aufgeloestesZiel))
        {
            zelle.Value = pfad;
            return;
        }

        try
        {
            zelle.Value = "öffnen";
            zelle.SetHyperlink(new XLHyperlink(
                new Uri(aufgeloestesZiel, UriKind.RelativeOrAbsolute),
                aufgeloestesZiel));
        }
        catch (Exception)
        {
            zelle.Value = pfad;
        }
    }

    /// <summary>
    /// Relative Projektpfade werden nur lexikalisch und ohne Zugriff auf die
    /// Quelldatei aufgeloest. So funktionieren die Links auch dann, wenn der
    /// Bericht in einem beliebigen anderen Ordner gespeichert wird.
    /// </summary>
    private static string? LoeseVerweiszielAuf(string pfad, string? projectFilePath)
    {
        if (Path.IsPathRooted(pfad)
            || Uri.TryCreate(pfad, UriKind.Absolute, out _))
        {
            return pfad;
        }

        // Alte Aufrufer kennen den Projektpfad noch nicht. Ihr bisheriger,
        // relativer externer Link bleibt deshalb unveraendert kompatibel.
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return pfad;

        if (!ProjectPathResolver.IsSafeRelativeProjectPath(pfad))
            return null;

        try
        {
            var vollerProjektpfad = Path.GetFullPath(projectFilePath);
            var projectRoot = ProjectFileLocator.ProjectRootFromFile(vollerProjektpfad);
            if (string.IsNullOrWhiteSpace(projectRoot))
                return null;

            var normalisierterRoot = Path.GetFullPath(projectRoot);
            var aufgeloest = Path.GetFullPath(Path.Combine(normalisierterRoot, pfad));
            var rootMitTrenner = normalisierterRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return aufgeloest.StartsWith(rootMitTrenner, StringComparison.OrdinalIgnoreCase)
                ? aufgeloest
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void SchliesseBlattAb(
        IXLWorksheet ws, Project project, string blatt,
        int headerRow, int startRow, int letzteDatenzeile, int spaltenzahl)
    {
        var kontext = ExcelReportContextFactory.AusProjekt(project, schaechte: blatt == "Schächte");
        ws.Cell(ExcelVorlagenLayout.TitelZeile, 1).Value = kontext.TitelFuer(blatt);
        StelleLogoGroesseWiederHer(ws);

        if (letzteDatenzeile >= startRow)
            ws.Range(headerRow, 1, letzteDatenzeile, spaltenzahl).SetAutoFilter();
    }

    /// <summary>
    /// ClosedXML setzt Bilder beim Speichern auf ihre native Pixelgroesse
    /// zurueck. Aus dem 5,6 cm breiten Logo der Vorlage wurden dadurch 8,7 cm,
    /// und es lag ueber der Legende. Deshalb hier erneut setzen.
    /// </summary>
    private static void StelleLogoGroesseWiederHer(IXLWorksheet ws)
    {
        foreach (var bild in ws.Pictures)
        {
            bild.Width = ExcelVorlagenLayout.LogoBreitePixel;
            bild.Height = ExcelVorlagenLayout.LogoHoehePixel;
        }
    }

    private static Dictionary<string, int> BaueFeldZuordnung(Dictionary<string, int> headerToCol)
    {
        var alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Haltungsnahme (ID)"] = FieldKeys.HoldingName,
            // Die UI nennt das Feld genauer "Lichte Hoehe / DN mm". Die bestehende
            // Kunden-Excelvorlage bleibt unveraendert bei "DN mm".
            ["DN mm"] = FieldKeys.NominalDiameterMm,
            ["Fliessrichtung"] = "Inspektionsrichtung"
        };

        var fieldToCol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in headerToCol)
        {
            var header = kv.Key.Trim();
            var normalizedHeader = NormalizeHeader(header);

            if (alias.TryGetValue(header, out var fieldFromAlias))
            {
                fieldToCol[fieldFromAlias] = kv.Value;
                continue;
            }

            var match = FieldCatalog.Definitions.FirstOrDefault(d =>
                string.Equals(NormalizeHeader(d.Value.Label), normalizedHeader, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeHeader(d.Key), normalizedHeader, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match.Key))
                fieldToCol[match.Key] = kv.Value;
        }

        return fieldToCol;
    }

    private static int FindeSpalte(Dictionary<string, int> headerToCol, string teil)
    {
        foreach (var kv in headerToCol)
        {
            if (NormalizeHeader(kv.Key).Contains(teil, StringComparison.Ordinal))
                return kv.Value;
        }
        return 0;
    }

    private static void SchreibeZahl(
        IXLWorksheet ws,
        int row,
        int col,
        int headerRow,
        string? value)
    {
        var zelle = ws.Cell(row, col);
        LeereZelle(zelle);
        var s = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s))
            return;

        if (TryParseExcelNumber(s, out var d))
        {
            zelle.Value = d;
            return;
        }

        var spaltenname = ws.Cell(headerRow, col).GetString().Trim();
        var spaltenangabe = string.IsNullOrWhiteSpace(spaltenname)
            ? zelle.Address.ColumnLetter
            : $"'{spaltenname}' ({zelle.Address.ColumnLetter})";
        throw new FormatException(
            $"Ungültiger Zahlenwert in Zeile {row}, Spalte {spaltenangabe}.");
    }

    private static void SchreibeText(IXLCell zelle, string? value)
    {
        LeereZelle(zelle);
        var text = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        zelle.Value = text;
    }

    private static void LeereZelle(IXLCell zelle)
    {
        if (zelle.HasHyperlink)
            zelle.GetHyperlink().Delete();

        zelle.Clear(XLClearOptions.Contents);
    }

    private static bool TryParseExcelNumber(string? value, out double result)
        => ExcelCellFormatting.TryParseExcelNumber(value, out result);

    private static int? TryInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var v) ? v : null;

    private static Dictionary<string, int> ReadHeaderColumns(IXLWorksheet ws, int headerRow)
    {
        var headerToCol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastHeaderCell = ws.Row(headerRow).LastCellUsed();
        var lastCol = lastHeaderCell?.Address.ColumnNumber ?? 1;

        for (int c = 1; c <= lastCol; c++)
        {
            var h = ws.Cell(headerRow, c).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(h) && !headerToCol.ContainsKey(h))
                headerToCol[h] = c;
        }

        return headerToCol;
    }

    private static string NormalizeHeader(string? text)
        => ExcelCellFormatting.NormalizeHeader(text);
}
