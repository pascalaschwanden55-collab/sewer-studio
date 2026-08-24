using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Jeder Knopf und jeder Menüeintrag mit sichtbarer Beschriftung soll beim
/// Darüberfahren sagen, was er tut.
///
/// Der Wächter arbeitet als Ratsche: Er prüft die Dateien, die bereits
/// vollständig sind. Wer dort einen neuen Knopf ohne Beschreibung anfügt,
/// macht die Prüfung rot. Kommt ein Bereich neu dazu, wandert seine Datei in
/// die Liste — so wächst der Schutz mit der Arbeit mit, statt erst am Ende
/// scharf zu werden.
/// </summary>
public sealed class KnopfBeschreibungWaechterTests
{
    /// <summary>
    /// Fertige Dateien. Nur hinzufügen, wenn die Datei WIRKLICH vollständig
    /// ist — eine Datei hier drin, die es nicht ist, macht den Wächter wertlos.
    /// </summary>
    private static readonly string[] Fertig =
    [
        @"Views\Pages\DossiersPage.xaml",
        @"Views\Windows\DossierAreaWindow.xaml",
        @"Views\Windows\DossierBatchWindow.xaml",
        @"Views\Windows\DossierEditWindow.xaml",
        @"Views\Windows\DossierHoldingPickerWindow.xaml",
        @"Views\Windows\DossierParcelLookupWindow.xaml",
        @"Views\Windows\DossierPlanWindow.xaml",
        @"Views\Windows\DossierPreviewWindow.xaml",
        @"Views\Windows\DossierRefreshWindow.xaml",
        @"Views\Windows\DossierShaftPickerWindow.xaml"
    ];

    /// <summary>
    /// Ein Knopf oder Menüeintrag mit fester Beschriftung. Elemente ohne
    /// eigene Beschriftung bleiben aussen vor: das sind Vorlagen für
    /// Einträge, die erst zur Laufzeit entstehen — ihr Text steht dann im
    /// ViewModel, nicht im XAML.
    /// </summary>
    private static readonly Regex Element = new(
        @"<(Button|MenuItem)\b(?<attribute>.*?)(?:/>|>)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Beschriftung = new(
        @"(?:Content|Header)=""(?<text>[^""{][^""]*)""", RegexOptions.Compiled);

    [Fact]
    public void Die_fertigen_Seiten_beschreiben_jeden_Knopf()
    {
        var offen = new List<string>();

        foreach (var relativ in Fertig)
        {
            var pfad = RepoFile("src", "AuswertungPro.Next.UI", relativ);
            Assert.True(File.Exists(pfad), $"Datei aus der Fertig-Liste fehlt: {relativ}");

            foreach (var treffer in Element.Matches(File.ReadAllText(pfad)).Cast<Match>())
            {
                var attribute = treffer.Groups["attribute"].Value;
                if (attribute.Contains("ToolTip", StringComparison.Ordinal))
                    continue;

                var text = Beschriftung.Match(attribute);
                if (text.Success)
                    offen.Add($"{Path.GetFileName(pfad)}: „{text.Groups["text"].Value}\"");
            }
        }

        Assert.True(
            offen.Count == 0,
            "Ohne Beschreibung: " + Environment.NewLine + string.Join(Environment.NewLine, offen));
    }

    [Fact]
    public void Ein_beschrifteter_Knopf_bekommt_mehr_als_sein_eigenes_Wort()
    {
        // „Speichern" als Beschreibung eines Knopfes namens Speichern hilft
        // niemandem. Die Beschreibung soll sagen, was danach anders ist.
        //
        // Bei einem Knopf OHNE Beschriftung — einem reinen Symbol — ist es
        // umgekehrt: dort benennt eine kurze Beschreibung wie „90° nach links"
        // den Knopf überhaupt erst, und mehr braucht es nicht.
        var zuKurz = new List<string>();

        foreach (var relativ in Fertig)
        {
            var pfad = RepoFile("src", "AuswertungPro.Next.UI", relativ);

            foreach (var treffer in Element.Matches(File.ReadAllText(pfad)).Cast<Match>())
            {
                var attribute = treffer.Groups["attribute"].Value;

                var beschriftung = Beschriftung.Match(attribute);
                if (!beschriftung.Success || !IstWort(beschriftung.Groups["text"].Value))
                    continue;

                var hinweis = Regex.Match(attribute, @"ToolTip=""(?<text>[^""{][^""]*)""");
                if (!hinweis.Success)
                    continue;

                var text = hinweis.Groups["text"].Value.Trim();
                if (text.Length < 25)
                    zuKurz.Add($"{Path.GetFileName(pfad)}: „{text}\"");
            }
        }

        Assert.True(
            zuKurz.Count == 0,
            "Zu knapp: " + Environment.NewLine + string.Join(Environment.NewLine, zuKurz));
    }

    /// <summary>
    /// Ein Zeichen ist keine Beschriftung. Bei "▲" oder "180°" benennt erst die
    /// kurze Beschreibung den Knopf; bei "Speichern" muss sie mehr sagen als
    /// das Wort, das ohnehin draufsteht.
    /// </summary>
    private static bool IstWort(string beschriftung)
        => beschriftung.Count(char.IsLetter) >= 3;

    [Fact]
    public void Ein_Zeichen_gilt_nicht_als_Beschriftung()
    {
        Assert.False(IstWort("▲"));
        Assert.False(IstWort("180°"));
        Assert.False(IstWort("+"));
        Assert.True(IstWort("Speichern"));
        Assert.True(IstWort("+ Zeile"));
    }

    [Fact]
    public void Der_Waechter_wuerde_eine_fehlende_Beschreibung_wirklich_bemerken()
    {
        // Ein Wächter, der nie anschlägt, ist keiner. Hier der Gegenbeweis an
        // einem erfundenen Stück XAML.
        const string ohneBeschreibung =
            "<StackPanel><Button Content=\"Tu etwas\" Click=\"OnTu\"/></StackPanel>";

        var treffer = Element.Match(ohneBeschreibung);

        Assert.True(treffer.Success);
        Assert.DoesNotContain("ToolTip", treffer.Groups["attribute"].Value, StringComparison.Ordinal);
        Assert.Matches(Beschriftung, treffer.Groups["attribute"].Value);
    }

    [Fact]
    public void Eintraege_ohne_eigene_Beschriftung_verlangt_der_Waechter_nicht()
    {
        // Ihr Text entsteht erst zur Laufzeit aus dem ViewModel.
        const string gebunden =
            "<MenuItem Header=\"{Binding Name}\" Command=\"{Binding OeffneCommand}\"/>";

        var treffer = Element.Match(gebunden);

        Assert.True(treffer.Success);
        Assert.DoesNotMatch(Beschriftung, treffer.Groups["attribute"].Value);
    }
}
