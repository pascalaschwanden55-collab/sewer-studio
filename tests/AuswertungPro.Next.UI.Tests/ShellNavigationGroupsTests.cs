using System.IO;
using AuswertungPro.Next.UI.ViewModels;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 1: Die Leiste ist in Projekt, Daten, Bewertung, System gruppiert.</summary>
public sealed class ShellNavigationGroupsTests
{
    [Theory]
    [InlineData("Uebersicht", "Projekt")]
    [InlineData("Projekt", "Projekt")]
    [InlineData("Haltungen", "Projekt")]
    [InlineData("Schaechte", "Projekt")]
    [InlineData("Import", "Daten")]
    [InlineData("Export", "Daten")]
    [InlineData("Medienkonflikte", "Daten")]
    [InlineData("Druckcenter", "Daten")]
    [InlineData("Dossiers", "Daten")]
    [InlineData("Sanierungs-Matrix", "Bewertung")]
    [InlineData("Schacht-Matrix", "Bewertung")]
    [InlineData("Schattenauswertung", "Bewertung")]
    [InlineData("VSA", "Bewertung")]
    [InlineData("Diagnose", "System")]
    [InlineData("Einstellungen", "System")]
    public void Jeder_Navigationspunkt_hat_seine_Gruppe(string title, string group)
        => Assert.Equal(group, ShellNavigationGroups.GroupOf(title));

    [Fact]
    public void Unbekannter_Titel_landet_in_System_statt_zu_werfen()
        => Assert.Equal("System", ShellNavigationGroups.GroupOf("Neu"));

    [Fact]
    public void Reihenfolge_der_Gruppen_ist_fest()
        => Assert.Equal(new[] { "Projekt", "Daten", "Bewertung", "System" }, ShellNavigationGroups.Order);

    [Fact]
    public void NavItems_im_ShellViewModel_stehen_gruppenweise_zusammen()
    {
        // Die Leiste gruppiert ohne Sortierung; deshalb muessen die Eintraege in
        // ShellViewModel.cs bereits gruppenweise hintereinander stehen.
        var code = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));
        var titles = new[] { "Uebersicht", "Projekt", "Haltungen", "Schaechte", "Import", "Export", "Medienkonflikte", "Druckcenter", "Dossiers", "Sanierungs-Matrix", "Schacht-Matrix", "Schattenauswertung", "VSA", "Diagnose", "Einstellungen" };
        var last = -1; string? lastGroup = null; var seen = new HashSet<string>();
        foreach (var t in titles)
        {
            var idx = code.IndexOf($"\"{t}\", () =>", StringComparison.Ordinal);
            Assert.True(idx > last, $"{t} steht nicht in der erwarteten Reihenfolge");
            last = idx;
            var g = ShellNavigationGroups.GroupOf(t);
            if (g != lastGroup) { Assert.True(seen.Add(g), $"Gruppe {g} ist unterbrochen"); lastGroup = g; }
        }
    }

    [Fact]
    public void Hauptfenster_gruppiert_die_Leiste_nach_Group()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"));
        Assert.Contains("PropertyGroupDescription PropertyName=\"Group\"", xaml);
        Assert.Contains("<ListBox.GroupStyle>", xaml);
    }
}
