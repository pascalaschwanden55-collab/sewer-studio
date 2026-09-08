using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 4): Die Uebersicht zeigt die Haltungsgrafik des Protokolls. Der
/// Aufbau laeuft mit den echten App-Ressourcen in einem eigenen Kindprozess — kein Projekt,
/// kein ViewModel, kein Fensterstart.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class HaltungsgrafikControlIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(HaltungsgrafikControlIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_zeichnet_die_Haltungsgrafik);

    [Fact]
    public async Task Haltungsgrafik_laesst_sich_in_eigenem_Wpf_Prozess_zeichnen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_zeichnet_die_Haltungsgrafik()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            OhneHaltungBleibtDieFlaecheLeer();
            DreiBefundeErgebenDreiHinweisflaechen();
            OhneLaengeStehtEinEhrlicherHinweis();
            EinSteuerzeichenImTextStuerztNichtAb();
            EinFehlerBeimAufloesenZeigtHinweisStattAbsturz();
            DieRohrsaeuleBleibtLesbar();
            DasPanelZeigtDieGrafikStattDesRohrrings();
            DieFarbenFolgenDemTheme(app);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    /// <summary>Ohne gewaehlte Haltung wird nichts gezeichnet und nichts behauptet.</summary>
    private static void OhneHaltungBleibtDieFlaecheLeer()
    {
        var grafik = new HaltungsgrafikControl();
        Zeichnen(grafik);

        Assert.Null(Buehne(grafik).Child);
        Assert.Equal(0, grafik.SymbolAnzahl);
        Assert.Equal(Visibility.Collapsed, Hinweis(grafik).Visibility);
    }

    private static void DreiBefundeErgebenDreiHinweisflaechen()
    {
        var eintraege = new ObservableCollection<ProtocolEntry>();
        var grafik = new HaltungsgrafikControl { Record = Haltung(), Entries = eintraege };
        Zeichnen(grafik);

        Assert.Equal(3, grafik.SymbolAnzahl);
        var flaeche = Assert.IsType<Canvas>(Buehne(grafik).Child);
        Assert.NotEmpty(flaeche.Children.OfType<Shape>());

        var hinweise = flaeche.Children.OfType<Rectangle>()
            .Select(r => r.ToolTip as string)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
        Assert.Equal(3, hinweise.Count);
        Assert.Contains(hinweise, t => t!.StartsWith("BAB", StringComparison.Ordinal));

        // Live: Ein Eintrag mehr im Protokoll zeichnet neu, ohne dass der Datensatz wechselt.
        grafik.Record!.Protocol!.Current.Entries.Add(Eintrag("BAC", 20.0));
        eintraege.Add(grafik.Record.Protocol.Current.Entries[^1]);
        Zeichnen(grafik);
        Assert.Equal(4, grafik.SymbolAnzahl);
    }

    /// <summary>Ohne Laenge gibt es keinen Massstab — und deshalb einen Satz statt einer Grafik.</summary>
    private static void OhneLaengeStehtEinEhrlicherHinweis()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "10001-10002", FieldSource.Manual, false);
        var grafik = new HaltungsgrafikControl { Record = record };
        Zeichnen(grafik);

        Assert.Null(Buehne(grafik).Child);
        Assert.Equal(Visibility.Visible, Hinweis(grafik).Visibility);
        Assert.Contains("Haltungslänge", Hinweis(grafik).Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fix-Runde 1 (Review B.1): Ein Steuerzeichen im Befundtext liess den WPF-XmlReader beim
    /// blossen Anzeigen mit einer XmlException abstuerzen, weil <c>EscapeSvgText</c> es nicht
    /// bereinigte und <c>Baue()</c> ausserhalb des Try-Blocks stand. Jetzt wird das Steuerzeichen
    /// entfernt und die Grafik zeichnet normal weiter — kein Absturz, kein Hinweistext noetig.
    /// </summary>
    private static void EinSteuerzeichenImTextStuerztNichtAb()
    {
        var record = Haltung();
        record.Protocol!.Current.Entries[0].Beschreibung = "Riss \u0002 quer";
        var grafik = new HaltungsgrafikControl { Record = record };
        Zeichnen(grafik);

        Assert.Equal(3, grafik.SymbolAnzahl);
        Assert.NotNull(Buehne(grafik).Child);
        Assert.Equal(Visibility.Collapsed, Hinweis(grafik).Visibility);
    }

    /// <summary>
    /// Fix-Runde 1 (Review B.1, Kernfall): Vorher stand <c>Baue()</c> AUSSERHALB des Try-Blocks —
    /// nur ein Fehler beim Zeichnen selbst wurde gefangen, nicht beim Aufloesen der Grafik. Ein
    /// kaputter Katalog (hier absichtlich) liess das Programm beim blossen Auswaehlen der Haltung
    /// abstuerzen. Jetzt zeigt die Flaeche einen Hinweis statt eine Ausnahme durchzureichen.
    /// </summary>
    private static void EinFehlerBeimAufloesenZeigtHinweisStattAbsturz()
    {
        var grafik = new HaltungsgrafikControl { Record = Haltung(), Catalog = new KaputterKatalog() };
        Zeichnen(grafik);

        Assert.Null(Buehne(grafik).Child);
        Assert.Equal(0, grafik.SymbolAnzahl);
        Assert.Equal(Visibility.Visible, Hinweis(grafik).Visibility);
        Assert.Contains("nicht gezeichnet werden", Hinweis(grafik).Text, StringComparison.Ordinal);
    }

    /// <summary>Katalog, der bei jedem Zugriff wirft — steht nur fuer einen echten Fehlerfall.</summary>
    private sealed class KaputterKatalog : ICodeCatalogProvider
    {
        public IReadOnlyList<CodeDefinition> GetAll() => throw new InvalidOperationException("Katalog kaputt.");
        public bool TryGet(string code, out CodeDefinition def) => throw new InvalidOperationException("Katalog kaputt.");
        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new InvalidOperationException("Katalog kaputt.");
        public IReadOnlyList<string> AllowedCodes() => throw new InvalidOperationException("Katalog kaputt.");
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => throw new InvalidOperationException("Katalog kaputt.");
    }

    /// <summary>
    /// Fix-Runde 1 (Sichtprobe im Pruefhost): Die volle Grafik war in der Uebersicht Pixelstaub —
    /// das Rohr ein Strich, die Texte unlesbar. In der schmalen Spalte wird deshalb nur die
    /// Rohrsaeule gezeichnet, und der Massstab muss so bleiben, dass man sie lesen kann.
    /// </summary>
    private static void DieRohrsaeuleBleibtLesbar()
    {
        var grafik = new HaltungsgrafikControl
        {
            Record = Haltung(),
            NurRohr = true,
            SvgHoehe = 310,
            Height = 360
        };
        // Breite wie die Uebersicht in der Standardbreite der Spalte.
        grafik.Measure(new Size(300, 360));
        grafik.Arrange(new Rect(0, 0, 300, 360));
        grafik.UpdateLayout();
        grafik.ZeichneJetzt();
        grafik.UpdateLayout();

        var buehne = Buehne(grafik);
        var flaeche = Assert.IsType<Canvas>(buehne.Child);
        var skala = Math.Min(buehne.ActualWidth / flaeche.Width, buehne.ActualHeight / flaeche.Height);
        Assert.True(skala >= 1d, $"Die Rohrsaeule wird verkleinert: Massstab {skala:0.00}");

        // Die Rohrsaeule steht in einer verschobenen Gruppe, deshalb tief suchen.
        var formen = AlleKinder(flaeche).ToList();
        var rohr = formen.OfType<Rectangle>().First(r => r.Fill is LinearGradientBrush);
        Assert.True(
            rohr.Width * skala >= 3d,
            $"Rohr nur {rohr.Width * skala:0.0} px breit auf dem Bildschirm");

        var texte = formen.OfType<TextBlock>().ToList();
        Assert.NotEmpty(texte);
        foreach (var text in texte)
        {
            Assert.True(
                text.FontSize * skala >= 9d,
                $"Beschriftung \"{text.Text}\" nur {text.FontSize * skala:0.0} px gross");
        }

        // Die Beschriftungstabelle ist weg; die Schadenliste daneben ist die Legende.
        Assert.DoesNotContain(texte, t => t.Text == "OP Kürzel");
    }

    private static void DasPanelZeigtDieGrafikStattDesRohrrings()
    {
        var panel = new HaltungUebersichtPanel { Record = Haltung() };
        panel.Measure(new Size(360, 900));
        panel.Arrange(new Rect(0, 0, 360, 900));
        panel.UpdateLayout();

        Assert.Null(Nachfahre<RohrringControl>(panel));
        var grafik = Nachfahre<HaltungsgrafikControl>(panel);
        Assert.NotNull(grafik);
        Assert.True(grafik!.NurRohr, "In der Uebersicht wird nur die Rohrsaeule gezeichnet.");
        grafik.ZeichneJetzt();
        Assert.Equal(3, grafik.SymbolAnzahl);
    }

    /// <summary>
    /// Keine feste Farbe: Dieselbe Grafik zeigt im hellen und im dunklen Design verschiedene
    /// Farben, weil jede Form an einem Theme-Token haengt.
    /// </summary>
    private static void DieFarbenFolgenDemTheme(App app)
    {
        var farben = new List<Color>();
        foreach (var datei in new[] { "ThemeLight.xaml", "Theme.xaml" })
        {
            app.Resources.MergedDictionaries[0] = new ResourceDictionary
            {
                Source = new Uri($"/SewerStudio;component/Theme/{datei}", UriKind.Relative)
            };

            var grafik = new HaltungsgrafikControl { Record = Haltung() };
            Zeichnen(grafik);
            var flaeche = Assert.IsType<Canvas>(Buehne(grafik).Child);
            var blatt = flaeche.Children.OfType<Rectangle>().First();
            farben.Add(Assert.IsType<SolidColorBrush>(blatt.Fill).Color);
        }

        Assert.NotEqual(farben[0], farben[1]);
    }

    private static Viewbox Buehne(HaltungsgrafikControl grafik)
        => Assert.IsType<Viewbox>(grafik.FindName("Buehne"));

    private static TextBlock Hinweis(HaltungsgrafikControl grafik)
        => Assert.IsType<TextBlock>(grafik.FindName("Hinweis"));

    /// <summary>
    /// Messen, anordnen und den gebuendelten Neuaufbau ausloesen. Im laufenden Programm arbeitet
    /// ihn der Dispatcher ab; ohne Fenster gibt es keine Nachrichtenschleife, deshalb hier direkt.
    /// </summary>
    private static void Zeichnen(HaltungsgrafikControl grafik)
    {
        grafik.Measure(new Size(360, 400));
        grafik.Arrange(new Rect(0, 0, 360, 400));
        grafik.UpdateLayout();
        grafik.ZeichneJetzt();
        grafik.UpdateLayout();
    }

    /// <summary>Alle Elemente einer Zeichenflaeche, auch die in verschobenen Gruppen.</summary>
    private static IEnumerable<FrameworkElement> AlleKinder(Canvas flaeche)
    {
        foreach (var kind in flaeche.Children.OfType<FrameworkElement>())
        {
            yield return kind;
            if (kind is Canvas gruppe)
            {
                foreach (var enkel in AlleKinder(gruppe))
                    yield return enkel;
            }
        }
    }

    private static T? Nachfahre<T>(DependencyObject wurzel) where T : DependencyObject
    {
        var anzahl = VisualTreeHelper.GetChildrenCount(wurzel);
        for (var i = 0; i < anzahl; i++)
        {
            var kind = VisualTreeHelper.GetChild(wurzel, i);
            if (kind is T treffer)
                return treffer;
            if (Nachfahre<T>(kind) is { } tiefer)
                return tiefer;
        }

        return null;
    }

    private static HaltungRecord Haltung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "10001-10002", FieldSource.Manual, false);
        record.SetFieldValue("Haltungslaenge_m", "42.5", FieldSource.Manual, false);
        record.Protocol = new ProtocolDocument
        {
            HaltungId = "10001-10002",
            Current = new ProtocolRevision
            {
                Entries = { Eintrag("BAB", 3.2), Eintrag("BBC", 12.5), Eintrag("BCA", 30.0) }
            }
        };
        return record;
    }

    private static ProtocolEntry Eintrag(string code, double meter)
        => new()
        {
            Code = code,
            Beschreibung = code,
            MeterStart = meter,
            CodeMeta = new ProtocolEntryCodeMeta { Code = code }
        };
}
