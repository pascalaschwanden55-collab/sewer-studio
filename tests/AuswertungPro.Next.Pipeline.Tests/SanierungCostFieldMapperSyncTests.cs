using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer die Sanieren-Regel in <see cref="SanierungCostFieldMapper.SyncRecord"/>:
/// Nur Haltungen mit Sanieren_JaNein=Ja zaehlen; Nein/leer -> abgeleitete Felder leer.
/// </summary>
public class SanierungCostFieldMapperSyncTests
{
    private static HaltungRecord Rec(string sanieren, string? anschl = null)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Sanieren_JaNein", sanieren, FieldSource.Manual, userEdited: true);
        if (anschl != null)
            r.SetFieldValue("Anschluesse_verpressen", anschl, FieldSource.Pdf, userEdited: false);
        return r;
    }

    private static HoldingCost CostWithAnschluss(int stk) => new()
    {
        Holding = "H1",
        Measures =
        {
            new MeasureCost
            {
                MeasureId = "M",
                MeasureName = "GFK",
                Lines =
                {
                    new CostLine { ItemKey = "ANSCHLUSS_EINBINDEN", Unit = "Stk", Qty = stk, Selected = true, UnitPrice = 100m }
                }
            }
        }
    };

    [Fact]
    public void Ja_mit_Massnahme_setzt_Anschlusszahl()
    {
        var r = Rec("Ja");
        var changed = SanierungCostFieldMapper.SyncRecord(r, CostWithAnschluss(2));
        Assert.True(changed);
        Assert.Equal("2", r.GetFieldValue("Anschluesse_verpressen"));
    }

    [Fact]
    public void Nein_leert_alle_Kostenfelder_auch_Pdf_Import()
    {
        var r = Rec("Nein", anschl: "5");
        var changed = SanierungCostFieldMapper.SyncRecord(r, cost: null);
        Assert.True(changed);
        Assert.Equal("", r.GetFieldValue("Anschluesse_verpressen"));
    }

    [Fact]
    public void Ja_ohne_Massnahme_leert_Mengenfelder_behaelt_Kosten()
    {
        var r = Rec("Ja", anschl: "3");
        r.SetFieldValue("Kosten", "1200.00", FieldSource.Manual, userEdited: true);
        SanierungCostFieldMapper.SyncRecord(r, cost: null);
        Assert.Equal("", r.GetFieldValue("Anschluesse_verpressen"));
        Assert.Equal("1200.00", r.GetFieldValue("Kosten"));
    }

    [Fact]
    public void Bereits_synchron_meldet_keine_Aenderung()
    {
        var r = Rec("Ja");
        SanierungCostFieldMapper.SyncRecord(r, CostWithAnschluss(2));
        var changedAgain = SanierungCostFieldMapper.SyncRecord(r, CostWithAnschluss(2));
        Assert.False(changedAgain);
    }
}
