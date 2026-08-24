using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierTopicEditingTests
{
    private static DossierAreaSettings Gebiet()
        => new()
        {
            Topics =
            {
                new DossierTopicRow { Title = "Unternehmer", Text = "Musterbau AG" },
                new DossierTopicRow { Title = "Schäden", Text = "" }
            }
        };

    [Fact]
    public void Tippen_setzt_eine_Abweichung_und_laesst_das_Gebiet_in_Ruhe()
    {
        var gebiet = Gebiet();
        var dossier = new DossierDefinition();

        DossierTopicEditing.SetForDossier(dossier, "Schäden", "Leitung undicht");

        Assert.Equal("Leitung undicht", dossier.Topics.Single().Text);
        Assert.Equal("", gebiet.Topics.Single(t => t.Title == "Schäden").Text);

        var aufgeloest = DossierTopicResolver.Resolve(gebiet, dossier);
        Assert.Equal("Leitung undicht", aufgeloest.Single(t => t.Title == "Schäden").Text);
    }

    [Fact]
    public void Zweimal_tippen_erzeugt_keine_zweite_Zeile()
    {
        var dossier = new DossierDefinition();

        DossierTopicEditing.SetForDossier(dossier, "Schäden", "erst");
        DossierTopicEditing.SetForDossier(dossier, "schäden", "dann");

        Assert.Equal("dann", dossier.Topics.Single().Text);
    }

    [Fact]
    public void Fuer_alle_uebernehmen_schreibt_ins_Gebiet_und_raeumt_die_Abweichung_weg()
    {
        var gebiet = Gebiet();
        var dossier = new DossierDefinition();

        DossierTopicEditing.SetForDossier(dossier, "Unternehmer", "Implenia AG");
        DossierTopicEditing.PromoteToArea(gebiet, dossier, "Unternehmer", "Implenia AG");

        Assert.Equal("Implenia AG", gebiet.Topics.Single(t => t.Title == "Unternehmer").Text);
        Assert.Empty(dossier.Topics);

        // Und damit gilt der Text auch fuer jede andere Liegenschaft.
        var andere = DossierTopicResolver.Resolve(gebiet, new DossierDefinition());
        Assert.Equal("Implenia AG", andere.Single(t => t.Title == "Unternehmer").Text);
    }

    [Fact]
    public void Ein_dem_Gebiet_unbekannter_Titel_entsteht_dort_neu()
    {
        // Sonst verschwaende der Text spurlos.
        var gebiet = Gebiet();
        var dossier = new DossierDefinition();

        DossierTopicEditing.PromoteToArea(gebiet, dossier, "Aktennotiz", "Altdorf, 24.08.2026");

        Assert.Equal("Altdorf, 24.08.2026",
            gebiet.Topics.Single(t => t.Title == "Aktennotiz").Text);
    }

    [Fact]
    public void Eine_Abweichung_laesst_sich_wieder_entfernen()
    {
        var gebiet = Gebiet();
        var dossier = new DossierDefinition();

        DossierTopicEditing.SetForDossier(dossier, "Unternehmer", "Nur hier");
        Assert.True(DossierTopicEditing.HasDossierOverride(dossier, "Unternehmer"));

        DossierTopicEditing.RemoveDossierOverride(dossier, "Unternehmer");

        Assert.False(DossierTopicEditing.HasDossierOverride(dossier, "Unternehmer"));
        Assert.Equal("Musterbau AG",
            DossierTopicResolver.Resolve(gebiet, dossier)
                .Single(t => t.Title == "Unternehmer").Text);
    }

    [Fact]
    public void Ein_leerer_Titel_erzeugt_keine_Zeile()
    {
        var dossier = new DossierDefinition();

        DossierTopicEditing.SetForDossier(dossier, "   ", "Text ohne Thema");

        Assert.Empty(dossier.Topics);
    }
}
