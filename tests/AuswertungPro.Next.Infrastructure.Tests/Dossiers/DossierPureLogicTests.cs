using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierFolderPlannerTests
{
    [Fact]
    public void PlanFolderName_ersetzt_ungueltige_Zeichen()
    {
        var name = DossierFolderPlanner.PlanFolderName("Brämenhofstatt 3+4/7", _ => false);

        Assert.DoesNotContain("/", name, StringComparison.Ordinal);
        Assert.Contains("Brämenhofstatt", name, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanFolderName_weicht_bei_Kollision_aus()
    {
        var belegt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Musterweg 12" };

        var name = DossierFolderPlanner.PlanFolderName("Musterweg 12", belegt.Contains);

        Assert.Equal("Musterweg 12-2", name);
    }

    [Fact]
    public void PlanFolderName_faengt_Punktsegmente_ab()
    {
        // ".." wuerde ueber Path.Combine aus dem Dossier-Ordner ausbrechen.
        var name = DossierFolderPlanner.PlanFolderName("..", _ => false);

        Assert.Equal("UNKNOWN", name);
    }

    [Fact]
    public void PlanFreeFileName_ueberschreibt_vorhandene_Datei_nie()
    {
        var belegt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Eigentuemerdossier.docx",
            "Eigentuemerdossier-2.docx"
        };

        var name = DossierFolderPlanner.PlanFreeFileName("Eigentuemerdossier.docx", belegt.Contains);

        Assert.Equal("Eigentuemerdossier-3.docx", name);
    }
}

public sealed class DossierFieldResolverTests
{
    [Fact]
    public void Gebietsangabe_gilt_wenn_das_Dossier_nichts_eigenes_hat()
    {
        var area = new DossierAreaSettings { ExecutionDate = "Herbst 2026" };
        var dossier = new DossierDefinition();

        var resolved = DossierFieldResolver.Resolve(area, dossier);

        Assert.Equal("Herbst 2026", resolved.ExecutionDate);
    }

    [Fact]
    public void Eigener_Wert_des_Dossiers_gewinnt()
    {
        var area = new DossierAreaSettings { ExecutionDate = "Herbst 2026" };
        var dossier = new DossierDefinition { ExecutionDateOverride = "Frühling 2027" };

        var resolved = DossierFieldResolver.Resolve(area, dossier);

        Assert.Equal("Frühling 2027", resolved.ExecutionDate);
    }

    [Fact]
    public void Nur_Leerzeichen_loescht_die_Gebietsangabe_nicht()
    {
        var area = new DossierAreaSettings { ContactPerson = "Abwasser Uri" };
        var dossier = new DossierDefinition { ContactPersonOverride = "   " };

        var resolved = DossierFieldResolver.Resolve(area, dossier);

        Assert.Equal("Abwasser Uri", resolved.ContactPerson);
    }
}

public sealed class DossierSnapshotBuilderTests
{
    private static HaltungRecord Holding(
        string name,
        string length = "41.70",
        string condition = "1",
        string street = "Brämenhofstatt")
    {
        var record = new HaltungRecord();
        record.Fields[FieldKeys.HoldingName] = name;
        record.Fields[FieldKeys.HoldingLengthMeters] = length;
        record.Fields[FieldKeys.ConditionClass] = condition;
        record.Fields[FieldKeys.Street] = street;
        return record;
    }

    [Fact]
    public void Nimmt_nur_die_ausgewaehlten_Haltungen_auf()
    {
        var a = Holding("36080-36086");
        var b = Holding("33850-7.25390");
        var fremd = Holding("99999-99998");

        var project = new Project();
        project.Data.Add(a);
        project.Data.Add(b);
        project.Data.Add(fremd);

        var dossier = new DossierDefinition { HoldingIds = { a.Id, b.Id } };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        Assert.Equal(2, snapshot.HoldingCount);
        Assert.DoesNotContain(snapshot.Holdings, h => h.HoldingName == "99999-99998");
    }

    [Fact]
    public void Behaelt_die_gespeicherte_Reihenfolge()
    {
        var a = Holding("A");
        var b = Holding("B");

        var project = new Project();
        project.Data.Add(a);
        project.Data.Add(b);

        var dossier = new DossierDefinition { HoldingIds = { b.Id, a.Id } };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        Assert.Equal(new[] { "B", "A" }, snapshot.Holdings.Select(h => h.HoldingName));
    }

    [Fact]
    public void Meldet_eine_geloeschte_Haltung_sichtbar_statt_sie_zu_schlucken()
    {
        var vorhanden = Holding("36080-36086");
        var verschwunden = Guid.NewGuid();

        var project = new Project();
        project.Data.Add(vorhanden);

        var dossier = new DossierDefinition { HoldingIds = { vorhanden.Id, verschwunden } };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        Assert.True(snapshot.HasMissingHoldings);
        Assert.Equal(verschwunden, Assert.Single(snapshot.MissingHoldingIds));
        Assert.Single(snapshot.Holdings);
    }

    [Fact]
    public void Umbenennen_einer_Haltung_zerstoert_das_Dossier_nicht()
    {
        var record = Holding("36080-36086");
        var project = new Project();
        project.Data.Add(record);

        var dossier = new DossierDefinition { HoldingIds = { record.Id } };

        record.Fields[FieldKeys.HoldingName] = "36080-36086-neu";

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        Assert.False(snapshot.HasMissingHoldings);
        Assert.Equal("36080-36086-neu", Assert.Single(snapshot.Holdings).HoldingName);
    }

    [Fact]
    public void Rechnet_nur_die_Kosten_der_eigenen_Haltungen()
    {
        var meine = Holding("36080-36086");
        var fremde = Holding("11111-22222");

        var project = new Project();
        project.Data.Add(meine);
        project.Data.Add(fremde);

        var costs = new ProjectCostStore();
        costs.ByHolding["36080-36086"] = new HoldingCost { Holding = "36080-36086", Total = 28_400m };
        costs.ByHolding["11111-22222"] = new HoldingCost { Holding = "11111-22222", Total = 99_000m };

        var dossier = new DossierDefinition { HoldingIds = { meine.Id } };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, costs);

        Assert.Equal(28_400m, snapshot.NetCostTotal);
        Assert.Equal(28_400m, snapshot.Statistics.HaltungSanierungsKosten);
    }

    [Fact]
    public void Gesamtlaenge_summiert_die_ausgewaehlten_Haltungen()
    {
        var a = Holding("A", length: "41.70");
        var b = Holding("B", length: "25.40");

        var project = new Project();
        project.Data.Add(a);
        project.Data.Add(b);

        var dossier = new DossierDefinition { HoldingIds = { a.Id, b.Id } };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        Assert.Equal(67.10, snapshot.LengthTotal, precision: 2);
    }

    [Fact]
    public void Leeres_Dossier_ergibt_leere_Kennzahlen_statt_Absturz()
    {
        var snapshot = DossierSnapshotBuilder.Build(
            new DossierDefinition(), new Project(), new ProjectCostStore());

        Assert.Equal(0, snapshot.HoldingCount);
        Assert.Equal(0m, snapshot.NetCostTotal);
        Assert.False(snapshot.HasMissingHoldings);
    }
}
