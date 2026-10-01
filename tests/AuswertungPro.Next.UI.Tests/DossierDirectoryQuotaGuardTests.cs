using System;
using System.IO;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Das Verzeichnis-Kontingent ist eine Nutzungsgrenze, keine Bequemlichkeit:
/// search.ch untersagt maschinelle Massenabfragen ausdruecklich. Es lag im
/// Fenstercode und war dort von keiner Pruefung gedeckt.
/// </summary>
public sealed class DossierDirectoryQuotaGuardTests
{
    private static string Fenster() => File.ReadAllText(RepoFile(
        "src", "AuswertungPro.Next.UI", "Views", "Windows",
        "DossierParcelLookupWindow.xaml.cs"));

    [Fact]
    public void Das_Fenster_ruft_den_geprueften_Weg()
    {
        Assert.Contains(
            "new OwnerDirectoryLookupUseCase(_directory)", Fenster(), StringComparison.Ordinal);
        Assert.Contains("FillWithResultAsync", Fenster(), StringComparison.Ordinal);
        Assert.Contains("Warnings = [.. ergebnis.Warnings", Fenster(), StringComparison.Ordinal);
    }

    [Fact]
    public void Im_Fenstercode_steht_kein_eigenes_Kontingent_mehr()
    {
        // Eine zweite Zahl im Fenster wuerde die gepruefte Grenze aushebeln,
        // ohne dass ein Test davon erfuehre.
        var quelle = Fenster();

        Assert.DoesNotContain("MaxVerzeichnisAbfragen", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("_directory.FindAsync", quelle, StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Kontingent_bleibt_klein()
    {
        // Eine Handvoll je angelegter Liegenschaft — nie ein Stapellauf.
        Assert.InRange(OwnerDirectoryLookupUseCase.MaxQueriesPerProperty, 1, 10);
    }
}
