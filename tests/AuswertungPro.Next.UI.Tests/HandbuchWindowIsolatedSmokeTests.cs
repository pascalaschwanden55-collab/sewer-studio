using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 6 («Hilfe-Menü, F1, Tastenkürzel, Handbuch»): das Handbuchfenster
/// wirklich in einem eigenen WPF-Prozess bauen und prüfen (Muster wie
/// <see cref="AboutWindowIsolatedSmokeTests"/>). Geprüft werden: Inhaltsverzeichnis vollständig aus
/// <see cref="HandbuchInhalt"/>, F1-Sprung zum richtigen Abschnitt, Fachleute-Abschnitt
/// standardmässig eingeklappt, Suche filtert, und <see cref="HandbuchWindow.ZeigeAn"/> hält das
/// Fenster als Einzelstück (kein zweites Fenster beim zweiten Aufruf).
/// </summary>
[Collection("IsolatedWpf")]
public sealed class HandbuchWindowIsolatedSmokeTests
{
    [Fact]
    public async Task HandbuchWindow_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(HandbuchWindowIsolatedSmokeTests).FullName + "." + nameof(Kindprozess),
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
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var fenster = new HandbuchWindow();
            fenster.ShowActivated = false;
            fenster.ShowInTaskbar = false;
            fenster.WindowStartupLocation = WindowStartupLocation.Manual;
            fenster.Left = -20000;
            fenster.Top = -20000;
            fenster.Show();
            fenster.UpdateLayout();

            try
            {
                Assert.True(fenster.IsLoaded);
                Assert.Contains("SewerStudio", fenster.Title);
                Assert.Contains("Handbuch", fenster.Title);

                // ── Inhaltsverzeichnis: alle Abschnitte aus HandbuchInhalt, nichts erfunden ──
                var toc = (ListBox)fenster.FindName("InhaltsverzeichnisListe");
                var tocSchluessel = toc.Items
                    .OfType<HandbuchWindow.HandbuchTocEintrag>()
                    .Select(e => e.Abschnitt.Schluessel)
                    .ToList();
                Assert.Equal(HandbuchInhalt.Abschnitte.Count, tocSchluessel.Count);
                foreach (var abschnitt in HandbuchInhalt.Abschnitte)
                    Assert.Contains(abschnitt.Schluessel, tocSchluessel);

                // ── Start ohne Angabe: erster Abschnitt (Übersicht) ──
                var titelText = (TextBlock)fenster.FindName("AbschnittTitelText");
                Assert.Equal(HandbuchInhalt.Abschnitte[0].Titel, titelText.Text);

                // ── F1 mit Seitenschlüssel springt zum richtigen Abschnitt ──
                fenster.ZeigeAbschnitt("Haltungen");
                Assert.Equal(HandbuchInhalt.Finde("Haltungen").Titel, titelText.Text);
                var host = (ContentControl)fenster.FindName("AbschnittInhaltHost");
                Assert.IsType<TextBlock>(host.Content);

                // ── Unbekannter Schlüssel fällt auf Übersicht zurück ──
                fenster.ZeigeAbschnitt("gibt es nicht");
                Assert.Equal(HandbuchInhalt.Abschnitte[0].Titel, titelText.Text);

                // ── Fachleute-Abschnitt: Inhalt steckt in einem standardmässig zugeklappten Expander ──
                fenster.ZeigeAbschnitt(HandbuchInhalt.FachleuteSchluessel);
                Assert.Equal(HandbuchInhalt.Abschnitte[^1].Titel, titelText.Text);
                var expander = Assert.IsType<Expander>(host.Content);
                Assert.False(expander.IsExpanded, "Der Fachleute-Abschnitt muss standardmässig zu sein.");
                Assert.IsType<TextBlock>(expander.Content);

                // ── Suche filtert das Inhaltsverzeichnis ──
                fenster.ZeigeAbschnitt("Uebersicht");
                var sucheBox = (TextBox)fenster.FindName("SucheBox");
                sucheBox.Text = "NPK";
                sucheBox.RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
                fenster.UpdateLayout();
                var sichtbareNachSuche = toc.Items.OfType<HandbuchWindow.HandbuchTocEintrag>().ToList();
                Assert.True(sichtbareNachSuche.Count < HandbuchInhalt.Abschnitte.Count,
                    "Eine Suche nach einem Begriff, der nur in einem Abschnitt vorkommt, muss die Liste einschränken.");

                sucheBox.Text = string.Empty;
                sucheBox.RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
                fenster.UpdateLayout();
                Assert.Equal(
                    HandbuchInhalt.Abschnitte.Count,
                    toc.Items.OfType<HandbuchWindow.HandbuchTocEintrag>().Count());

                // ── ZeigeAn haelt das Fenster als Einzelstueck ──
                HandbuchWindow.ZeigeAn("Export");
                var erstes = HandbuchWindow.Aktuelles;
                Assert.NotNull(erstes);
                if (!ReferenceEquals(erstes, fenster))
                {
                    erstes!.Left = -20000;
                    erstes.Top = -20000;
                }

                HandbuchWindow.ZeigeAn("VSA");
                Assert.Same(erstes, HandbuchWindow.Aktuelles);

                var closeButton = (Button)erstes!.FindName("CloseButton");
                Assert.True(closeButton.IsCancel);
                closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Null(HandbuchWindow.Aktuelles);
            }
            finally
            {
                if (fenster.IsVisible)
                    fenster.Close();
                HandbuchWindow.Aktuelles?.Close();
            }

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }
}
