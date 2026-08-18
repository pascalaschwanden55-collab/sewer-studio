using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Ein volles Dossier gibt es nur fuer Haltungen. Die Zeile selbst muss das sagen koennen,
/// damit weder Kontextmenue noch Befehl versehentlich ein leeres Haltungsdossier ausgeben.
/// </summary>
public sealed class DruckcenterRowVmTests
{
    [Fact]
    public void Schachtzeile_darf_kein_Haltungsdossier_drucken()
    {
        var row = new DruckcenterRowVm
        {
            Kind = DruckcenterRowKind.Schacht,
            Holding = "S-1"
        };

        Assert.False(row.CanPrintDossier);
    }

    [Fact]
    public void Haltungszeile_mit_Datensatz_darf_ein_Dossier_drucken()
    {
        var row = new DruckcenterRowVm
        {
            Kind = DruckcenterRowKind.Haltung,
            Holding = "H-1",
            Record = new HaltungRecord()
        };

        Assert.True(row.CanPrintDossier);
    }

    [Fact]
    public void Haltungszeile_ohne_Datensatz_darf_kein_Dossier_drucken()
    {
        var row = new DruckcenterRowVm
        {
            Kind = DruckcenterRowKind.Haltung,
            Holding = "H-1"
        };

        Assert.False(row.CanPrintDossier);
    }
}
