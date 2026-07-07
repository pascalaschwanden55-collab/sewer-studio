using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class DerivedCostFieldSynchronizerTests
{
    private static HaltungRecord Rec(string name, string sanieren, string? anschl = null)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: true);
        r.SetFieldValue("Sanieren_JaNein", sanieren, FieldSource.Manual, userEdited: true);
        if (anschl != null)
            r.SetFieldValue("Anschluesse_verpressen", anschl, FieldSource.Pdf, userEdited: false);
        return r;
    }

    [Fact]
    public void Nein_Haltung_wird_geleert()
    {
        var project = new Project();
        project.Data.Add(Rec("A-B", "Nein", anschl: "5"));
        project.Data.Add(Rec("C-D", "Ja"));
        var store = new ProjectCostStore();

        var changed = new DerivedCostFieldSynchronizer().Sync(project, store);

        Assert.Equal(1, changed); // nur die Nein-Haltung hatte 5 -> ""
        Assert.Equal("", project.Data[0].GetFieldValue("Anschluesse_verpressen"));
    }

    [Fact]
    public void Ja_Haltung_mit_Store_bekommt_Anschlusszahl()
    {
        var project = new Project();
        project.Data.Add(Rec("A-B", "Ja"));
        var store = new ProjectCostStore();
        store.ByHolding["A-B"] = new HoldingCost
        {
            Holding = "A-B",
            Measures =
            {
                new MeasureCost
                {
                    Lines = { new CostLine { ItemKey = "ANSCHLUSS_EINBINDEN", Unit = "Stk", Qty = 3, Selected = true } }
                }
            }
        };

        new DerivedCostFieldSynchronizer().Sync(project, store);

        Assert.Equal("3", project.Data[0].GetFieldValue("Anschluesse_verpressen"));
    }

    [Fact]
    public void Store_case_insensitiv_gematcht()
    {
        var project = new Project();
        project.Data.Add(Rec("a-b", "Ja"));
        var store = new ProjectCostStore();
        store.ByHolding["A-B"] = new HoldingCost
        {
            Holding = "A-B",
            Measures =
            {
                new MeasureCost
                {
                    Lines = { new CostLine { ItemKey = "ANSCHLUSS_AUFFRAESEN", Unit = "Stk", Qty = 4, Selected = true } }
                }
            }
        };

        new DerivedCostFieldSynchronizer().Sync(project, store);

        Assert.Equal("4", project.Data[0].GetFieldValue("Anschluesse_verpressen"));
    }
}
