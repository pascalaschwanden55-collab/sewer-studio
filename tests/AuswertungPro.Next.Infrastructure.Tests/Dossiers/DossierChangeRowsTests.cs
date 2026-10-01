using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierChangeRowsTests
{
    [Fact]
    public void Leere_Liste_erhaelt_idempotent_eine_beschreibbare_Grundzeile()
    {
        var dossier = new DossierDefinition();

        DossierChangeRows.EnsureStarter(dossier);
        DossierChangeRows.EnsureStarter(dossier);

        Assert.Single(dossier.Changes);
        Assert.False(DossierChangeRows.HasContent(dossier.Changes[0]));
    }

    [Fact]
    public void Leere_Grundzeile_wird_nicht_als_Aenderung_gespeichert()
    {
        var dossier = new DossierDefinition
        {
            Changes =
            [
                new DossierChangeRow(),
                new DossierChangeRow { Version = "  " },
                new DossierChangeRow { Date = "26.08.2026" }
            ]
        };

        var removed = DossierChangeRows.RemoveEmpty(dossier);

        Assert.Equal(2, removed);
        var row = Assert.Single(dossier.Changes);
        Assert.Equal("26.08.2026", row.Date);
    }

    [Fact]
    public void Null_Aenderungszeile_aus_Altdaten_wird_durch_Grundzeile_ersetzt()
    {
        var dossier = new DossierDefinition
        {
            Changes = [null!]
        };

        DossierChangeRows.EnsureStarter(dossier);

        var row = Assert.Single(dossier.Changes);
        Assert.NotNull(row);
        Assert.False(DossierChangeRows.HasContent(row));
    }
}
