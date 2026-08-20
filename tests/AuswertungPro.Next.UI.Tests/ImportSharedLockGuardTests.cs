using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Ein-Knopf-Import und die fuenf manuellen Importe teilen sich Projekt, Staging und
/// den einen Wiederherstellungs-Marker. Sie duerfen deshalb nicht gleichzeitig laufen.
///
/// Der Ein-Knopf-Weg setzte <c>IsImportInProgress</c> gar nicht, und die Sperre meldete
/// den Wechsel nicht an seinen eigenen Befehl - beide Richtungen standen offen.
/// Die Import-Seite laesst sich nicht ohne den ganzen ServiceProvider bauen; deshalb
/// wie bei den uebrigen Import-Waechtern eine Quelltextpruefung.
/// </summary>
public sealed class ImportSharedLockGuardTests
{
    private static string ImportPageSource() => File.ReadAllText(RepoFile(
        "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs"));

    [Fact]
    public void EinKnopfImport_setzt_die_gemeinsame_importsperre()
    {
        var source = ImportPageSource();

        var start = source.IndexOf("private async Task ImportKanalProjektAsync()", StringComparison.Ordinal);
        Assert.True(start >= 0, "ImportKanalProjektAsync nicht gefunden.");
        var ende = source.IndexOf("private Task RunVsaAfterImport", StringComparison.Ordinal);
        Assert.True(ende > start, "Ende von ImportKanalProjektAsync nicht gefunden.");

        var methode = source[start..ende];
        Assert.Contains("IsImportInProgress = true;", methode);
        Assert.Contains("IsImportInProgress = false;", methode);
        // Ohne finally bliebe die Sperre nach einem Fehler haengen und niemand koennte
        // mehr importieren.
        Assert.Contains("finally", methode);
    }

    [Fact]
    public void Sperre_meldet_den_wechsel_auch_an_den_einknopf_befehl()
    {
        var source = ImportPageSource();

        var start = source.IndexOf(
            "partial void OnIsImportInProgressChanged(bool value)", StringComparison.Ordinal);
        Assert.True(start >= 0, "OnIsImportInProgressChanged nicht gefunden.");
        var ende = source.IndexOf("partial void OnCanCancelChanged", StringComparison.Ordinal);
        Assert.True(ende > start, "Ende von OnIsImportInProgressChanged nicht gefunden.");

        var methode = source[start..ende];
        Assert.Contains("ImportKanalProjektCommand.NotifyCanExecuteChanged();", methode);
    }
}
