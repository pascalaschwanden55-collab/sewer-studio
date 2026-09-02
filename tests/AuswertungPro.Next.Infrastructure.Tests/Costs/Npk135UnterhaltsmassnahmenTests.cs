using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests.Costs;

/// <summary>
/// Prueft die NPK-135-Unterhaltsmassnahmen (Kanalreinigung, TV-Kontrolle) gegen die
/// AUSGELIEFERTEN Konfigurationsdateien unter <c>src/AuswertungPro.Next.UI/Config</c>.
///
/// Bewusst nicht gegen einen im Test gebauten Katalog: Der Fehler, den dieser Test
/// fangen soll, ist eine unvollstaendige oder falsch verdrahtete Katalogdatei. Ein
/// selbst gebauter Katalog koennte das nie zeigen.
///
/// Messlatte ist die reale Rechnung Fretz Kanal-Service AG Nr. 42990 vom 31.08.2026
/// (Objekt 26610455 Vorstadt, 6460 Altdorf UR), gerechnet OHNE die dort gewaehrten
/// 14 % Rabatt. Netto der Rechnung: 5 Positionen, zusammen 854.375 CHF.
/// </summary>
public sealed class Npk135UnterhaltsmassnahmenTests
{
    private const string KombiMassnahme = "KANALREINIGUNG_TV";
    private const string ReinigungMassnahme = "KANALREINIGUNG";
    private const string TvMassnahme = "TV_KONTROLLE";

    [Fact]
    public void KanalreinigungUndTvKontrolle_ErgibtDenNettobetragDerRechnung42990()
    {
        var (templates, catalog) = LoadShippedConfig();

        var holding = HoldingMeasureFactory.Build(
            "77471-77563",
            Record("77471-77563", dn: "250", laenge: "11.10"),
            KombiMassnahme,
            templates,
            catalog,
            vatRate: 0.081m);

        Assert.NotNull(holding);
        var measure = Assert.Single(holding!.Measures);
        var netto = measure.Lines.Where(l => l.Selected).Sum(l => l.Qty * l.UnitPrice);
        Assert.Equal(854.375m, decimal.Round(netto, 3));
    }

    [Theory]
    // Menge und Einzelpreis je Position, exakt wie auf der Rechnung ohne Rabatt.
    [InlineData("HAUPTARBEIT_REINIGUNG_KANAL", "211.112", "h", 1.00, 202.00)]
    [InlineData("HAUPTARBEIT_ROTIERDUESE", "211.312", "h", 0.25, 39.50)]
    [InlineData("HAUPTARBEIT_TV_FAHRWAGEN", "222.112", "h", 0.75, 206.50)]
    [InlineData("HAUPTARBEIT_FACHARBEITER_TV", "222.701", "h", 1.75, 83.50)]
    [InlineData("QK_BERICHT_SPEICHERMEDIUM", "234.102", "St", 1.00, 341.50)]
    public void KombiMassnahme_EnthaeltJedeRechnungspositionMitNpkCodeMengeUndPreis(
        string itemKey,
        string npkCode,
        string unit,
        double erwarteteMenge,
        double erwarteterPreis)
    {
        var (templates, catalog) = LoadShippedConfig();

        var holding = HoldingMeasureFactory.Build(
            "77471-77563",
            Record("77471-77563", dn: "250", laenge: "11.10"),
            KombiMassnahme,
            templates,
            catalog,
            vatRate: 0.081m);

        var line = Assert.Single(
            holding!.Measures.Single().Lines.Where(l =>
                string.Equals(l.ItemKey, itemKey, StringComparison.OrdinalIgnoreCase)));

        Assert.True(line.Selected, $"{itemKey} muss angehakt sein.");
        Assert.Equal(unit, line.Unit);
        Assert.Equal((decimal)erwarteteMenge, line.Qty);
        Assert.Equal((decimal)erwarteterPreis, line.UnitPrice);
        Assert.Equal(npkCode, catalog[itemKey].NpkCode);
    }

    [Theory]
    // NPK 135 verlangt die Einrichtung (Abschnitt 110); Fretz rechnet sie in die
    // Stundensaetze ein. Ohne belegten Preis bleiben beide auf 0.00 und ABGEWAEHLT,
    // damit keine erfundene Zahl in eine Offerte laeuft.
    [InlineData("INSTALL_REINIGUNG", "111.001")]
    [InlineData("INSTALL_ZUSTANDSERFASSUNG", "112.001")]
    public void Einrichtungspositionen_SindVorhandenAberOhnePreisUndAbgewaehlt(
        string itemKey,
        string npkCode)
    {
        var (templates, catalog) = LoadShippedConfig();

        var holding = HoldingMeasureFactory.Build(
            "77471-77563",
            Record("77471-77563", dn: "250", laenge: "11.10"),
            KombiMassnahme,
            templates,
            catalog,
            vatRate: 0.081m);

        var line = Assert.Single(
            holding!.Measures.Single().Lines.Where(l =>
                string.Equals(l.ItemKey, itemKey, StringComparison.OrdinalIgnoreCase)));

        Assert.False(line.Selected, $"{itemKey} darf ohne belegten Preis nicht angehakt sein.");
        Assert.Equal(0m, line.UnitPrice);
        Assert.Equal(npkCode, catalog[itemKey].NpkCode);
        Assert.Equal("Installation", line.Group);
    }

    [Theory]
    [InlineData(ReinigungMassnahme, "HAUPTARBEIT_REINIGUNG_KANAL", "HAUPTARBEIT_TV_FAHRWAGEN")]
    [InlineData(TvMassnahme, "HAUPTARBEIT_TV_FAHRWAGEN", "HAUPTARBEIT_REINIGUNG_KANAL")]
    public void EinzelmassnahmenEnthaltenNurIhreEigeneLeistung(
        string measureId,
        string erwarteterSchluessel,
        string nichtErwarteterSchluessel)
    {
        var (templates, catalog) = LoadShippedConfig();

        var holding = HoldingMeasureFactory.Build(
            "77471-77563",
            Record("77471-77563", dn: "250", laenge: "11.10"),
            measureId,
            templates,
            catalog,
            vatRate: 0.081m);

        Assert.NotNull(holding);
        var keys = holding!.Measures.Single().Lines
            .Where(l => l.Selected)
            .Select(l => l.ItemKey)
            .ToList();

        Assert.Contains(erwarteterSchluessel, keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(nichtErwarteterSchluessel, keys, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(KombiMassnahme)]
    [InlineData(ReinigungMassnahme)]
    [InlineData(TvMassnahme)]
    public void JedeUnterhaltsmassnahme_IstAlsMatrixOptionMitManuellerMengeWaehlbar(string measureId)
    {
        var (templates, catalog) = LoadShippedConfig();

        var options = MatrixMeasureOptionBuilder.Build(
            new[] { (measureId, "Unterhalt") },
            templates,
            catalog);

        var option = Assert.Single(options.Where(o =>
            string.Equals(o.Id, measureId, StringComparison.OrdinalIgnoreCase)));

        // Stundenpositionen: Die Matrix-Mengenspalte muss manuell bedienbar sein,
        // sonst wuerde sie die Haltungslaenge als Stundenzahl eintragen.
        Assert.True(option.ManuelleMenge, $"{measureId} muss eine manuelle Menge erlauben.");
        Assert.Equal("Unterhalt", option.Kategorie);
    }

    [Theory]
    // Die Haekchenspalten der Sanierungs-Matrix muessen auch bei Unterhalt greifen.
    [InlineData(KombiMassnahme, "VORARBEIT_VD")]
    [InlineData(KombiMassnahme, "VORARBEIT_WASSERHALTUNG")]
    [InlineData(ReinigungMassnahme, "VORARBEIT_VD")]
    [InlineData(TvMassnahme, "VORARBEIT_VD")]
    public void Zusatzoptionen_LiegenAlsAbgewaehlteZeilenBereit(string measureId, string itemKey)
    {
        var (templates, catalog) = LoadShippedConfig();

        var holding = HoldingMeasureFactory.Build(
            "77471-77563",
            Record("77471-77563", dn: "250", laenge: "11.10"),
            measureId,
            templates,
            catalog,
            vatRate: 0.081m);

        var line = Assert.Single(
            holding!.Measures.Single().Lines.Where(l =>
                string.Equals(l.ItemKey, itemKey, StringComparison.OrdinalIgnoreCase)));

        Assert.False(line.Selected);
    }

    private static HaltungRecord Record(string holding, string dn, string laenge)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: true);
        r.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: true);
        r.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: true);
        return r;
    }

    /// <summary>
    /// Laedt Katalog und Vorlagen aus den ausgelieferten Dateien des UI-Projekts.
    /// Der Umweg ueber einen Projektpfad ist der einzige Weg, den echten Store auf
    /// diese Dateien zu richten, ohne die Testablage zu veraendern.
    /// </summary>
    private static (Dictionary<string, MeasureTemplate> Templates, Dictionary<string, CostCatalogItem> Catalog)
        LoadShippedConfig()
    {
        var repoRoot = RepoRootLocator.Locate(AppContext.BaseDirectory);
        Assert.False(
            string.IsNullOrWhiteSpace(repoRoot),
            "Projektwurzel nicht gefunden; der Test kann die ausgelieferte Konfiguration nicht lesen.");

        var uiProjectDir = Path.Combine(repoRoot!, "src", "AuswertungPro.Next.UI");
        var configDir = Path.Combine(uiProjectDir, "Config");
        Assert.True(
            File.Exists(Path.Combine(configDir, "cost_catalog.json")),
            $"cost_catalog.json fehlt unter {configDir}.");

        // ResolvePath der Stores sucht "<Ordner der Projektdatei>\Config\<Datei>".
        var pseudoProjectFile = Path.Combine(uiProjectDir, "projekt.json");
        var overridePath = Path.Combine(Path.GetTempPath(), $"npk135-test-{Guid.NewGuid():N}.json");

        var catalog = new CostCatalogStore(overridePath).LoadDefault(pseudoProjectFile);
        var templates = new MeasureTemplateStore(overridePath).LoadDefault(pseudoProjectFile);

        Assert.NotEmpty(catalog.Items);
        Assert.NotEmpty(templates.Measures);

        return (
            templates.Measures.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase),
            catalog.Items.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase));
    }
}
