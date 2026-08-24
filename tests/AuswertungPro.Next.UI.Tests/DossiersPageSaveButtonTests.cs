using System;
using System.IO;
using System.Linq;
using System.Reflection;

using AuswertungPro.Next.UI.ViewModels.Pages;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Prueft den Speichern-Knopf des Dossier-Cockpits.
///
/// Jede Aktion speichert zwar schon selbst. Der Knopf beantwortet die andere
/// Frage: Liegt mein Stand JETZT auf der Platte? Deshalb ist die Rueckmeldung
/// mit Anzahl und Uhrzeit hier der eigentliche Gegenstand.
/// </summary>
public sealed class DossiersPageSaveButtonTests
{
    [Fact]
    public void Der_Knopf_steht_auf_der_Seite_und_ruft_den_Speicherbefehl()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));

        var knopf = xaml.IndexOf("Content=\"Speichern\"", StringComparison.Ordinal);
        Assert.True(knopf >= 0, "Es gibt keinen Speichern-Knopf.");

        // Der Befehl muss am selben Knopf haengen, nicht irgendwo auf der Seite.
        var abschnitt = xaml[knopf..Math.Min(xaml.Length, knopf + 400)];
        Assert.Contains("Command=\"{Binding SaveCommand}\"", abschnitt, StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Cockpit_bietet_den_Speicherbefehl_an()
    {
        var eigenschaft = typeof(DossiersPageViewModel)
            .GetProperty("SaveCommand", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(eigenschaft);
        Assert.True(
            typeof(System.Windows.Input.ICommand).IsAssignableFrom(eigenschaft!.PropertyType),
            "SaveCommand ist kein Befehl.");
    }

    [Theory]
    [InlineData(1, "1 Dossier gespeichert um 14:32 Uhr.")]
    [InlineData(0, "0 Dossiers gespeichert um 14:32 Uhr.")]
    [InlineData(7, "7 Dossiers gespeichert um 14:32 Uhr.")]
    public void Die_Rueckmeldung_nennt_Anzahl_und_Uhrzeit(int anzahl, string erwartet)
    {
        var text = DossiersPageViewModel.BuildSaveConfirmation(
            anzahl, new DateTime(2026, 8, 24, 14, 32, 5));

        Assert.Equal(erwartet, text);
    }

    [Fact]
    public void Die_Uhrzeit_ist_zweistellig_auch_am_Morgen()
    {
        // "7:05" laesst offen, ob morgens oder abends gespeichert wurde.
        var text = DossiersPageViewModel.BuildSaveConfirmation(
            2, new DateTime(2026, 8, 24, 7, 5, 0));

        Assert.Contains("07:05", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Speicherweg_geht_ueber_dieselbe_Sperre_wie_jede_andere_Aktion()
    {
        // EnsureProject verweigert bei fehlendem Projekt UND bei einer
        // unlesbaren Dossierdatei. Ohne diese Sperre wuerde der Knopf einen
        // halb geladenen Stand ueber die gute Datei schreiben.
        var quelle = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "DossiersPageViewModel.Actions.cs"));

        var start = quelle.IndexOf(
            "private async Task SaveNowAsync()", StringComparison.Ordinal);
        Assert.True(start >= 0, "SaveNowAsync fehlt.");

        var ende = quelle.IndexOf("BuildSaveConfirmation(", start, StringComparison.Ordinal);
        Assert.True(ende > start);

        var rumpf = quelle[start..ende];
        Assert.Contains("EnsureProject(out var root)", rumpf, StringComparison.Ordinal);
        Assert.Contains("SaveDocumentAsync(root)", rumpf, StringComparison.Ordinal);
    }
}

/// <summary>
/// Prueft den Knopf „Nachführen" — die Ergaenzung eines bestehenden Dossiers,
/// wenn das Projekt spaeter mehr weiss.
/// </summary>
public sealed class DossiersPageRefreshButtonTests
{
    [Fact]
    public void Der_Knopf_steht_bei_der_gewaehlten_Liegenschaft()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));

        var knopf = xaml.IndexOf("Content=\"Nachführen\"", StringComparison.Ordinal);
        Assert.True(knopf >= 0, "Es gibt keinen Nachführen-Knopf.");

        var abschnitt = xaml[knopf..Math.Min(xaml.Length, knopf + 400)];
        Assert.Contains(
            "Command=\"{Binding RefreshDossierCommand}\"", abschnitt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, 1, "2 Leitungen und 1 Schacht ergänzt.")]
    [InlineData(1, 0, "1 Leitung ergänzt.")]
    [InlineData(0, 3, "3 Schächte ergänzt.")]
    [InlineData(0, 0, "Nichts übernommen.")]
    public void Die_Rueckmeldung_nennt_was_wirklich_dazukam(
        int leitungen, int schaechte, string erwartet)
    {
        Assert.Equal(erwartet, DossiersPageViewModel.Nachgefuehrt(leitungen, schaechte));
    }

    [Fact]
    public void Ein_misslungenes_Speichern_laesst_nichts_Halbes_stehen()
    {
        // Ohne diese Ruecknahme haette das Dossier die Ergaenzung im Speicher
        // und die Ablehnungen dazu, waehrend auf der Platte der alte Stand
        // liegt. Beim naechsten Nachfuehren waere das Abgelehnte dann still
        // verschwunden.
        var quelle = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "DossiersPageViewModel.Actions.cs"));

        var start = quelle.IndexOf(
            "private async Task RefreshDossierAsync()", StringComparison.Ordinal);
        Assert.True(start >= 0, "RefreshDossierAsync fehlt.");

        var ende = quelle.IndexOf("Nachgefuehrt(", start, StringComparison.Ordinal);
        var rumpf = quelle[start..ende];

        Assert.Contains("if (!await SaveDocumentAsync(root))", rumpf, StringComparison.Ordinal);
        Assert.Contains("definition.DismissedShaftNumbers = vorherAbgelehnteSchaechte;",
            rumpf, StringComparison.Ordinal);
    }
}
