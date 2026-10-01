using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierEditableStarterRowsTests
{
    [Fact]
    public void Leere_Eigentuemerliste_erhaelt_genau_eine_beschreibbare_Grundzeile()
    {
        var dossier = new DossierDefinition();

        DossierOwnerRows.EnsureStarter(dossier);
        DossierOwnerRows.EnsureStarter(dossier);

        Assert.Single(dossier.Owners);
        Assert.False(dossier.Owners[0].HasContent);
        Assert.Equal(1, DossierOwnerRows.RemoveEmpty(dossier));
        Assert.Empty(dossier.Owners);
    }

    [Fact]
    public void Gefuellte_Eigentuemerzeile_wird_nicht_als_Eingabehilfe_entfernt()
    {
        var dossier = new DossierDefinition
        {
            Owners = [new DossierOwnerRow { Mail = "person@example.ch" }]
        };

        Assert.Equal(0, DossierOwnerRows.RemoveEmpty(dossier));
        Assert.Single(dossier.Owners);
    }

    [Fact]
    public void Null_Eigentuemerzeile_aus_Altdaten_wird_durch_Grundzeile_ersetzt()
    {
        var dossier = new DossierDefinition
        {
            Owners = [null!]
        };

        DossierOwnerRows.EnsureStarter(dossier);

        var row = Assert.Single(dossier.Owners);
        Assert.NotNull(row);
        Assert.False(row.HasContent);
    }

    [Fact]
    public void Vollstaendig_leere_Themenliste_erhaelt_eine_anklickbare_Grundzeile()
    {
        var area = new DossierAreaSettings();
        var dossier = new DossierDefinition();

        DossierTopicRows.EnsureStarter(area, dossier);
        DossierTopicRows.EnsureStarter(area, dossier);

        Assert.Single(dossier.Topics);
        var resolved = Assert.Single(DossierTopicResolver.Resolve(area, dossier));
        Assert.Equal(string.Empty, resolved.Title);
        Assert.Equal(string.Empty, resolved.Text);

        Assert.Equal(1, DossierTopicRows.RemoveEmpty(dossier));
        Assert.Empty(dossier.Topics);
    }

    [Fact]
    public void Themenzeile_mit_Text_aber_ohne_Titel_bleibt_erhalten()
    {
        var dossier = new DossierDefinition
        {
            Topics = [new DossierTopicRow { Text = "Bemerkung ohne Beschriftung" }]
        };

        Assert.Equal(0, DossierTopicRows.RemoveEmpty(dossier));
        var row = Assert.Single(DossierTopicResolver.Resolve(null, dossier));
        Assert.Equal("Bemerkung ohne Beschriftung", row.Text);
    }
}
