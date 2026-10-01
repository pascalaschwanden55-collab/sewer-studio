using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 6: das Tastenkürzel-Fenster wirklich in einem eigenen
/// WPF-Prozess bauen und prüfen (Muster wie <see cref="AboutWindowIsolatedSmokeTests"/>). Geprüft
/// wird, dass die globalen Kürzel vorhanden sind, dass die Player-Kürzel WIRKLICH aus
/// <see cref="PlayerKeyboardShortcutPolicy.Beschreibungen"/> stammen (nicht von Hand abgeschrieben -
/// ein Wert wird über die Quelle verändert und muss im Fenster ankommen), und dass
/// <see cref="TastenkuerzelWindow.ZeigeAn"/> das Fenster als Einzelstück hält.
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class TastenkuerzelWindowIsolatedSmokeTests
{
    [Fact]
    public async Task TastenkuerzelWindow_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(TastenkuerzelWindowIsolatedSmokeTests).FullName + "." + nameof(Kindprozess),
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

            var fenster = new TastenkuerzelWindow();
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
                Assert.Contains("Tastenkürzel", fenster.Title);

                var gruppenListe = (ItemsControl)fenster.FindName("GruppenListe");
                var gruppen = gruppenListe.ItemsSource
                    .Cast<TastenkuerzelWindow.TastenkuerzelGruppe>()
                    .ToList();

                // ── Die globalen Kürzel stehen namentlich drin ──
                var allgemein = gruppen.Single(g => g.Titel == "Allgemein");
                Assert.Contains(allgemein.Kuerzel, k => k.Taste == "F11" && k.Beschreibung.Contains("Fokusmodus"));
                Assert.Contains(allgemein.Kuerzel, k => k.Taste == "Strg+S");
                Assert.Contains(allgemein.Kuerzel, k => k.Taste == "Strg+K");
                Assert.Contains(allgemein.Kuerzel, k => k.Taste == "F1" && k.Beschreibung.Contains("Handbuch"));
                Assert.Contains(allgemein.Kuerzel, k => k.Taste == "Strg+F1");

                var haltungen = gruppen.Single(g => g.Titel == "Haltungen");
                Assert.Contains(haltungen.Kuerzel, k => k.Taste == "F3");

                // ── Player-Kürzel kommen wirklich aus PlayerKeyboardShortcutPolicy.Beschreibungen,
                //    nicht aus einer eigenen Abschrift: jede Gruppe/Taste/Text-Kombination der
                //    Policy muss in genau einer Player-Gruppe des Fensters vorkommen. ──
                foreach (var quelle in PlayerKeyboardShortcutPolicy.Beschreibungen)
                {
                    var gruppe = gruppen.SingleOrDefault(g => g.Titel == $"Videoplayer – {quelle.Gruppe}");
                    Assert.NotNull(gruppe);
                    Assert.Contains(
                        gruppe!.Kuerzel,
                        k => k.Taste == quelle.Taste && k.Beschreibung == quelle.Beschreibung);
                }

                // ── Anzahl Player-Gruppen entspricht genau der Anzahl distinkter Policy-Gruppen ──
                var policyGruppenAnzahl = PlayerKeyboardShortcutPolicy.Beschreibungen
                    .Select(b => b.Gruppe).Distinct().Count();
                Assert.Equal(policyGruppenAnzahl, gruppen.Count(g => g.Titel.StartsWith("Videoplayer – ")));

                var closeButton = (Button)fenster.FindName("CloseButton");
                Assert.True(closeButton.IsCancel);
                closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(fenster.IsVisible, "Der Schliessen-Knopf muss das Fenster wirklich schliessen.");

                // ── ZeigeAn haelt das Fenster als Einzelstueck ──
                TastenkuerzelWindow.ZeigeAn();
                var erstes = TastenkuerzelWindow.Aktuelles;
                Assert.NotNull(erstes);
                erstes!.Left = -20000;
                erstes.Top = -20000;

                TastenkuerzelWindow.ZeigeAn();
                Assert.Same(erstes, TastenkuerzelWindow.Aktuelles);
                erstes.Close();
                Assert.Null(TastenkuerzelWindow.Aktuelles);
            }
            finally
            {
                if (fenster.IsVisible)
                    fenster.Close();
                TastenkuerzelWindow.Aktuelles?.Close();
            }

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }
}
