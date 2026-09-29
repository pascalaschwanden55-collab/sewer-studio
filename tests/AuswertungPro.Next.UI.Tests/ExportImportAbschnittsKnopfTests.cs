using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 9 («Export- und Import-Seite ordnen»), Fix-Runde 1
/// (Koordinator-Rückmeldung «genau ein Hauptknopf je Abschnitt»): Waechter fuer die Regel
/// «genau ein <c>ToolbarButtonAccent</c> je Abschnitt», mit namentlichen, begruendeten
/// Ausnahmen fuer Abschnitte, die aus mehreren wirklich gleichrangigen Alternativen bestehen
/// und deshalb bewusst KEINEN Akzentknopf tragen.
///
/// <para><b>Definition «Abschnitt»</b> (fuer diesen Waechter): ein Container-Element (in der
/// Praxis ein <c>Border</c>) mit einem <c>x:Name</c>, das mit dem Praefix <c>"Abschnitt"</c>
/// beginnt — z. B. <c>AbschnittExcelListen</c>, <c>AbschnittKatasterXtf</c>. ExportPage.xaml
/// traegt vier solche Container (Excel-Listen, Dateien verteilen, Kataster (XTF), WebGIS),
/// ImportPage.xaml drei (Normalfall, Einzelne Quellen, Nacharbeiten). Ein Abschnitt darf
/// beliebig viele Unter-Karten enthalten (z. B. die zwei XTF-Wege) — es zaehlt nur die Anzahl
/// echter Akzentknoepfe (<c>Style="{...Resource ToolbarButtonAccent}"</c>) irgendwo darin.</para>
///
/// <para><b>Regel:</b> Jeder Abschnitt braucht GENAU einen Akzentknopf (seine eine
/// Hauptaktion) — es sei denn, er steht namentlich in <see cref="NullAkzentAusnahmen"/> mit
/// einem Grund; solche Abschnitte duerfen KEINEN Akzentknopf tragen, weil sie aus mehreren
/// wirklich gleichrangigen Alternativen bestehen (kein Rang zwischen ihnen, ein Akzentknopf
/// waere eine erfundene Praeferenz). Andere Abweichungen (0 ausserhalb der Ausnahmeliste, oder
/// 2+) sind immer ein Verstoss.</para>
/// </summary>
public sealed class ExportImportAbschnittsKnopfTests
{
    private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// Namentliche, begruendete Ausnahmen: dieser Abschnitt darf 0 statt genau 1 Akzentknopf
    /// tragen. Jede Ausnahme braucht einen fachlichen Grund — mehrere echt gleichrangige
    /// Alternativen ohne Rangfolge, keine "Kann-man-so-lassen"-Bequemlichkeit.
    /// </summary>
    private static readonly (string Datei, string Abschnitt, string Grund)[] NullAkzentAusnahmen =
    [
        ("ExportPage.xaml", "AbschnittKatasterXtf",
            "Zwei gleichrangige XTF-Wege (Bestehende Katasterdaten aktualisieren / XTF erstellen), " +
            "je mit eigenem \"empfohlen\"-Chip als Fuehrung — kein Rang zwischen beiden, ein " +
            "Akzentknopf waere eine erfundene Praeferenz für den jeweils NICHT empfohlenen Weg."),
        ("ImportPage.xaml", "AbschnittEinzelneQuellen",
            "Sieben gleichrangige Importquellen (PDF-Protokolle, XTF/M150/MDB, WinCan-, IBAK-, " +
            "KINS-Projekt, SchachtPro-Archiv, SchachtPro-QR) ohne jede Empfehlung — keine davon ist " +
            "haeufiger richtig als eine andere."),
        ("ImportPage.xaml", "AbschnittNacharbeiten",
            "Vier gleichrangige Nacharbeiten (Protokolle aus einem Ordner zuordnen, Fotos zuordnen, " +
            "eigene Protokolle neu erzeugen, Projekt portabel machen) ohne Rangfolge — anders als " +
            "\"Kataster (XTF)\" nicht einmal mit einem Empfehlungs-Chip."),
    ];

    [Theory]
    [InlineData("ExportPage.xaml")]
    [InlineData("ImportPage.xaml")]
    public void Jeder_Abschnitt_hat_genau_einen_Akzentknopf_ausser_benannte_Ausnahmen(string dateiname)
    {
        var xaml = Lies(dateiname);
        var verstoesse = FindeVerstoesse(dateiname, xaml);

        Assert.True(
            verstoesse.Count == 0,
            $"{dateiname}: Abschnitte, die nicht genau einen ToolbarButtonAccent-Knopf haben "
            + "(und keine benannte Ausnahme sind): " + string.Join(", ", verstoesse));
    }

    [Theory]
    [InlineData("ExportPage.xaml", 4)]
    [InlineData("ImportPage.xaml", 3)]
    public void Die_Seite_traegt_die_benannten_Abschnitte(string dateiname, int erwarteteAnzahl)
    {
        var abschnitte = Abschnitte(XDocument.Parse(Lies(dateiname)));
        Assert.Equal(erwarteteAnzahl, abschnitte.Count);
    }

    /// <summary>Jede Ausnahme muss einen wirklich vorhandenen Abschnitt der genannten Datei treffen.</summary>
    [Fact]
    public void Jede_Ausnahme_zeigt_auf_einen_wirklich_vorhandenen_Abschnitt()
    {
        foreach (var (datei, abschnittName, _) in NullAkzentAusnahmen)
        {
            var abschnitte = Abschnitte(XDocument.Parse(Lies(datei)));
            Assert.Contains(abschnitte, a => (string)a.Attribute(XNs + "Name")! == abschnittName);
        }
    }

    /// <summary>
    /// Beweist, dass die Regel wirklich prueft: ein kuenstlich zweiter Akzentknopf im selben
    /// Abschnitt wird erkannt (Sabotageprobe, kein vom Zufall gruener Test).
    /// </summary>
    [Fact]
    public void Sabotage_zwei_Akzentknoepfe_im_selben_Abschnitt_werden_erkannt()
    {
        const string sabotiert = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <Border x:Name="AbschnittTest">
                        <StackPanel>
                            <Button Style="{StaticResource ToolbarButtonAccent}" Content="Eins"/>
                            <Button Style="{StaticResource ToolbarButtonAccent}" Content="Zwei"/>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </UserControl>
            """;

        var verstoesse = FindeVerstoesse("Sabotage.xaml", sabotiert);

        Assert.Single(verstoesse);
        Assert.Contains("AbschnittTest", verstoesse[0]);
    }

    /// <summary>
    /// Sabotageprobe fuer die neue "genau eins"-Regel: ein Abschnitt OHNE Akzentknopf, der
    /// NICHT in der Ausnahmeliste steht, muss erkannt werden — sonst waere "genau eins" nur
    /// "hoechstens eins" geblieben.
    /// </summary>
    [Fact]
    public void Sabotage_ein_Abschnitt_ohne_Akzentknopf_ausserhalb_der_Ausnahmeliste_wird_erkannt()
    {
        const string sabotiert = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <Border x:Name="AbschnittOhneAkzent">
                        <StackPanel>
                            <Button Style="{StaticResource ToolbarButton}" Content="Eins"/>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </UserControl>
            """;

        var verstoesse = FindeVerstoesse("Sabotage.xaml", sabotiert);

        Assert.Single(verstoesse);
        Assert.Contains("AbschnittOhneAkzent", verstoesse[0]);
    }

    /// <summary>Gegenprobe: genau ein Akzentknopf im Abschnitt bleibt erlaubt.</summary>
    [Fact]
    public void Ein_Akzentknopf_im_Abschnitt_ist_kein_Verstoss()
    {
        const string ok = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <Border x:Name="AbschnittTest">
                        <StackPanel>
                            <Button Style="{StaticResource ToolbarButtonAccent}" Content="Eins"/>
                            <Button Style="{StaticResource ToolbarButton}" Content="Zwei"/>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </UserControl>
            """;

        Assert.Empty(FindeVerstoesse("Ok.xaml", ok));
    }

    /// <summary>Gegenprobe: ein Abschnitt ohne Akzentknopf ist erlaubt, wenn er auf der Ausnahmeliste steht.</summary>
    [Fact]
    public void Kataster_Xtf_Abschnitt_ohne_Akzentknopf_ist_die_dokumentierte_Ausnahme()
    {
        var verstoesse = FindeVerstoesse("ExportPage.xaml", Lies("ExportPage.xaml"));
        Assert.DoesNotContain(verstoesse, v => v.Contains("AbschnittKatasterXtf"));
    }

    private static string Lies(string dateiname)
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", dateiname));

    private static List<XElement> Abschnitte(XDocument doc)
        => doc.Descendants()
            .Where(e => ((string?)e.Attribute(XNs + "Name"))?.StartsWith("Abschnitt", System.StringComparison.Ordinal) == true)
            .ToList();

    private static List<string> FindeVerstoesse(string dateiname, string xaml)
    {
        var doc = XDocument.Parse(xaml);
        var ns = doc.Root!.Name.Namespace;
        var verstoesse = new List<string>();

        foreach (var abschnitt in Abschnitte(doc))
        {
            var name = (string)abschnitt.Attribute(XNs + "Name")!;
            var anzahl = abschnitt.Descendants(ns + "Button").Count(IstAkzentknopf);
            var erwartet = NullAkzentAusnahmen.Any(a => a.Datei == dateiname && a.Abschnitt == name) ? 0 : 1;

            if (anzahl != erwartet)
                verstoesse.Add($"{name} (hat {anzahl}, erwartet {erwartet})");
        }

        return verstoesse;
    }

    private static bool IstAkzentknopf(XElement button)
    {
        var style = (string?)button.Attribute("Style");
        return style is not null && style.Contains("ToolbarButtonAccent", System.StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fix-Runde 2 (Koordinator-Rückmeldung, Finding 1): Fachbegriffe duerfen nur
    // in ToolTips oder im eingeklappten "Technische Details"-Bereich stehen, nie
    // in immer sichtbarem Text/Content. Dieser Waechter haelt das dauerhaft fest,
    // damit ein spaeterer Satz nicht wieder ein Fachwort "hineinschmuggelt".
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly string[] FachbegriffJargon =
        ["FME", ".ili", "DSS", "SIA405", "Zusatzmodell", "Normdatei"];

    /// <summary>
    /// Genehmigte Ausnahme (Fix-Runde 1, begruendet im Bericht): der Kopfzeilen-Knopf zum
    /// eigenstaendigen SIA405-Lieferungs-Editor — sein Name IST der etablierte Name dieses
    /// separaten Features (matcht den Fenstertitel von <c>XtfLieferungWindow</c>), kein
    /// Fachbegriff in erklaerendem Fliesstext.
    /// </summary>
    private const string GenehmigteAusnahmeSia405Knopf = "SIA405-Lieferung bearbeiten …";

    [Fact]
    public void Export_seite_zeigt_keine_Fachbegriffe_ausserhalb_von_ToolTip_und_Technische_Details()
    {
        var treffer = FindeFachbegriffLecks(Lies("ExportPage.xaml"));

        Assert.True(
            treffer.Count == 0,
            "Fachbegriff (FME/.ili/DSS/SIA405/Zusatzmodell/Normdatei) ausserhalb ToolTip/"
            + "\"Technische Details\" gefunden:\n" + string.Join("\n", treffer));
    }

    /// <summary>
    /// Beweist, dass der Fachbegriff-Waechter wirklich prueft: ein sichtbarer Satz mit zwei
    /// Fachbegriffen wird erkannt, waehrend derselbe Begriff im "Technische Details"-Bereich
    /// oder in einem ToolTip unbeanstandet bleibt (Sabotageprobe).
    /// </summary>
    [Fact]
    public void Sabotage_ein_Fachbegriff_ausserhalb_ToolTip_und_Technische_Details_wird_erkannt()
    {
        const string sabotiert = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <TextBlock Text="Das ist eine SIA405-Datei mit Zusatzmodell."/>
                    <Expander Header="Technische Details">
                        <TextBlock Text="FME und .ili dürfen hier stehen."/>
                    </Expander>
                    <Button Content="X" ToolTip="Auch FME im ToolTip ist erlaubt"/>
                </StackPanel>
            </UserControl>
            """;

        var treffer = FindeFachbegriffLecks(sabotiert);

        Assert.Equal(2, treffer.Count);
        Assert.Contains(treffer, t => t.Contains("SIA405"));
        Assert.Contains(treffer, t => t.Contains("Zusatzmodell"));
        Assert.DoesNotContain(treffer, t => t.Contains("FME"));
    }

    /// <summary>Gegenprobe: die genehmigte Ausnahme loest keinen Treffer aus.</summary>
    [Fact]
    public void Die_genehmigte_Sia405_Knopfbeschriftung_ist_kein_Verstoss()
    {
        var treffer = FindeFachbegriffLecks(Lies("ExportPage.xaml"));
        Assert.DoesNotContain(treffer, t => t.Contains(GenehmigteAusnahmeSia405Knopf));
    }

    private static List<string> FindeFachbegriffLecks(string xaml)
    {
        var doc = XDocument.Parse(xaml);
        var ns = doc.Root!.Name.Namespace;

        var technischeDetailsElemente = new HashSet<XElement>();
        var technischeDetails = doc.Descendants(ns + "Expander")
            .FirstOrDefault(e => (string?)e.Attribute("Header") == "Technische Details");
        if (technischeDetails is not null)
        {
            foreach (var element in technischeDetails.DescendantsAndSelf())
                technischeDetailsElemente.Add(element);
        }

        var treffer = new List<string>();
        foreach (var element in doc.Descendants())
        {
            if (technischeDetailsElemente.Contains(element))
                continue;

            foreach (var attributName in new[] { "Text", "Content" })
            {
                var wert = (string?)element.Attribute(attributName);
                if (wert is null || wert == GenehmigteAusnahmeSia405Knopf)
                    continue;

                foreach (var begriff in FachbegriffJargon)
                {
                    if (wert.Contains(begriff, System.StringComparison.Ordinal))
                        treffer.Add($"{element.Name.LocalName}@{attributName}=\"{wert}\" enthält \"{begriff}\"");
                }
            }
        }

        return treffer;
    }
}
