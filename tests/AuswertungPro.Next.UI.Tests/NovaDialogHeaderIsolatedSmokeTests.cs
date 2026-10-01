using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 3 («Fensterregel - gemeinsamer Kopf und Knopfleiste, Teil 1:
/// Grundlage + Dossier-Fenster»): baut die drei neuen Theme-Bausteine wirklich auf (Muster wie
/// <see cref="NovaPageHeaderIsolatedSmokeTests"/> und <see cref="NovaDialogWindowIsolatedSmokeTests"/>) -
/// <see cref="NovaDialogHeader"/> (Titel + umbrechender Untertitel, kollabiert bei leer),
/// <c>DialogButtonBar</c> (Border-Stil: Trennlinie oben, 16 px Rand) und <c>DangerButton</c>
/// (Umriss in DangerBrush wie das bisherige fensterlokale <c>NovaDialogDangerButton</c>).
///
/// Die <see cref="NovaDialogHeader"/>-Faelle brauchen ein echtes gezeigtes Fenster: Ihr
/// impliziter Stil (<c>DefaultStyleKey</c>, kein <c>x:Key</c>) loest sich nur zuverlaessig auf,
/// wenn das Control tatsaechlich Teil eines angezeigten Fensters ist - ein freistehendes,
/// nirgends eingehaengtes Control fand die Anwendungsressourcen in einer ersten Fassung dieses
/// Tests NICHT (Focusable blieb der CLR-Standard <c>true</c>).
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class NovaDialogHeaderIsolatedSmokeTests
{
    [Fact]
    public async Task NovaDialogHeader_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(NovaDialogHeaderIsolatedSmokeTests).FullName + "." + nameof(Kindprozess),
            TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var resource in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SewerStudio;component/" + resource, UriKind.Relative) });

            // ── 1) NovaDialogHeader: Titel sichtbar, Untertitel kollabiert bei leer und ist
            //      sichtbar+umbrechend wenn gesetzt; kein Tab-Stopp/Fokusrahmen. ──
            var kopfOhneUntertitel = new NovaDialogHeader { Title = "Liegenschaft" };
            ZeigeInFenster(kopfOhneUntertitel);
            kopfOhneUntertitel.ApplyTemplate();

            Assert.False(kopfOhneUntertitel.Focusable, "NovaDialogHeader darf nicht fokussierbar sein.");
            Assert.False(kopfOhneUntertitel.IsTabStop, "NovaDialogHeader darf kein Tab-Stopp sein.");

            var titelOhne = Assert.IsType<TextBlock>(kopfOhneUntertitel.Template.FindName("TitleText", kopfOhneUntertitel));
            Assert.Equal("Liegenschaft", titelOhne.Text);
            Assert.Equal(Visibility.Visible, titelOhne.Visibility);

            var untertitelOhne = Assert.IsType<TextBlock>(kopfOhneUntertitel.Template.FindName("SubtitleText", kopfOhneUntertitel));
            Assert.Equal(Visibility.Collapsed, untertitelOhne.Visibility);

            var langerSatz = "Diese Angaben gelten für ALLE Dossiers dieses Projekts. Einzelne Liegenschaften können sie überschreiben.";
            var kopfMitUntertitel = new NovaDialogHeader { Title = "Gebietsangaben", Subtitle = langerSatz };
            ZeigeInFenster(kopfMitUntertitel);
            kopfMitUntertitel.ApplyTemplate();

            var untertitelMit = Assert.IsType<TextBlock>(kopfMitUntertitel.Template.FindName("SubtitleText", kopfMitUntertitel));
            Assert.Equal(langerSatz, untertitelMit.Text);
            Assert.Equal(Visibility.Visible, untertitelMit.Visibility);
            // Der Untertitel ist ein ganzer erklaerender Satz und muss UMBRECHEN duerfen (das
            // unterscheidet NovaDialogHeader vom inline-einzeiligen NovaPageHeader-Untertitel).
            Assert.Equal(TextWrapping.Wrap, untertitelMit.TextWrapping);

            // Der Aktionen-Inhalt haengt am DataContext des Kopfes, nicht an einer isolierten
            // eigenen Namescope (gleiches Muster wie NovaPageHeader).
            var eigenerContext = new object();
            var aktionenInhalt = new TextBlock();
            var kopfMitAktionen = new NovaDialogHeader { Title = "Titel", DataContext = eigenerContext, Aktionen = aktionenInhalt };
            ZeigeInFenster(kopfMitAktionen);
            kopfMitAktionen.ApplyTemplate();
            Assert.Same(aktionenInhalt, kopfMitAktionen.Aktionen);

            // ── 2) DialogButtonBar: Trennlinie oben in BorderLightBrush, 16 px Abstand darunter,
            //      keine Trennlinie an den anderen Seiten. Ein Border braucht dafuer kein eigenes
            //      Fenster - der Stil wird explizit zugewiesen (kein DefaultStyleKey-Nachschlag). ──
            var borderLight = (SolidColorBrush)app.Resources["BorderLightBrush"];
            var dialogButtonBar = (Style)app.Resources["DialogButtonBar"];
            Assert.Equal(typeof(Border), dialogButtonBar.TargetType);

            var leiste = new Border { Style = dialogButtonBar };
            Layout(leiste);
            Assert.Equal(new Thickness(0, 1, 0, 0), leiste.BorderThickness);
            Assert.Equal(new Thickness(0, 16, 0, 0), leiste.Padding);
            Assert.Equal(borderLight.Color, ((SolidColorBrush)leiste.BorderBrush).Color);

            // ── 3) DangerButton: Umriss/Text in DangerBrush statt Flaechenfuellung, wie das
            //      bisherige fensterlokale NovaDialogDangerButton (Aufgabe 1). ──
            var dangerButtonStyle = (Style)app.Resources["DangerButton"];
            var secondaryButtonStyle = (Style)app.Resources["SecondaryButton"];
            var dangerBrush = (SolidColorBrush)app.Resources["DangerBrush"];
            var dangerTextBrush = (SolidColorBrush)app.Resources["DangerTextBrush"];
            Assert.Same(secondaryButtonStyle, dangerButtonStyle.BasedOn);

            var dangerKnopf = new Button { Style = dangerButtonStyle, Content = "Löschen" };
            Layout(dangerKnopf);
            dangerKnopf.ApplyTemplate();
            Assert.Equal(dangerTextBrush.Color, ((SolidColorBrush)dangerKnopf.Foreground).Color);
            Assert.Equal(dangerBrush.Color, ((SolidColorBrush)dangerKnopf.BorderBrush).Color);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }

    private static Window ZeigeInFenster(UIElement inhalt)
    {
        var fenster = new Window
        {
            Content = inhalt,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false
        };
        fenster.Show();
        fenster.UpdateLayout();
        fenster.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        return fenster;
    }

    private static void Layout(UIElement element)
    {
        element.Measure(new Size(1400, 900));
        element.Arrange(new Rect(0, 0, 1400, 900));
        element.UpdateLayout();
    }
}
