using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 1): Waechter ueber das neue Control. Sie lesen die XAML-Datei
/// direkt von der Platte — Tokens statt fester Werte, vorlesbare Namen an jedem Knopf,
/// Virtualisierung ohne Recycling und vor allem: das Formular steht NUR im aufgeklappten Zweig.
/// </summary>
public sealed class DesignAuditNovaAufklappListeTests
{
    private static readonly string Datei = RepoFile(
        "src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungAufklappListe.xaml");

    private static string Xaml() => File.ReadAllText(Datei);

    [Fact]
    public void Die_Liste_virtualisiert_ohne_Recycling()
    {
        var xaml = Xaml();
        // Standard statt Recycling: Der aufgeklappte Bereich traegt Editoren; ein wiederverwendeter
        // Container wuerde deren Zustand an die falsche Haltung haengen.
        Assert.Contains("VirtualizingStackPanel.VirtualizationMode=\"Standard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingStackPanel.IsVirtualizing=\"True\"", xaml, StringComparison.Ordinal);
        // SharedSizeGroup wuerde bei Virtualisierung nur die gerade erzeugten Zeilen messen.
        Assert.DoesNotContain("SharedSizeGroup=", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Formular_steht_nur_im_aufgeklappten_Zweig()
    {
        var xaml = Xaml();
        var formular = Regex.Match(xaml, "<DataTemplate x:Key=\"FormularVorlage\">[\\s\\S]*?</DataTemplate>\\s*\\r?\\n\\s*<!-- ENDE FormularVorlage -->");
        Assert.True(formular.Success, "FormularVorlage samt Endmarke nicht gefunden");
        Assert.Contains("controls:RecordDetailsView", formular.Value, StringComparison.Ordinal);

        var ausserhalb = xaml.Remove(formular.Index, formular.Length);
        Assert.DoesNotContain("RecordDetailsView", ausserhalb, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fix-Runde 1: Beschriftung und vorlesbarer Name des Pfeils wechseln mit dem Zustand — an
    /// einer offenen Haltung heisst der Klick "zuklappen". Beide haengen am selben Konverter.
    /// </summary>
    [Fact]
    public void Der_Pfeil_traegt_einen_wechselnden_vorlesbaren_Namen_und_dreht_beim_Aufklappen()
    {
        var xaml = Xaml();
        Assert.Contains("Converter={StaticResource PfeilBeschriftung}", xaml, StringComparison.Ordinal);
        Assert.Contains("&#xE76C;", xaml, StringComparison.Ordinal);
        Assert.Contains("<RotateTransform Angle=\"90\"/>", xaml, StringComparison.Ordinal);

        var knopf = Regex.Match(xaml, "<Button Grid.Column=\"0\" Click=\"Pfeil_Click\"[\\s\\S]*?>");
        Assert.True(knopf.Success, "Pfeilknopf nicht gefunden");
        Assert.Contains("ToolTip=", knopf.Value, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=", knopf.Value, StringComparison.Ordinal);

        var konverter = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungAufklappListeConverters.cs"));
        Assert.Contains("\"Haltung aufklappen\"", konverter, StringComparison.Ordinal);
        Assert.Contains("\"Haltung zuklappen\"", konverter, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fix-Runde 1: Video und Protokoll nennen die Haltung im vorlesbaren Namen — genau wie
    /// <c>StatusZellenBausteine</c> in der Tabelle. "Video abspielen" allein sagt einem
    /// Screenreader nicht, um welche Haltung es geht.
    /// </summary>
    [Fact]
    public void Video_und_Protokoll_nennen_die_Haltung_im_vorlesbaren_Namen()
    {
        var xaml = Xaml();
        Assert.Contains("StringFormat='Video {0} abspielen'", xaml, StringComparison.Ordinal);
        Assert.Contains("StringFormat='Protokoll {0} öffnen'", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fix-Runde 1: Der Auf-/Zuklappzustand eines Themas gehoert dem Thema (TwoWay), nicht dem
    /// Expander — sonst setzt ihn jedes Scrollen und jeder Wechsel der Anordnung zurueck.
    /// </summary>
    [Fact]
    public void Der_Zustand_der_Themen_haengt_am_Thema()
    {
        var xaml = Xaml();
        Assert.Contains("IsExpanded=\"{Binding IstAufgeklappt, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Mode=OneTime", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Kopfzeile_und_Zeile_lesen_dieselben_Spaltenbreiten()
    {
        var xaml = Xaml();
        foreach (var (schluessel, wert) in new[]
                 {
                     ("SpalteName", "170"), ("SpalteStrasse", "140"), ("SpalteMaterial", "110"),
                     ("SpalteDn", "70"), ("SpalteLaenge", "80"), ("SpalteZustand", "60"),
                     ("SpalteKi", "130"), ("SpaltePruefung", "190"), ("SpalteVideo", "48"),
                     ("SpalteProtokoll", "56")
                 })
        {
            Assert.Contains($"<GridLength x:Key=\"{schluessel}\">{wert}</GridLength>", xaml, StringComparison.Ordinal);
        }

        // Jede Breite wird mehrfach gelesen (Kopfzeile, Zeile beziehungsweise Statusgruppe) und
        // nirgends als Zahl wiederholt.
        Assert.DoesNotContain("Width=\"170\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Width=\"190\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Masse_Schrift_und_Rundungen_kommen_aus_den_Tokens()
    {
        var xaml = Xaml();
        foreach (var token in new[]
                 {
                     "{DynamicResource RowHeightCompact}", "{DynamicResource TextXS}", "{DynamicResource TextS}",
                     "{DynamicResource FontMono}", "{DynamicResource MutedBrush}",
                     "{DynamicResource ZustandsklasseChipBreite}", "{DynamicResource ZustandsklasseChipHoehe}",
                     "{DynamicResource RadiusS}", "{DynamicResource SelectionBackgroundBrush}",
                     "{DynamicResource SelectionTextBrush}", "{DynamicResource SelectionBorderBrush}"
                 })
        {
            Assert.Contains(token, xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Zustand_KI_und_Pruefung_verwenden_die_bestehenden_Regeln()
    {
        var xaml = Xaml();
        Assert.Contains("HaltungZeilenStatusConverter", xaml, StringComparison.Ordinal);
        Assert.Contains("ZustandsklasseChipTextConverter", xaml, StringComparison.Ordinal);
        Assert.Contains("ZustandsklasseChipHintergrundConverter", xaml, StringComparison.Ordinal);
        Assert.Contains("ZustandsklasseInkConverter", xaml, StringComparison.Ordinal);
        // Die Hinweistexte am Gedankenstrich stammen aus der Fachregel, nicht aus einer Kopie.
        Assert.Contains("HaltungProtokollQuelle.OhneVideoHinweis", xaml, StringComparison.Ordinal);
        Assert.Contains("HaltungProtokollQuelle.OhneProtokollHinweis", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Kopfzeile_beschriftet_ihre_Spalten_in_Grossbuchstaben()
    {
        var xaml = Xaml();
        Assert.Contains("GrossbuchstabenConverter", xaml, StringComparison.Ordinal);
        foreach (var beschriftung in new[] { "Haltung", "Strasse", "Material", "DN", "Länge", "Zustand", "KI", "Prüfung", "Video", "Protokoll" })
            Assert.Contains($"Source={beschriftung},", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Controller_liegt_ausserhalb_der_DataPage_Teildateien()
    {
        var controller = RepoFile("src", "AuswertungPro.Next.UI", "DataPage", "DataPageAufklappListeController.cs");
        Assert.True(File.Exists(controller), "DataPageAufklappListeController.cs fehlt");

        var code = File.ReadAllText(controller);
        // Kein zweiter Schreibweg: Der Controller baut nur das Formular und den Live-Abgleich.
        Assert.Contains("DataPageDetailLiveSync", code, StringComparison.Ordinal);
        Assert.DoesNotContain("SetFieldValue", code, StringComparison.Ordinal);
        // Der Konflikt-Wortlaut ist gemeinsam, keine Kopie aus dem Nova-Workspace-Controller.
        Assert.Contains("DataPageKonfliktHinweis", code, StringComparison.Ordinal);

        var workspace = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "DataPage", "DataPageNovaWorkspaceController.cs"));
        Assert.Contains("DataPageKonfliktHinweis", workspace, StringComparison.Ordinal);
    }
}
