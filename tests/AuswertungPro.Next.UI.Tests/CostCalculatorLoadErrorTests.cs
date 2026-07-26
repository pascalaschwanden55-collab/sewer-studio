using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorLoadErrorTests
{
    [Theory]
    [InlineData("cost_catalog.json", "Kostenkatalog")]
    [InlineData("measure_templates.json", "Massnahmenvorlagen")]
    public void Beschaedigte_Berechnungsgrundlage_sperrt_Kostenspeicherung(
        string fileName,
        string expectedLabel)
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var configPath = Path.Combine(temp.Path, "Projektdateien", "Config", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "{ kaputt");
        var dialogs = new DialogFake();
        var viewModel = new CostCalculatorViewModel(
            holding: "H-1",
            date: null,
            recommendedTokens: [],
            projectPath: projectPath,
            catalogStore: new CostCatalogStore(Path.Combine(temp.Path, "cost_catalog.user.json")),
            templateStore: new MeasureTemplateStore(Path.Combine(temp.Path, "measure_templates.user.json")),
            costRepo: new ProjectCostStoreRepository(),
            dialogs: dialogs);

        Assert.Contains(expectedLabel, dialogs.LastError, StringComparison.Ordinal);

        dialogs.LastError = "";
        viewModel.SaveCommand.Execute(null);

        Assert.Contains("Speichern gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Projektdateien", "costs", "costs.json")));
    }

    [Fact]
    public void Ungueltige_nichtleere_Laenge_sperrt_Kostenspeicherung()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var configDirectory = Path.Combine(temp.Path, "Projektdateien", "Config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "cost_catalog.json"),
            """{"version":1,"currency":"CHF","vatRate":0.081,"items":[]}""");
        File.WriteAllText(
            Path.Combine(configDirectory, "measure_templates.json"),
            """{"version":1,"measures":[]}""");
        var dialogs = new DialogFake();
        var viewModel = new CostCalculatorViewModel(
            holding: "H-1",
            date: null,
            recommendedTokens: [],
            projectPath: projectPath,
            catalogStore: new CostCatalogStore(Path.Combine(temp.Path, "cost_catalog.user.json")),
            templateStore: new MeasureTemplateStore(Path.Combine(temp.Path, "measure_templates.user.json")),
            costRepo: new ProjectCostStoreRepository(),
            dialogs: dialogs);
        viewModel.SelectedMeasures.Add(new MeasureBlockVm(null, new Dictionary<string, AuswertungPro.Next.Domain.Models.CostCatalogItem>())
        {
            LengthText = "45'30"
        });

        viewModel.SaveCommand.Execute(null);

        Assert.Contains("Laenge", dialogs.LastError, StringComparison.Ordinal);
        Assert.Contains("Speichern gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Projektdateien", "costs", "costs.json")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    public void AusgewaehlteMeterzeile_OhnePositiveLaenge_SperrtKostenspeicherung(
        string lengthText)
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var configDirectory = Path.Combine(temp.Path, "Projektdateien", "Config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "cost_catalog.json"),
            """{"version":1,"currency":"CHF","vatRate":0.081,"items":[]}""");
        File.WriteAllText(
            Path.Combine(configDirectory, "measure_templates.json"),
            """{"version":1,"measures":[]}""");
        var dialogs = new DialogFake();
        var viewModel = new CostCalculatorViewModel(
            holding: "H-1",
            date: null,
            recommendedTokens: [],
            projectPath: projectPath,
            catalogStore: new CostCatalogStore(Path.Combine(temp.Path, "cost_catalog.user.json")),
            templateStore: new MeasureTemplateStore(Path.Combine(temp.Path, "measure_templates.user.json")),
            costRepo: new ProjectCostStoreRepository(),
            dialogs: dialogs);
        var measure = new MeasureBlockVm(null, new Dictionary<string, CostCatalogItem>());
        measure.LoadFrom(new MeasureCost
        {
            LengthMeters = 12m,
            Lines =
            [
                new CostLine
                {
                    ItemKey = "METER",
                    Text = "Meterposition",
                    Unit = "m",
                    Qty = 12m,
                    UnitPrice = 10m,
                    Selected = true
                }
            ]
        });
        measure.LengthText = lengthText;
        viewModel.SelectedMeasures.Add(measure);

        viewModel.SaveCommand.Execute(null);

        Assert.Contains("groesser als 0", dialogs.LastError, StringComparison.Ordinal);
        Assert.Contains("Speichern gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Projektdateien", "costs", "costs.json")));
    }

    [Theory]
    [InlineData(-1, 10, "Menge")]
    [InlineData(1, -10, "Preis")]
    public async Task AusgewaehlteNegativeMengeOderPreis_SperrtAlleAusgabepfade(
        int quantity,
        int unitPrice,
        string expectedField)
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var configDirectory = Path.Combine(temp.Path, "Projektdateien", "Config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "cost_catalog.json"),
            """{"version":1,"currency":"CHF","vatRate":0.081,"items":[]}""");
        File.WriteAllText(
            Path.Combine(configDirectory, "measure_templates.json"),
            """{"version":1,"measures":[]}""");
        var dialogs = new DialogFake();
        var applyCalls = 0;
        var viewModel = new CostCalculatorViewModel(
            holding: "H-1",
            date: null,
            recommendedTokens: [],
            projectPath: projectPath,
            catalogStore: new CostCatalogStore(Path.Combine(temp.Path, "cost_catalog.user.json")),
            templateStore: new MeasureTemplateStore(Path.Combine(temp.Path, "measure_templates.user.json")),
            costRepo: new ProjectCostStoreRepository(),
            applyTotal: _ => applyCalls++,
            dialogs: dialogs);
        var measure = new MeasureBlockVm(null, new Dictionary<string, CostCatalogItem>());
        measure.LoadFrom(new MeasureCost
        {
            Lines =
            [
                new CostLine
                {
                    ItemKey = "POSITION",
                    Text = "Position",
                    Unit = "Stk",
                    Qty = quantity,
                    UnitPrice = unitPrice,
                    Selected = true
                }
            ]
        });
        viewModel.SelectedMeasures.Add(measure);

        viewModel.SaveCommand.Execute(null);

        Assert.Contains("Speichern gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.Contains(expectedField, dialogs.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Projektdateien", "costs", "costs.json")));

        dialogs.LastError = "";
        viewModel.ApplyTotalCommand.Execute(null);

        Assert.Contains("Uebernahme gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.Contains(expectedField, dialogs.LastError, StringComparison.Ordinal);
        Assert.Equal(0, applyCalls);

        dialogs.LastError = "";
        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        Assert.Contains("PDF-Export gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.Contains(expectedField, dialogs.LastError, StringComparison.Ordinal);
        Assert.Equal(0, dialogs.SaveFileCalls);
    }

    private sealed class DialogFake : IDialogService
    {
        public string LastError { get; set; } = "";
        public int SaveFileCalls { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
        {
            SaveFileCalls++;
            return null;
        }
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => LastError = message;
        public bool Confirm(string message, string title = "Bestaetigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Yes;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "CostCalculatorLoadErrorTests_" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test-Aufraeumen ist nicht fachlich.
            }
        }
    }
}
