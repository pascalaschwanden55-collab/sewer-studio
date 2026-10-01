using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 5, Fix-Runde 1 nach Controller-Sichtprobe): Das erste
/// Prüfhost-Bild zeigte einen schmalen Streifen mit Pixelstaub-Beschriftung — die Grafik nutzte
/// nicht die volle Panelbreite und unterschritt nach der Viewbox-Skalierung die
/// Lesbarkeitsgrenze. Dieser Test misst genau das nach, analog
/// <c>HaltungsgrafikControlIsolatedSmokeTests.DieRohrsaeuleBleibtLesbar</c>: Skalenfaktor der
/// Viewbox mal Schriftgrösse bzw. Strichstärke.
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class SchachtgrafikControlIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(SchachtgrafikControlIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_zeichnet_die_Schachtgrafik_lesbar);

    [Fact]
    public async Task Schachtgrafik_laesst_sich_in_eigenem_Wpf_Prozess_zeichnen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_zeichnet_die_Schachtgrafik_lesbar()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            DieGrafikNutztDieVollePanelbreiteUndBleibtLesbar(app);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    /// <summary>
    /// Breite wie die Uebersicht in der Standardbreite der Spalte (Muster
    /// <c>HaltungsgrafikControlIsolatedSmokeTests.DieRohrsaeuleBleibtLesbar</c>, dort 300 px).
    /// Stammkarte (19.09.2026): Die Hoehe ist nicht mehr fest — sie folgt dem Seitenverhaeltnis
    /// der Zeichnung (Schnitt und Grundriss untereinander), der Massstab ist BREITENGEBUNDEN und
    /// die Zeichnung fuellt die Spalte aus.
    /// </summary>
    private static void DieGrafikNutztDieVollePanelbreiteUndBleibtLesbar(App app)
    {
        var grafik = new SchachtgrafikControl
        {
            Record = Schacht(),
            Haltungen = Haltungen()
        };
        grafik.Measure(new Size(300, double.PositiveInfinity));
        grafik.Arrange(new Rect(0, 0, 300, grafik.DesiredSize.Height));
        grafik.UpdateLayout();
        grafik.ZeichneJetzt();
        grafik.Measure(new Size(300, double.PositiveInfinity));
        grafik.Arrange(new Rect(0, 0, 300, grafik.DesiredSize.Height));
        grafik.UpdateLayout();

        var buehne = Assert.IsType<Viewbox>(grafik.FindName("Buehne"));
        var flaeche = Assert.IsType<Canvas>(buehne.Child);

        var breitenSkala = buehne.ActualWidth / flaeche.Width;
        var hoehenSkala = buehne.ActualHeight / flaeche.Height;
        var skala = Math.Min(breitenSkala, hoehenSkala);
        Assert.True(
            breitenSkala <= hoehenSkala + 0.01,
            $"Die Grafik ist nicht breitengebunden (Breitenskala {breitenSkala:0.00} vs Hoehenskala {hoehenSkala:0.00}) " +
            "— sie nutzt nicht die volle Panelbreite.");

        var formen = flaeche.Children.OfType<FrameworkElement>().ToList();

        var texte = formen.OfType<TextBlock>().ToList();
        Assert.NotEmpty(texte);
        foreach (var text in texte)
        {
            Assert.True(
                text.FontSize * skala >= 9d,
                $"Beschriftung \"{text.Text}\" nur {text.FontSize * skala:0.0} px gross");
        }

        // Stammkarte (19.09.2026): Anschluesse sind Rechtecke, Gerinne und Wasser sind Pfade,
        // der Tiefenmassstab steht in Textfarbe. Linien in MutedBrush/AccentBrush gibt es kaum
        // noch; wo der Bauer eine schreibt, muss sie nach der Skalierung 3 px behalten
        // (Controller-Ruling Fix-Runde 1). Schadensmarken sind Kreise, keine duennen Linien.
        var mutedColor = ((SolidColorBrush)app.FindResource("MutedBrush")).Color;
        var accentColor = ((SolidColorBrush)app.FindResource("AccentBrush")).Color;
        var linien = formen.OfType<Line>()
            .Where(l => l.Stroke is SolidColorBrush stroke && (stroke.Color == mutedColor || stroke.Color == accentColor))
            .ToList();
        foreach (var linie in linien)
        {
            Assert.True(
                linie.StrokeThickness * skala >= 3d,
                $"Linie nur {linie.StrokeThickness * skala:0.0} px dick");
        }

        // Der Schachtkoerper (Schachtwand/Sohle) bleibt ein breiter Koerper, kein schmaler
        // Streifen: mindestens ein Drittel der sichtbaren Panelbreite.
        var koerper = formen.OfType<Rectangle>()
            .Where(r => r.Fill is SolidColorBrush && r.Width > 0 && r.Width < flaeche.Width - 1)
            .OrderByDescending(r => r.Width)
            .First();
        Assert.True(
            koerper.Width * skala >= buehne.ActualWidth / 3d - 2d,
            $"Schachtkoerper nur {koerper.Width * skala:0.0} px breit auf {buehne.ActualWidth:0.0} px Panelbreite");
    }

    private static SchachtRecord Schacht()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "10001", FieldSource.Manual, false);
        record.SetFieldValue("Schachttiefe", "2.40", FieldSource.Manual, false);
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "1100", FieldSource.Manual, false);
        record.SetFieldValue(FieldKeys.ShaftDimension2Mm, "900", FieldSource.Manual, false);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new ProtocolEntry { Code = "Konus", Beschreibung = "gerissen" },
                    new ProtocolEntry { Code = "Bankett", Beschreibung = "Ablagerung" },
                }
            }
        };
        return record;
    }

    private static IReadOnlyList<HaltungRecord> Haltungen()
    {
        var ablauf = new HaltungRecord();
        ablauf.SetFieldValue(FieldKeys.HoldingName, "10001-10002", FieldSource.Manual, false);
        ablauf.SetFieldValue("Schacht_oben", "10001", FieldSource.Manual, false);
        ablauf.SetFieldValue("Schacht_unten", "10002", FieldSource.Manual, false);
        ablauf.SetFieldValue(FieldKeys.NominalDiameterMm, "200", FieldSource.Manual, false);
        return [ablauf];
    }
}
