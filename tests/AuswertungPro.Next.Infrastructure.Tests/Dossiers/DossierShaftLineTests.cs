using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Schachtzeilen des Dossier-Cockpits. Anders als bei den Leitungen stehen
/// Massnahme und Kosten eines Schachts nicht im Projektdatensatz, sondern in
/// den Kostendateien.
/// </summary>
public sealed class DossierShaftLineTests
{
    private static SchachtRecord Schacht(string nummer, string funktion = "", string strasse = "")
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        if (funktion.Length > 0)
            record.SetFieldValue("Funktion", funktion);
        if (strasse.Length > 0)
            record.SetFieldValue(FieldKeys.Street, strasse);
        return record;
    }

    private static HoldingCost Kosten(string nummer, decimal total, params string[] zeilen)
        => new()
        {
            Holding = nummer,
            Total = total,
            Measures =
            {
                new MeasureCost
                {
                    MeasureId = "SCHACHT_EMPFEHLUNG",
                    MeasureName = "Empfohlene Massnahmen",
                    Total = total,
                    Lines = zeilen
                        .Select(text => new CostLine { Text = text, Qty = 1, Selected = true })
                        .ToList()
                }
            }
        };

    [Fact]
    public void Die_Zeile_zeigt_die_Funktion_des_Schachts()
    {
        var project = new Project();
        project.SchaechteData.Add(Schacht("80551", funktion: "Kontrollschacht"));

        var dossier = new DossierDefinition { ShaftNumbers = { "80551" } };

        var zeile = Assert.Single(
            DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore()).Shafts);

        Assert.Equal("80551", zeile.Number);
        Assert.Equal("Kontrollschacht", zeile.Funktion);
    }

    [Fact]
    public void Die_Massnahme_nennt_die_einzelnen_Arbeiten()
    {
        // "Empfohlene Massnahmen" ist nur der Gruppenname des Dialogs. Der
        // Eigentuemer soll lesen, WAS gemacht wird.
        var project = new Project();
        project.SchaechteData.Add(Schacht("80551"));

        var kosten = new ProjectCostStore();
        kosten.ByHolding["80551"] = Kosten(
            "80551", 1100m, "Schachthals sanieren", "Schachtrohr sanieren", "Fugen sanieren");

        var dossier = new DossierDefinition { ShaftNumbers = { "80551" } };

        var zeile = Assert.Single(
            DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore(), kosten).Shafts);

        Assert.Equal(
            "Schachthals sanieren; Schachtrohr sanieren; Fugen sanieren",
            zeile.Measures);
        Assert.Equal(1100m, zeile.NetCost);
    }

    [Fact]
    public void Ohne_Zeilentexte_bleibt_der_Name_der_Massnahme()
    {
        var project = new Project();
        project.SchaechteData.Add(Schacht("80551"));

        var kosten = new ProjectCostStore();
        kosten.ByHolding["80551"] = Kosten("80551", 500m);

        var dossier = new DossierDefinition { ShaftNumbers = { "80551" } };

        var zeile = Assert.Single(
            DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore(), kosten).Shafts);

        Assert.Equal("Empfohlene Massnahmen", zeile.Measures);
    }

    [Fact]
    public void Nicht_gewaehlte_Kostenzeilen_stehen_nicht_in_der_Massnahme()
    {
        var project = new Project();
        project.SchaechteData.Add(Schacht("80551"));

        var kosten = new ProjectCostStore();
        var eintrag = Kosten("80551", 400m, "Schachthals sanieren");
        eintrag.Measures[0].Lines.Add(
            new CostLine { Text = "Deckel ersetzen", Selected = false });
        kosten.ByHolding["80551"] = eintrag;

        var dossier = new DossierDefinition { ShaftNumbers = { "80551" } };

        var zeile = Assert.Single(
            DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore(), kosten).Shafts);

        Assert.Equal("Schachthals sanieren", zeile.Measures);
    }

    [Fact]
    public void Ein_Schacht_ohne_Schachtnummernfeld_wird_ueber_die_laufende_Nummer_gefunden()
    {
        // Dieselbe Regel wie im Auswahlfenster — sonst waehlt man einen Schacht,
        // den die Tabelle danach nicht wiederfindet.
        var record = new SchachtRecord();
        record.SetFieldValue("NR.", "12");

        var project = new Project();
        project.SchaechteData.Add(record);

        var dossier = new DossierDefinition { ShaftNumbers = { "12" } };

        var zeile = Assert.Single(
            DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore()).Shafts);

        Assert.Equal("12", zeile.Number);
    }

    [Fact]
    public void Eine_Nummer_ohne_Schacht_im_Projekt_erscheint_nicht()
    {
        var dossier = new DossierDefinition { ShaftNumbers = { "gibt-es-nicht" } };

        var stand = DossierSnapshotBuilder.Build(dossier, new Project(), new ProjectCostStore());

        Assert.Empty(stand.Shafts);
    }

    [Fact]
    public void Die_Schachtkosten_veraendern_die_Kennzahlen_der_Leitungen_nicht()
    {
        // Die Kachel "Sanierungskosten" zaehlt weiterhin nur Leitungen.
        var project = new Project();
        project.SchaechteData.Add(Schacht("80551"));

        var kosten = new ProjectCostStore();
        kosten.ByHolding["80551"] = Kosten("80551", 1100m, "Schachthals sanieren");

        var dossier = new DossierDefinition { ShaftNumbers = { "80551" } };

        var stand = DossierSnapshotBuilder.Build(
            dossier, project, new ProjectCostStore(), kosten);

        Assert.Equal(0m, stand.NetCostTotal);
        Assert.Equal(0, stand.Statistics.DringendCount);
    }
}

/// <summary>
/// Ein Schacht, den das Projekt nicht mehr kennt, darf nicht spurlos aus dem
/// Dossier verschwinden. Bei den Leitungen warnt das Cockpit seit jeher.
/// </summary>
public sealed class DossierMissingShaftTests
{
    [Fact]
    public void Eine_Nummer_ohne_Schacht_im_Projekt_wird_gemeldet()
    {
        var dossier = new DossierDefinition { ShaftNumbers = { "80551", "gibt-es-nicht" } };

        var project = new Project();
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "80551");
        project.SchaechteData.Add(record);

        var stand = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        Assert.Single(stand.Shafts);
        Assert.True(stand.HasMissingShafts);
        Assert.Equal(new[] { "gibt-es-nicht" }, stand.MissingShaftNumbers);
    }

    [Fact]
    public void Ohne_Fehlstelle_bleibt_die_Meldung_aus()
    {
        var dossier = new DossierDefinition { ShaftNumbers = { "80551" } };

        var project = new Project();
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "80551");
        project.SchaechteData.Add(record);

        var stand = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        Assert.False(stand.HasMissingShafts);
        Assert.Empty(stand.MissingShaftNumbers);
    }

    [Fact]
    public void Eine_leere_Nummer_gilt_nicht_als_Fehlstelle()
    {
        // Leerraum in der Liste ist kein verlorener Schacht, sondern nichts.
        var dossier = new DossierDefinition { ShaftNumbers = { "  " } };

        var stand = DossierSnapshotBuilder.Build(dossier, new Project(), new ProjectCostStore());

        Assert.False(stand.HasMissingShafts);
    }
}

/// <summary>
/// Eine von Hand bearbeitete Dossierdatei darf das Programm nicht zum Absturz
/// bringen. Die Umstellung fuellt leere Listen auf — bisher aber nur vier von
/// zehn, und die uebrigen sechs sind genau die, ueber die spaeter gelaufen wird.
/// </summary>
public sealed class DossierMigrationNullListTests
{
    [Fact]
    public void Leere_Listen_werden_bei_der_Umstellung_aufgefuellt()
    {
        var dokument = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Dossiers =
            {
                new DossierDefinition
                {
                    HoldingIds = null!,
                    ShaftNumbers = null!,
                    DismissedHoldingIds = null!,
                    DismissedShaftNumbers = null!,
                    HiddenChapters = null!,
                    FieldOverrides = null!,
                    TextOverrides = null!
                }
            }
        };

        var umgestellt = DossierDocumentMigration.MigrateToCurrent(dokument);
        var dossier = umgestellt.Dossiers[0];

        Assert.NotNull(dossier.HoldingIds);
        Assert.NotNull(dossier.ShaftNumbers);
        Assert.NotNull(dossier.DismissedHoldingIds);
        Assert.NotNull(dossier.DismissedShaftNumbers);
        Assert.NotNull(dossier.HiddenChapters);
        Assert.NotNull(dossier.FieldOverrides);
        Assert.NotNull(dossier.TextOverrides);
    }

    [Fact]
    public void Der_Stand_eines_so_bereinigten_Dossiers_laesst_sich_rechnen()
    {
        var dokument = new DossierDocument
        {
            SchemaVersion = DossierDocument.CurrentSchemaVersion,
            Dossiers = { new DossierDefinition { HoldingIds = null! } }
        };

        var dossier = DossierDocumentMigration.MigrateToCurrent(dokument).Dossiers[0];

        var stand = DossierSnapshotBuilder.Build(dossier, new Project(), new ProjectCostStore());

        Assert.Empty(stand.Holdings);
    }
}
