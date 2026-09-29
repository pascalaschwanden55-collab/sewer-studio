using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Optikanalyse 28.09.2026, Aufgabe 3 («Fensterregel - gemeinsamer Kopf und
/// Knopfleiste, Teil 1: Grundlage + Dossier-Fenster»): alle elf Dossier-Fenster tragen einen
/// <c>NovaDialogHeader</c> und eine <c>DialogButtonBar</c>-Fussleiste nach der Knopfregel
/// (Plan-Abschnitt «Global Constraints», Punkt 5) - genau EIN <c>IsDefault="True"</c> mit
/// <c>PrimaryButton</c>, ein Abbrechen-/Schliessen-Knopf mit <c>IsCancel="True"</c> und kein
/// <c>SuccessButton</c> mehr. Der umfassende Waechter fuer ALLE Fenster
/// (<c>DesignAuditKnopfleistenTests</c>) folgt in Aufgabe 4; dieser Test sichert nur den in
/// Aufgabe 3 tatsaechlich umgesetzten Bestand.
/// </summary>
public sealed class DesignAuditDossierFensterTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    private static readonly string[] DossierFenster =
    [
        "DossierAreaWindow.xaml",
        "DossierBatchWindow.xaml",
        "DossierEditWindow.xaml",
        "DossierHoldingPickerWindow.xaml",
        "DossierPageSelectionWindow.xaml",
        "DossierParcelLookupWindow.xaml",
        "DossierPlanWindow.xaml",
        "DossierPreviewWindow.xaml",
        "DossierPrintDialog.xaml",
        "DossierRefreshWindow.xaml",
        "DossierShaftPickerWindow.xaml"
    ];

    [Fact]
    public void Jedes_Dossier_Fenster_traegt_einen_NovaDialogHeader()
    {
        var fehlend = new List<string>();
        foreach (var datei in DossierFenster)
        {
            var pfad = Path.Combine(UiRoot, "Views", "Windows", datei);
            var xaml = File.ReadAllText(pfad);
            if (!xaml.Contains("<controls:NovaDialogHeader", StringComparison.Ordinal))
                fehlend.Add(datei);
        }

        Assert.True(fehlend.Count == 0, "Kein NovaDialogHeader in: " + string.Join(", ", fehlend));
    }

    [Fact]
    public void Jedes_Dossier_Fenster_traegt_eine_DialogButtonBar_Fussleiste()
    {
        var fehlend = new List<string>();
        foreach (var datei in DossierFenster)
        {
            var pfad = Path.Combine(UiRoot, "Views", "Windows", datei);
            var xaml = File.ReadAllText(pfad);
            if (!xaml.Contains("Style=\"{StaticResource DialogButtonBar}\"", StringComparison.Ordinal))
                fehlend.Add(datei);
        }

        Assert.True(fehlend.Count == 0, "Keine DialogButtonBar-Fussleiste in: " + string.Join(", ", fehlend));
    }

    [Fact]
    public void Jedes_Dossier_Fenster_hat_genau_einen_Hauptknopf()
    {
        // Knopfregel: hoechstens EIN Hauptknopf je Fenster. IsDefault="True" darf deshalb genau
        // einmal vorkommen, und genau an diesem Knopf muss PrimaryButton stehen (nicht an einem
        // zufaellig danebenliegenden anderen Knopf).
        var verstoesse = new List<string>();
        foreach (var datei in DossierFenster)
        {
            var pfad = Path.Combine(UiRoot, "Views", "Windows", datei);
            var xaml = File.ReadAllText(pfad);

            var anzahlIsDefault = Regex.Matches(xaml, "IsDefault=\"True\"").Count;
            if (anzahlIsDefault != 1)
            {
                verstoesse.Add($"{datei}: {anzahlIsDefault} IsDefault=\"True\"-Knoepfe statt genau einem");
                continue;
            }

            // Der Knopf-Tag, der IsDefault="True" traegt, muss im selben <Button ...>-Block
            // auch Style="{StaticResource PrimaryButton}" tragen.
            var hauptknopf = Regex.Match(xaml, @"<Button\b[^>]*?IsDefault=""True""[^>]*?/>", RegexOptions.Singleline);
            if (!hauptknopf.Success)
            {
                verstoesse.Add($"{datei}: IsDefault=\"True\" steht nicht in einem einzeiligen Button-Tag (Test unvollständig)");
                continue;
            }

            if (!hauptknopf.Value.Contains("Style=\"{StaticResource PrimaryButton}\"", StringComparison.Ordinal))
                verstoesse.Add($"{datei}: Der Hauptknopf (IsDefault) traegt nicht PrimaryButton");
        }

        Assert.True(verstoesse.Count == 0, string.Join("\n", verstoesse));
    }

    [Fact]
    public void Jedes_Dossier_Fenster_hat_einen_Abbrechen_oder_Schliessen_Knopf()
    {
        var fehlend = new List<string>();
        foreach (var datei in DossierFenster)
        {
            var pfad = Path.Combine(UiRoot, "Views", "Windows", datei);
            var xaml = File.ReadAllText(pfad);
            if (!xaml.Contains("IsCancel=\"True\"", StringComparison.Ordinal))
                fehlend.Add(datei);
        }

        Assert.True(fehlend.Count == 0, "Kein Abbrechen-/Schliessen-Knopf (IsCancel) in: " + string.Join(", ", fehlend));
    }

    [Fact]
    public void Kein_SuccessButton_mehr_in_den_Dossier_Fenstern()
    {
        var treffer = new List<string>();
        foreach (var datei in DossierFenster)
        {
            var pfad = Path.Combine(UiRoot, "Views", "Windows", datei);
            var xaml = File.ReadAllText(pfad);
            if (xaml.Contains("SuccessButton", StringComparison.Ordinal))
                treffer.Add(datei);
        }

        Assert.True(treffer.Count == 0, "SuccessButton haette auf PrimaryButton umgestellt werden sollen in: " + string.Join(", ", treffer));
    }

    [Fact]
    public void DataPage_hat_keine_lokale_CompactButton_Doppeldefinition_mehr()
    {
        var xaml = File.ReadAllText(Path.Combine(UiRoot, "Views", "Pages", "DataPage.xaml"));
        Assert.DoesNotContain("<Style x:Key=\"CompactButton\"", xaml);
    }

    [Fact]
    public void CompactButton_ist_in_beiden_Themes_gleich_und_folgt_SecondaryButton()
    {
        foreach (var datei in new[] { "Theme.xaml", "ThemeLight.xaml" })
        {
            var xaml = File.ReadAllText(Path.Combine(UiRoot, "Theme", datei));
            var m = Regex.Match(xaml, @"<Style x:Key=""CompactButton"" TargetType=""Button"" BasedOn=""\{StaticResource SecondaryButton\}"">.*?</Style>", RegexOptions.Singleline);
            Assert.True(m.Success, $"{datei}: CompactButton fehlt oder ist nicht auf SecondaryButton umgestellt.");
            Assert.Contains("MinHeight\" Value=\"28\"", m.Value);
            Assert.Contains("Padding\" Value=\"10,4\"", m.Value);
        }
    }

    [Fact]
    public void DialogButtonBar_und_DangerButton_und_NovaDialogHeader_stehen_im_gemeinsamen_Theme()
    {
        var controls = File.ReadAllText(Path.Combine(UiRoot, "Theme", "Controls.xaml"));
        Assert.Contains("<Style x:Key=\"DialogButtonBar\" TargetType=\"Border\">", controls);
        Assert.Contains("<Style x:Key=\"DangerButton\" TargetType=\"Button\" BasedOn=\"{StaticResource SecondaryButton}\">", controls);
        Assert.Contains("<Style TargetType=\"{x:Type controls:NovaDialogHeader}\">", controls);

        Assert.True(File.Exists(Path.Combine(UiRoot, "Controls", "NovaDialogHeader.cs")), "NovaDialogHeader.cs fehlt.");
    }
}
