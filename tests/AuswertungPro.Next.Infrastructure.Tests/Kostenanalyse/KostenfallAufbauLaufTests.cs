using System;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallAufbauLaufTests
{
    private static readonly DateTime Jetzt = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static HaltungRecord Haltung(string name, string dn, string laenge, params string[] codes)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: false);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision { Entries = [.. codes.Select(c => new ProtocolEntry { Code = c })] }
        };
        return record;
    }

    private static HoldingCost Kosten(string holding) => new()
    {
        Holding = holding,
        Measures =
        [
            new MeasureCost
            {
                MeasureId = "M", MeasureName = "Renovierung",
                Lines = [new CostLine { ItemKey = "SCHLAUCHLINER_GFK", Qty = 40m, Unit = "m", UnitPrice = 200m, Selected = true }]
            }
        ]
    };

    [Fact]
    public void Baut_Faelle_nur_aus_Haltungen_mit_Kosten()
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung("H-1", "300", "40", "BAF01"));
        projekt.Data.Add(Haltung("H-2", "300", "40", "BAF01"));
        var kosten = new ProjectCostStore { ByHolding = { ["H-1"] = Kosten("H-1") } };

        var (faelle, uebersprungen) = KostenfallAufbauLauf.Baue(projekt, kosten, "Zone 1.15", Jetzt);

        Assert.Equal("H-1", Assert.Single(faelle).Haltung);
        Assert.Contains(uebersprungen, u => u.Contains("H-2"));
    }

    [Fact]
    public void Der_Grund_des_Ueberspringens_steht_im_Bericht()
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung("H-1", "", "40", "BAF01"));
        var kosten = new ProjectCostStore { ByHolding = { ["H-1"] = Kosten("H-1") } };

        var (faelle, uebersprungen) = KostenfallAufbauLauf.Baue(projekt, kosten, "P", Jetzt);

        Assert.Empty(faelle);
        Assert.Contains("Durchmesser", Assert.Single(uebersprungen));
    }

    [Fact]
    public void Alle_Faelle_gelten_als_unbeeinflusst()
    {
        // Der Altbestand entstand ohne jeden Vorschlag - er ist unbeeinflusst.
        var projekt = new Project();
        projekt.Data.Add(Haltung("H-1", "300", "40", "BAF01"));
        var kosten = new ProjectCostStore { ByHolding = { ["H-1"] = Kosten("H-1") } };

        var (faelle, _) = KostenfallAufbauLauf.Baue(projekt, kosten, "P", Jetzt);

        Assert.Equal(KostenfallHerkunft.Unbeeinflusst, Assert.Single(faelle).Herkunft);
    }
}
