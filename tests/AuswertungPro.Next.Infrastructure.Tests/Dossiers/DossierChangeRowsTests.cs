using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierChangeRowsTests
{
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
}
