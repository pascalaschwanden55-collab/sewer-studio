using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.ViewModels.Pages;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft den Untertitel im Dossier-Cockpit. Wer nur die neue Eigentuemer-
/// Tabelle fuellt, soll dort nicht "Noch keine Stammdaten erfasst" lesen,
/// waehrend im erzeugten Word bereits alle Namen stehen.
/// </summary>
public sealed class DossiersPageViewModelSubtitleTests
{
    [Fact]
    public void Nutzt_OwnerName_wenn_gefuellt()
    {
        var dossier = new DossierDefinition
        {
            OwnerName = "Lubag AG",
            Town = "Erstfeld"
        };

        var subtitle = DossiersPageViewModel.BuildSubtitle(dossier);

        Assert.Equal("Lubag AG · Erstfeld", subtitle);
    }

    [Fact]
    public void Faellt_bei_leerem_OwnerName_auf_die_erste_Eigentuemerzeile_zurueck()
    {
        var dossier = new DossierDefinition { Town = "Erstfeld" };
        dossier.Owners.Add(new DossierOwnerRow { Name = "Martin Muster" });
        dossier.Owners.Add(new DossierOwnerRow { Name = "Anna Gisler" });

        var subtitle = DossiersPageViewModel.BuildSubtitle(dossier);

        Assert.Equal("Martin Muster · Erstfeld", subtitle);
    }

    [Fact]
    public void Ohne_OwnerName_und_ohne_Zeilen_bleibt_der_Hinweistext()
    {
        var dossier = new DossierDefinition();

        var subtitle = DossiersPageViewModel.BuildSubtitle(dossier);

        Assert.Equal("Noch keine Stammdaten erfasst", subtitle);
    }
}
