using System.Text.Json;
using System.Runtime.Loader;
using System.Reflection;
using System.IO.Compression;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    var path = Path.GetFullPath(Path.Combine("tests/AuswertungPro.Next.Infrastructure.Tests/bin/Release/net10.0", name.Name + ".dll"));
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
var catalog = JsonSerializer.Deserialize<CostCatalog>(File.ReadAllText("src/AuswertungPro.Next.UI/Config/cost_catalog.json"), options)!.Items.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
var templates = JsonSerializer.Deserialize<MeasureTemplateCatalog>(File.ReadAllText("src/AuswertungPro.Next.UI/Config/measure_templates.json"), options)!.Measures.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
HoldingCost Line(string holding, string key, decimal qty, decimal price, string unit, int dn = 200) => new()
{
    Holding = holding, Measures = [new() { Dn = dn, Lines = [new() { ItemKey = key, Text = catalog[key].Name, Qty = qty, UnitPrice = price, Unit = unit, Selected = true }] }]
};
HoldingCost Build(string name, string measure, int dn, string length, decimal? qty = null, string? key = null)
{
    var r = new HaltungRecord();
    r.SetFieldValue("Haltungsname", name, FieldSource.Manual, true);
    r.SetFieldValue("DN_mm", dn.ToString(), FieldSource.Manual, true);
    r.SetFieldValue("Haltungslaenge_m", length, FieldSource.Manual, true);
    return HoldingMeasureFactory.Build(name, r, measure, templates, catalog, .081m, hauptarbeitMenge: qty, hauptarbeitItemKey: key)!;
}
var fraesen = Line("A", "VORARBEIT_FRAESEN", 10, 29, "m");
var roboter = Line("B", "HAUPTARBEIT_HINDERNISSE_ROBOTER", 3, catalog["HAUPTARBEIT_HINDERNISSE_ROBOTER"].Price!.Value, "h", 400);
var result = new Dictionary<string, object?>();
result["fraesen_zuerst"] = ProjectPositionAggregator.Aggregate([fraesen, roboter], catalog);
result["roboter_zuerst"] = ProjectPositionAggregator.Aggregate([roboter, fraesen], catalog);
var doubledRate = new Dictionary<string, CostCatalogItem>(catalog, StringComparer.OrdinalIgnoreCase);
doubledRate["HAUPTARBEIT_HINDERNISSE_ROBOTER"] = catalog["HAUPTARBEIT_HINDERNISSE_ROBOTER"] with { Price = catalog["HAUPTARBEIT_HINDERNISSE_ROBOTER"].Price * 2 };
result["fraesen_stundensatz_verdoppelt"] = ProjectPositionAggregator.Aggregate([fraesen], doubledRate);
result["nullpreis_gemischt"] = ProjectPositionAggregator.Aggregate([Line("A", "VORARBEIT_REINIGUNG", 10, 5, "m"), Line("B", "VORARBEIT_REINIGUNG", 10, 0, "m")], catalog);
var liner = Build("L400", "SCHLAUCHLINER_NADELFILZ", 400, "20");
result["liner_dn400"] = ProjectPositionAggregator.Aggregate([liner], catalog);
result["installation_zwei_haltungen"] = ProjectPositionAggregator.Aggregate([liner, Build("L200", "SCHLAUCHLINER_NADELFILZ", 200, "10")], catalog).Where(x => x.ItemKey.StartsWith("INSTALL")).ToArray();
result["schachtliner_menge3"] = SchachtMeasureFactory.Build("S1", "SCHACHT_LINER", templates, catalog, .081m, hauptarbeitMenge: 3, hauptarbeitItemKey: "SCHACHT_LINER_EINBAUEN");
result["reinigung_tv_menge3"] = Build("RT", "KANALREINIGUNG_TV", 1000, "100", 3, "HAUPTARBEIT_REINIGUNG_KANAL");
result["liner_dn150"] = Build("L150", "SCHLAUCHLINER_NADELFILZ", 150, "20");
result["fehlende_template_schluessel"] = templates.Values.SelectMany(t => t.Lines.Where(l => !catalog.ContainsKey(l.ItemKey)).Select(l => t.Id + ":" + l.ItemKey)).ToArray();
var zeroPositions = (IReadOnlyList<AggregatedPosition>)result["nullpreis_gemischt"]!;
result["nullpreis_csv"] = NpkLeistungsverzeichnisExporter.BuildCsv(zeroPositions, projectName: "Auditprobe Nullpreis");
result["nullpreis_pdf_modell"] = NpkOfferPdfModelFactory.Create(zeroPositions, new NpkOfferPdfContext(), DateTimeOffset.Now).PositionLines;
var workbook = new NpkLeistungsverzeichnisExcelExportService().BuildWorkbook(zeroPositions, projectName: "Auditprobe Nullpreis");
File.WriteAllBytes(".tmp/npk-audit/probe-nullpreis.xlsx", workbook);
using (var zip = new ZipArchive(new MemoryStream(workbook)))
{
    using var stream = zip.GetEntry("xl/worksheets/sheet2.xml")!.Open();
    var xml = XDocument.Load(stream);
    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    result["excel_nullpreis_datenzeile"] = xml.Descendants(ns + "c").Where(c => new[] { "E9", "G9", "H9" }.Contains((string?)c.Attribute("r"))).Select(c => c.ToString()).ToArray();
}
File.WriteAllText(".tmp/npk-audit/probe-ergebnis.json", JsonSerializer.Serialize(result, options));
File.WriteAllText(".tmp/npk-audit/probe-liner-dn400.csv", NpkLeistungsverzeichnisExporter.BuildCsv(ProjectPositionAggregator.Aggregate([liner], catalog), projectName: "Synthetische Auditprobe DN400"));
Console.WriteLine(JsonSerializer.Serialize(result["excel_nullpreis_datenzeile"], options));
