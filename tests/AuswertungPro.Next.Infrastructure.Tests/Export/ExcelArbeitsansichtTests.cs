using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

public sealed class ExcelArbeitsansichtTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Filter_aendert_Auswahlsummen_aber_nicht_Projektuebersicht(bool schaechte)
    {
        using var ausgabe = Export(schaechte);
        using var wb = new XLWorkbook(ausgabe.Path);
        var ws = wb.Worksheet(1);
        wb.RecalculateAllFormulas();
        Assert.Equal(2d, ws.Cell("C24").GetDouble());
        Assert.Equal(300d, ws.Cell("H24").GetDouble());
        if (!schaechte) Assert.Equal(30d, ws.Cell("E24").GetDouble());

        ws.AutoFilter.Column(schaechte ? 3 : 2).AddFilter("001");
        wb.RecalculateAllFormulas();
        Assert.False(ws.Row(27).IsHidden);
        Assert.True(ws.Row(28).IsHidden);
        Assert.Equal(1d, ws.Cell("C24").GetDouble());
        Assert.Equal(100d, ws.Cell("H24").GetDouble());
        if (!schaechte) Assert.Equal(10d, ws.Cell("E24").GetDouble());
        Assert.Equal(2d, wb.Worksheet(1).Cell("C23").GetDouble());
        ws.AutoFilter.Clear();
        wb.RecalculateAllFormulas();
        Assert.Equal(300d, ws.Cell("H24").GetDouble());
    }

    [Theory]
    [InlineData(false, 27, 2)]
    [InlineData(true, 17, 3)]
    public void Alle_Spalten_bleiben_und_Kennungen_sind_beim_Scrollen_und_Drucken_fest(
        bool schaechte, int spalten, int kennungen)
    {
        using var ausgabe = Export(schaechte);
        using var wb = new XLWorkbook(ausgabe.Path);
        var ws = wb.Worksheet(1);
        Assert.Equal(spalten, ws.Row(26).CellsUsed().Count());
        Assert.Single(wb.Worksheets);
        Assert.All(Enumerable.Range(1, 22), r => Assert.False(ws.Row(r).IsHidden));
        Assert.All(Enumerable.Range(23, 6), r => Assert.False(ws.Row(r).IsHidden));
        Assert.Equal(kennungen, ws.SheetView.SplitColumn);
        Assert.Equal(26, ws.SheetView.SplitRow);
        Assert.Equal(1, ws.PageSetup.PagesWide);
        Assert.Equal(0, ws.PageSetup.PagesTall);
        Assert.Equal(1, ws.PageSetup.FirstColumnToRepeatAtLeft);
        Assert.Equal(kennungen, ws.PageSetup.LastColumnToRepeatAtLeft);
        var druck = ws.PageSetup.PrintAreas.Single().RangeAddress;
        Assert.Equal(1, druck.FirstAddress.RowNumber);
        Assert.Equal(28, druck.LastAddress.RowNumber);
        Assert.Equal(spalten, druck.LastAddress.ColumnNumber);
        Assert.Contains("Synthetische Excel-Bedienprobe", ws.PageSetup.Header.Left.GetText(XLHFOccurrence.OddPages));
        using var zip = ZipFile.OpenRead(ausgabe.Path);
        using var stream = zip.GetEntry("xl/workbook.xml")!.Open();
        var titel = XDocument.Load(stream).Descendants()
            .Where(e => e.Name.LocalName == "definedName" && (string?)e.Attribute("name") == "_xlnm.Print_Titles")
            .Select(e => e.Value).ToArray();
        Assert.Contains(titel, t => t.Contains("!$A:$" + (schaechte ? "C" : "B")) && t.Contains("!$26:$26"));
        Assert.Single(titel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lange_Texte_bleiben_vollstaendig_in_der_einzigen_Tabelle(bool schaechte)
    {
        using var ausgabe = Export(schaechte);
        using var wb = new XLWorkbook(ausgabe.Path);
        var ws = wb.Worksheet(1);
        var quelle = ws.Cell(27, schaechte ? 5 : 9);
        var kennung = ws.Cell(27, schaechte ? 3 : 2);
        Assert.Equal(LangerText, quelle.GetString());
        Assert.False(kennung.HasHyperlink);
        Assert.Single(wb.Worksheets);
        Assert.True(quelle.Style.Alignment.WrapText);

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Leeres_Projekt_zeigt_null_und_erzeugt_keine_Phantomdaten(bool schaechte)
    {
        using var ausgabe = Export(schaechte, leer: true);
        using var wb = new XLWorkbook(ausgabe.Path);
        wb.RecalculateAllFormulas();
        Assert.Equal(0d, wb.Worksheet(1).Cell("C24").GetDouble());
        Assert.Equal(0d, wb.Worksheet(1).Cell("H24").GetDouble());
        Assert.Single(wb.Worksheets);
        Assert.Equal(26, wb.Worksheet(1).PageSetup.PrintAreas.Single().RangeAddress.LastAddress.RowNumber);
    }

    private static readonly string LangerText = string.Join("\n",
        Enumerable.Repeat("12.50 m: Harte Ablagerungen an der Rohrverbindung. Vollständiger Befund mit Umlauten äöü und Sonderzeichen.", 65));

    private static Ausgabe Export(bool schaechte, bool leer = false)
    {
        var projekt = new Project { Name = "Synthetische Excel-Bedienprobe" };
        if (!leer)
            for (var i = 1; i <= 2; i++)
            {
                var id = i.ToString("000");
                if (schaechte)
                {
                    var r = new SchachtRecord();
                    r.SetFieldValue("Schachtnummer", id);
                    r.SetFieldValue("Kosten", (i * 100).ToString());
                    r.SetFieldValue("Primäre Schäden", LangerText);
                    projekt.SchaechteData.Add(r);
                }
                else
                {
                    var r = new HaltungRecord();
                    void Set(string k, string v) => r.SetFieldValue(k, v, FieldSource.Manual, false);
                    Set(FieldKeys.HoldingName, id);
                    Set(FieldKeys.Cost, (i * 100).ToString());
                    Set(FieldKeys.HoldingLengthMeters, (i * 10).ToString());
                    Set(FieldKeys.PrimaryDamages, LangerText);
                    projekt.Data.Add(r);
                }
            }
        var ziel = new Ausgabe();
        var vorlage = System.IO.Path.Combine(TestPaths.FindSolutionRoot(), "Export_Vorlage",
            schaechte ? "Schächte.xlsx" : "Haltungen.xlsx");
        var service = new ExcelTemplateExportService();
        var result = schaechte
            ? service.ExportSchaechteToTemplate(projekt, vorlage, ziel.Path, 26, 27)
            : service.ExportToTemplate(projekt, vorlage, ziel.Path, 26, 27);
        Assert.True(result.Ok, result.ErrorMessage);
        return ziel;
    }

    private sealed class Ausgabe : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"excel-ansicht-{Guid.NewGuid():N}.xlsx");
        public void Dispose() => File.Delete(Path);
    }
}
