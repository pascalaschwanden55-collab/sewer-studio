using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

/// <summary>
/// Das Aussehen des Berichts liegt in der Vorlage: Diagramme, Logo, bedingte
/// Formatierung, Formeln. Der Export darf davon nichts verlieren.
///
/// Der Test zaehlt praefix-unabhaengig. Beim Bau dieser Funktion hatte eine
/// Suche nach den Tags OHNE Namensraum-Praefix faelschlich "alles verloren"
/// gemeldet - ClosedXML schreibt sie als &lt;x:cfRule&gt;.
/// </summary>
public sealed class ExcelExportVorlagentreueTests
{
    private static string VorlageHaltungen()
        => Path.Combine(TestPaths.FindSolutionRoot(), "Export_Vorlage", "Haltungen.xlsx");

    private static string VorlageSchaechte()
        => Path.Combine(TestPaths.FindSolutionRoot(), "Export_Vorlage", "Schächte.xlsx");

    private static Project BaueProjekt(params (string Name, string Massnahmen)[] zeilen)
    {
        var project = new Project { Name = "Altdorf Zone 1.15" };
        foreach (var (name, massnahmen) in zeilen)
        {
            var rec = new HaltungRecord();
            rec.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, userEdited: false);
            rec.SetFieldValue(FieldKeys.ConditionClass, "2", FieldSource.Manual, userEdited: false);
            rec.SetFieldValue(FieldKeys.Owner, "AWU", FieldSource.Manual, userEdited: false);
            rec.SetFieldValue(FieldKeys.Cost, "1234.50", FieldSource.Manual, userEdited: false);
            rec.SetFieldValue(FieldKeys.InspectionYear, "24.09.2025", FieldSource.Manual, userEdited: false);
            rec.SetFieldValue(FieldKeys.RecommendedRehabilitationMeasures, massnahmen,
                FieldSource.Manual, userEdited: false);
            project.Data.Add(rec);
        }
        return project;
    }

    private static string Exportiere(Project project)
    {
        var ziel = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.xlsx");
        var ergebnis = new ExcelTemplateExportService().ExportToTemplate(
            project, VorlageHaltungen(), ziel,
            ExcelVorlagenLayout.KopfZeile, ExcelVorlagenLayout.ErsteDatenZeile);
        Assert.True(ergebnis.Ok, $"{ergebnis.ErrorCode}: {ergebnis.ErrorMessage}");
        return ziel;
    }

    private static string ExportiereSchaechte()
    {
        var project = new Project { Name = "Altdorf Zone 1.15" };
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "S-001");
        schacht.SetFieldValue("Zustandsklasse", "2");
        schacht.SetFieldValue("Eigentuemer", "AWU");
        schacht.SetFieldValue("Ausgefuehrt_durch", "Baumeister");
        project.SchaechteData.Add(schacht);

        var ziel = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.xlsx");
        var ergebnis = new ExcelTemplateExportService().ExportSchaechteToTemplate(
            project,
            VorlageSchaechte(),
            ziel,
            ExcelVorlagenLayout.KopfZeile,
            ExcelVorlagenLayout.ErsteDatenZeile);
        Assert.True(ergebnis.Ok, $"{ergebnis.ErrorCode}: {ergebnis.ErrorMessage}");
        return ziel;
    }

    private sealed record Bestand(
        int Diagramme,
        int Bilder,
        int Regeln,
        int Farbformate,
        int Formeln,
        string[] Formeltexte,
        string[] Diagrammvertrag,
        string[] BildHashes,
        string[] BedingteFormatierung,
        string[] BlattansichtUndDruck)
    {
        public static Bestand Lies(string pfad)
        {
            using var zip = ZipFile.OpenRead(pfad);
            var namen = zip.Entries.Select(e => e.FullName).ToList();
            var blatt = LiesXml(zip, "xl/worksheets/sheet1.xml");
            var arbeitsmappe = LiesXml(zip, "xl/workbook.xml");
            var stile = LiesXml(zip, "xl/styles.xml");

            return new Bestand(
                Diagramme: namen.Count(n => Regex.IsMatch(n, @"^xl/charts/chart\d+\.xml$")),
                Bilder: namen.Count(n => n.StartsWith("xl/media/", StringComparison.Ordinal)),
                Regeln: Treffer(zip, "xl/worksheets/sheet1.xml", @"<(?:\w+:)?cfRule\b"),
                Farbformate: Treffer(zip, "xl/styles.xml", @"<(?:\w+:)?dxf>"),
                Formeln: Treffer(zip, "xl/worksheets/sheet1.xml", @"<(?:\w+:)?f>"),
                Formeltexte: blatt.Descendants().Where(e => e.Name.LocalName == "f")
                    .Select(e => e.Value).ToArray(),
                Diagrammvertrag: zip.Entries
                    .Where(e => Regex.IsMatch(e.FullName, @"^xl/charts/chart\d+\.xml$"))
                    .OrderBy(e => e.FullName, StringComparer.Ordinal)
                    .Select(e => KanonischesXml(LiesXml(e).Root!))
                    .ToArray(),
                BildHashes: zip.Entries
                    .Where(e => e.FullName.StartsWith("xl/media/", StringComparison.Ordinal))
                    .OrderBy(e => e.FullName, StringComparer.Ordinal)
                    .Select(Hash)
                    .ToArray(),
                BedingteFormatierung: BedingteRegeln(blatt, stile),
                BlattansichtUndDruck: AnsichtUndDruck(blatt, arbeitsmappe));
        }

        private static string[] AnsichtUndDruck(XDocument blatt, XDocument arbeitsmappe)
        {
            var pane = blatt.Descendants().Single(e => e.Name.LocalName == "pane");
            var druckoptionen = blatt.Descendants().Single(e => e.Name.LocalName == "printOptions");
            var raender = blatt.Descendants().Single(e => e.Name.LocalName == "pageMargins");
            var seite = blatt.Descendants().Single(e => e.Name.LocalName == "pageSetup");
            var anpassen = blatt.Descendants().Single(e => e.Name.LocalName == "pageSetUpPr");
            var kopfFuss = blatt.Descendants().Single(e => e.Name.LocalName == "headerFooter");
            var berechnung = arbeitsmappe.Descendants().Single(e => e.Name.LocalName == "calcPr");

            return new[]
            {
                "freeze|" + Attribute(pane, "ySplit", "topLeftCell"),
                "printOptions|" + Attribute(druckoptionen, "horizontalCentered"),
                "margins|" + Attribute(raender, "left", "right", "top", "bottom", "header", "footer"),
                "page|" + Attribute(seite, "orientation", "paperSize", "fitToWidth", "fitToHeight"),
                "fit|" + Attribute(anpassen, "fitToPage"),
                "footer|" + string.Join("|", kopfFuss.Elements()
                    .Where(e => e.Name.LocalName is "oddHeader" or "oddFooter"
                        or "evenHeader" or "evenFooter" or "firstHeader" or "firstFooter")
                    .Select(e => $"{e.Name.LocalName}={e.Value}")),
                "titles|" + string.Join("|", arbeitsmappe.Descendants()
                    .Where(e => e.Name.LocalName == "definedName"
                        && (string?)e.Attribute("name") is "_xlnm.Print_Titles" or "_xlnm.Print_Area")
                    .Select(e => $"{(string?)e.Attribute("name")}={NormalisiereDruckbezug(e.Value)}")),
                "calc|" + Attribute(berechnung, "calcMode", "fullCalcOnLoad", "forceFullCalc")
            };
        }

        private static string Attribute(XElement element, params string[] namen)
            => string.Join("|", namen.Select(name =>
                $"{name}={(string?)element.Attribute(name) ?? string.Empty}"));

        private static string NormalisiereDruckbezug(string wert)
            => wert.Replace("'", string.Empty, StringComparison.Ordinal)
                .Replace("$", string.Empty, StringComparison.Ordinal);

        private static string[] BedingteRegeln(XDocument blatt, XDocument stile)
        {
            var farbformate = stile.Descendants().Where(e => e.Name.LocalName == "dxf").ToArray();
            return blatt.Descendants()
                .Where(e => e.Name.LocalName == "conditionalFormatting")
                .SelectMany(bereich => bereich.Elements()
                    .Where(e => e.Name.LocalName == "cfRule")
                    .Select(regel => string.Join("|", new[]
                    {
                        (string?)bereich.Attribute("sqref") ?? string.Empty,
                        (string?)regel.Attribute("type") ?? string.Empty,
                        string.Equals((string?)regel.Attribute("type"), "expression", StringComparison.Ordinal)
                            ? string.Empty
                            : (string?)regel.Attribute("operator") ?? string.Empty,
                        (string?)regel.Attribute("stopIfTrue") ?? string.Empty,
                        string.Join("~", regel.Elements()
                            .Where(e => e.Name.LocalName == "formula")
                            .Select(e => e.Value)),
                        Farbformat(regel, farbformate)
                    })))
                .ToArray();
        }

        private static string Farbformat(XElement regel, XElement[] farbformate)
        {
            if (!int.TryParse((string?)regel.Attribute("dxfId"), out var id)
                || id < 0
                || id >= farbformate.Length)
                return string.Empty;

            var format = farbformate[id];
            // openpyxl schreibt die Vollflaeche als fgColor und bgColor,
            // ClosedXML normalisiert denselben sichtbaren Dxf-Stil auf bgColor.
            var fuellung = format.Descendants().FirstOrDefault(e => e.Name.LocalName == "fgColor")
                ?.Attribute("rgb")?.Value
                ?? format.Descendants().FirstOrDefault(e => e.Name.LocalName == "bgColor")
                    ?.Attribute("rgb")?.Value
                ?? string.Empty;
            var schrift = format.Elements().FirstOrDefault(e => e.Name.LocalName == "font")
                ?.Descendants().FirstOrDefault(e => e.Name.LocalName == "color")
                ?.Attribute("rgb")?.Value ?? string.Empty;
            var fett = format.Elements().FirstOrDefault(e => e.Name.LocalName == "font")
                ?.Elements().Any(e => e.Name.LocalName == "b") == true;
            return $"{fuellung}/{schrift}/{fett}";
        }

        internal static XDocument LiesXml(ZipArchive zip, string teil)
            => LiesXml(zip.Entries.Single(e => e.FullName == teil));

        private static XDocument LiesXml(ZipArchiveEntry eintrag)
        {
            using var stream = eintrag.Open();
            return XDocument.Load(stream, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        }

        private static string KanonischesXml(XElement element)
            => new XElement(
                    element.Name.LocalName,
                    element.Attributes()
                        .Where(a => !a.IsNamespaceDeclaration)
                        .OrderBy(a => a.Name.LocalName, StringComparer.Ordinal)
                        .Select(a => new XAttribute(a.Name.LocalName, a.Value)),
                    element.Nodes()
                        .Where(n => n is not XText text || !string.IsNullOrWhiteSpace(text.Value))
                        .Select(KanonischerKnoten))
                .ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

        private static object KanonischerKnoten(XNode knoten)
            => knoten is XElement element
                ? XElement.Parse(
                    KanonischesXml(element),
                    System.Xml.Linq.LoadOptions.PreserveWhitespace)
                : knoten is XText text
                    ? text.Value
                    : knoten.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

        private static string Hash(ZipArchiveEntry eintrag)
        {
            using var stream = eintrag.Open();
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static int Treffer(ZipArchive zip, string teil, string muster)
        {
            var eintrag = zip.Entries.FirstOrDefault(e => e.FullName == teil);
            if (eintrag is null)
                return 0;

            using var leser = new StreamReader(eintrag.Open(), Encoding.UTF8);
            return Regex.Matches(leser.ReadToEnd(), muster).Count;
        }
    }

    [Fact]
    public void Export_verliert_weder_Diagramme_noch_Regeln_noch_Formeln()
    {
        var vorlage = Bestand.Lies(VorlageHaltungen());
        var ziel = Exportiere(BaueProjekt(("H-1", "Schlauchliner (GFK)")));

        try
        {
            var ergebnis = Bestand.Lies(ziel);

            Assert.True(vorlage.Diagramme >= 6, "Die Vorlage sollte sechs Diagramme tragen.");
            Assert.Equal(vorlage.Diagramme, ergebnis.Diagramme);
            Assert.Equal(vorlage.Bilder, ergebnis.Bilder);
            Assert.Equal(vorlage.Regeln, ergebnis.Regeln);
            Assert.Equal(vorlage.Farbformate, ergebnis.Farbformate);
            Assert.Equal(vorlage.Formeln, ergebnis.Formeln);
            Assert.Equal(vorlage.Formeltexte, ergebnis.Formeltexte);
            Assert.Equal(vorlage.Diagrammvertrag, ergebnis.Diagrammvertrag);
            Assert.Equal(vorlage.BildHashes, ergebnis.BildHashes);
            Assert.Equal(vorlage.BedingteFormatierung, ergebnis.BedingteFormatierung);
            Assert.Equal(vorlage.BlattansichtUndDruck, ergebnis.BlattansichtUndDruck);
        }
        finally
        {
            File.Delete(ziel);
        }
    }

    [Fact]
    public void Schachtexport_verliert_weder_Diagramme_noch_Regeln_noch_Formeln()
    {
        var vorlage = Bestand.Lies(VorlageSchaechte());
        var ziel = ExportiereSchaechte();

        try
        {
            var ergebnis = Bestand.Lies(ziel);

            Assert.True(vorlage.Diagramme >= 6, "Die Vorlage sollte sechs Diagramme tragen.");
            Assert.Equal(vorlage.Diagramme, ergebnis.Diagramme);
            Assert.Equal(vorlage.Bilder, ergebnis.Bilder);
            Assert.Equal(vorlage.Regeln, ergebnis.Regeln);
            Assert.Equal(vorlage.Farbformate, ergebnis.Farbformate);
            Assert.Equal(vorlage.Formeln, ergebnis.Formeln);
            Assert.Equal(vorlage.Formeltexte, ergebnis.Formeltexte);
            Assert.Equal(vorlage.Diagrammvertrag, ergebnis.Diagrammvertrag);
            Assert.Equal(vorlage.BildHashes, ergebnis.BildHashes);
            Assert.Equal(vorlage.BedingteFormatierung, ergebnis.BedingteFormatierung);
            Assert.Equal(vorlage.BlattansichtUndDruck, ergebnis.BlattansichtUndDruck);
        }
        finally
        {
            File.Delete(ziel);
        }
    }

    [Theory]
    [InlineData("Haltungen.xlsx", "Haltungen", "Haltungsname (ID)", "Ausgeführt durch")]
    [InlineData("Schächte.xlsx", "Schaechte", "Schachtnummer", "Ausgeführt durch")]
    public void Vorlage_ist_datenfrei_und_hat_professionelle_Ueberschriften(
        string datei,
        string blatt,
        string fachlicheId,
        string ausfuehrung)
    {
        var pfad = Path.Combine(TestPaths.FindSolutionRoot(), "Export_Vorlage", datei);

        using (var wb = new XLWorkbook(pfad))
        {
            var ws = wb.Worksheet(blatt);
            var kopf = ws.Row(ExcelVorlagenLayout.KopfZeile).CellsUsed()
                .Select(c => c.GetString()).ToArray();

            Assert.Contains(fachlicheId, kopf);
            Assert.Contains(ausfuehrung, kopf);
            Assert.Empty(ws.CellsUsed(XLCellsUsedOptions.Contents)
                .Where(c => c.Address.RowNumber >= ExcelVorlagenLayout.ErsteDatenZeile));
        }

        using var zip = ZipFile.OpenRead(pfad);
        Assert.DoesNotContain(zip.Entries, e =>
            e.FullName.Equals("xl/sharedStrings.xml", StringComparison.Ordinal));

        foreach (var eintrag in zip.Entries.Where(e =>
                     e.FullName.EndsWith(".xml", StringComparison.Ordinal)
                     || e.FullName.EndsWith(".rels", StringComparison.Ordinal)))
        {
            using var leser = new StreamReader(eintrag.Open(), Encoding.UTF8);
            var xml = leser.ReadToEnd();
            Assert.DoesNotContain("<hyperlink", xml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TargetMode=\"External\"", xml, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Haltungspruefung_zaehlt_und_faerbt_beide_Wertefamilien_ohne_Umschreiben()
    {
        using var zip = ZipFile.OpenRead(VorlageHaltungen());
        var blatt = Bestand.LiesXml(zip, "xl/worksheets/sheet1.xml");
        var formeln = blatt.Descendants().Where(e => e.Name.LocalName == "f")
            .Select(e => e.Value).ToArray();
        var farbformeln = blatt.Descendants().Where(e => e.Name.LocalName == "formula")
            .Select(e => e.Value).ToArray();
        var werte = new[]
        {
            "i.O.",
            "beobachten",
            "Sanierungsbedarf",
            "Prüfung bestanden",
            "Prüfung knapp nicht bestanden",
            "Prüfung nicht bestanden (grob undicht)",
            "Pruefung bestanden",
            "Pruefung knapp nicht bestanden",
            "Pruefung nicht bestanden (grob undicht)",
            "Keine"
        };

        foreach (var wert in werte)
        {
            Assert.Contains(formeln, f =>
                f.Contains($"COUNTIF($K$27:$K$5000,\"{wert}\")", StringComparison.Ordinal));
            Assert.Contains(farbformeln, f =>
                f.Contains($"$K27=\"{wert}\"", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Kostenblock_enthaelt_jeden_aufgefuehrten_Eigentuemer()
    {
        using var zip = ZipFile.OpenRead(VorlageHaltungen());
        var blatt = Bestand.LiesXml(zip, "xl/worksheets/sheet1.xml");
        var formeln = blatt.Descendants().Where(e => e.Name.LocalName == "f")
            .Select(e => e.Value).ToArray();

        foreach (var eigentuemer in ExcelReportStyle.Eigentuemer)
        {
            Assert.Contains(
                $"SUMIF($O$27:$O$5000,\"{eigentuemer.Wert}\",$N$27:$N$5000)",
                formeln);
        }
    }

    [Fact]
    public void Bedeutungsfarben_der_Haltungsvorlage_stimmen_mit_dem_Laufzeitvertrag_ueberein()
    {
        using var zip = ZipFile.OpenRead(VorlageHaltungen());
        var blatt = Bestand.LiesXml(zip, "xl/worksheets/sheet1.xml");
        var stile = Bestand.LiesXml(zip, "xl/styles.xml");
        var farbformate = stile.Descendants().Where(e => e.Name.LocalName == "dxf").ToArray();

        PruefeFarbregeln(blatt, farbformate, "$J27", ExcelReportStyle.Zustandsklassen);
        PruefeFarbregeln(blatt, farbformate, "$K27", ExcelReportStyle.Pruefungsresultate);
        PruefeFarbregeln(blatt, farbformate, "$O27", ExcelReportStyle.Eigentuemer);
        PruefeFarbregeln(blatt, farbformate, "$Z27", ExcelReportStyle.Status);
    }

    private static void PruefeFarbregeln(
        XDocument blatt,
        XElement[] farbformate,
        string zelle,
        System.Collections.Generic.IEnumerable<ExcelFarbregel> erwartungen)
    {
        foreach (var erwartet in erwartungen)
        {
            var regel = blatt.Descendants().Single(e =>
                e.Name.LocalName == "cfRule"
                && e.Elements().Any(f => f.Name.LocalName == "formula"
                    && f.Value.Contains($"{zelle}=\"{erwartet.Wert}\"", StringComparison.Ordinal)));
            var dxfId = int.Parse((string?)regel.Attribute("dxfId") ?? "-1");
            var farbe = farbformate[dxfId].Descendants()
                .First(e => e.Name.LocalName == "fgColor")
                .Attribute("rgb")?.Value;

            Assert.Equal(erwartet.Farbe, farbe);
        }
    }

    [Theory]
    [InlineData("Haltungen.xlsx")]
    [InlineData("Schächte.xlsx")]
    public void Vorlage_verlangt_beim_Oeffnen_eine_vollstaendige_Neuberechnung(string datei)
    {
        var pfad = Path.Combine(TestPaths.FindSolutionRoot(), "Export_Vorlage", datei);
        using var zip = ZipFile.OpenRead(pfad);
        var arbeitsmappe = Bestand.LiesXml(zip, "xl/workbook.xml");
        var berechnung = arbeitsmappe.Descendants().Single(e => e.Name.LocalName == "calcPr");

        Assert.Equal("auto", (string?)berechnung.Attribute("calcMode"));
        Assert.Equal("1", (string?)berechnung.Attribute("fullCalcOnLoad"));
        Assert.Equal("1", (string?)berechnung.Attribute("forceFullCalc"));
    }

    [Fact]
    public void Titel_kommt_aus_dem_Projekt_und_nicht_aus_der_Vorlage()
    {
        var ziel = Exportiere(BaueProjekt(("H-1", "Fräsen")));
        try
        {
            using var wb = new XLWorkbook(ziel);
            var titel = wb.Worksheet(1).Cell(ExcelVorlagenLayout.TitelZeile, 1).GetString();

            Assert.Contains("Altdorf Zone 1.15", titel, StringComparison.Ordinal);
            Assert.Contains("2025", titel, StringComparison.Ordinal);
            Assert.Contains("Haltungen", titel, StringComparison.Ordinal);
            Assert.DoesNotContain("Klausenstrasse", titel, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(ziel);
        }
    }

    [Fact]
    public void Zeilenhoehe_folgt_den_empfohlenen_Massnahmen()
    {
        var kurz = "Fräsen";
        var lang = string.Join("\n", Enumerable.Repeat("Linerendmanschette (LEM) / Schachtanbindung", 5));

        var ziel = Exportiere(BaueProjekt(("H-1", kurz), ("H-2", lang)));
        try
        {
            using var wb = new XLWorkbook(ziel);
            var ws = wb.Worksheet(1);
            var erste = ws.Row(ExcelVorlagenLayout.ErsteDatenZeile).Height;
            var zweite = ws.Row(ExcelVorlagenLayout.ErsteDatenZeile + 1).Height;

            Assert.True(zweite > erste,
                $"Die Zeile mit fuenf Massnahmen muss hoeher sein (kurz={erste}, lang={zweite}).");
            Assert.True(zweite <= ExcelZeilenhoehe.Hoechsthoehe);
        }
        finally
        {
            File.Delete(ziel);
        }
    }

    [Fact]
    public void Weitere_Zeilen_erben_den_Stil_der_Musterzeile()
    {
        var ziel = Exportiere(BaueProjekt(("H-1", "a"), ("H-2", "b"), ("H-3", "c")));
        try
        {
            using var wb = new XLWorkbook(ziel);
            var ws = wb.Worksheet(1);
            var muster = ws.Cell(ExcelVorlagenLayout.ErsteDatenZeile, 14);
            var dritte = ws.Cell(ExcelVorlagenLayout.ErsteDatenZeile + 2, 14);

            Assert.Equal(muster.Style.NumberFormat.Format, dritte.Style.NumberFormat.Format);
            Assert.Equal(muster.Style.Alignment.Horizontal, dritte.Style.Alignment.Horizontal);
            Assert.Equal(muster.Style.Font.FontName, dritte.Style.Font.FontName);
        }
        finally
        {
            File.Delete(ziel);
        }
    }

    [Fact]
    public void Logo_behaelt_seine_Groesse()
    {
        // ClosedXML setzt Bilder beim Speichern auf ihre native Pixelgroesse
        // zurueck. Aus 5,6 cm wurden 8,7 cm und das Logo lag ueber der Legende.
        var ziel = Exportiere(BaueProjekt(("H-1", "a")));
        try
        {
            using var wb = new XLWorkbook(ziel);
            var bild = wb.Worksheet(1).Pictures.FirstOrDefault();

            Assert.NotNull(bild);
            Assert.Equal(ExcelVorlagenLayout.LogoBreitePixel, bild!.Width);
            Assert.Equal(ExcelVorlagenLayout.LogoHoehePixel, bild.Height);
        }
        finally
        {
            File.Delete(ziel);
        }
    }

    [Fact]
    public void Zustandsklasse_wird_auch_als_Text_gezaehlt_und_gefaerbt()
    {
        // Der Export schreibt die Zustandsklasse als Text ("2"), von Hand
        // getippt waere sie eine Zahl. Excel wandelt beim Vergleich nicht um.
        // Ohne diese Schreibweise blieben Kennzahlen und Balken auf null -
        // genau das war im alten Export der Fall.
        using var zip = ZipFile.OpenRead(VorlageHaltungen());

        var blatt = zip.Entries.First(e => e.FullName == "xl/worksheets/sheet1.xml");
        using var leser = new StreamReader(blatt.Open(), Encoding.UTF8);
        var xml = leser.ReadToEnd();

        // Zaehlkriterium in Anfuehrungszeichen trifft Zahl UND Text.
        Assert.Contains("COUNTIF($J$27:$J$5000,\"0\")", xml, StringComparison.Ordinal);

        // Farbregel prueft beide Formen und schliesst leere Zellen aus
        // (im XML ist nur "<>" maskiert, die Anfuehrungszeichen nicht).
        Assert.Contains("OR($J27=0,$J27=\"0\")", xml, StringComparison.Ordinal);
        Assert.Contains("AND($J27&lt;&gt;\"\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Export_faerbt_selbst_nichts_ein()
    {
        // Die Ampelfarben muessen aus der bedingten Formatierung kommen. Eine
        // fest gesetzte Fuellung wuerde beim spaeteren Bearbeiten stehenbleiben
        // und den falschen Zustand behaupten.
        var ziel = Exportiere(BaueProjekt(("H-1", "a")));
        try
        {
            using var wb = new XLWorkbook(ziel);
            var ws = wb.Worksheet(1);
            var zustandsklasse = ws.Cell(ExcelVorlagenLayout.ErsteDatenZeile, 10);

            Assert.Equal(XLFillPatternValues.None, zustandsklasse.Style.Fill.PatternType);
        }
        finally
        {
            File.Delete(ziel);
        }
    }
}
