using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Audit A15 (23.09.2026): Die Schachttabelle zeigte bei Auswahlspalten ohne eigene Liste (Status,
/// Sanierungsbedarf, Nutzungsart, Material …) einen vorhandenen Wert leer. Wer das leere Feld anklickte,
/// ueberschrieb ihn; und schon das blosse Verlassen der Zelle schrieb den Wert mit Handmarke neu — ein
/// Kanalfirma-Wert waere so ohne Haekchen ins WebGIS gegangen.
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class SchachtTabellenAuswahlTests
{
    [Fact]
    public void Altwert_wird_hinten_angehaengt()
    {
        var liste = SchachtTabellenAuswahl.MitAltwert(new[] { "", "In Betrieb" }, "weitere");
        Assert.Equal(new[] { "", "In Betrieb", "weitere" }, liste);
    }

    [Theory]
    [InlineData("In Betrieb")]
    [InlineData("")]
    [InlineData(null)]
    public void Wert_aus_der_liste_oder_leer_aendert_die_liste_nicht(string? aktuell)
        => Assert.Equal(new[] { "", "In Betrieb" }, SchachtTabellenAuswahl.MitAltwert(new[] { "", "In Betrieb" }, aktuell));

    [Theory]
    [InlineData("In Betrieb", "In Betrieb", false)]
    [InlineData("In Betrieb", " In Betrieb ", false)]
    [InlineData("", "In Betrieb", true)]
    [InlineData("In Betrieb", "Tot/Aufgehoben, verfüllt", true)]
    public void Nur_eine_echte_aenderung_wird_geschrieben(string bisher, string neu, bool erwartet)
        => Assert.Equal(erwartet, SchachtTabellenAuswahl.IstAenderung(bisher, neu));

    [Fact]
    public async Task Echter_zelleditor_zeigt_vorhandene_werte_und_leert_nichts()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(SchachtTabellenAuswahlTests).FullName + "." + nameof(Kindprozess), TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            // Bewusst die schlichte Application, nicht App: So laeuft beim Warten auf das Layout kein
            // Programmstart mit (23.09.2026: ein Fehlerdialog des App-Starts erschien auf dem Bildschirm).
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var liste = FieldCatalog.GetComboItems(FieldKeys.OperatingStatus);
            foreach (var wert in new[] { "In Betrieb", "weitere" })
            {
                var s = new SchachtRecord();
                s.SetFieldValue(FieldKeys.OperatingStatus, wert, FieldSource.Xtf405, userEdited: false);
                var gespeichert = s.GetFieldValue(FieldKeys.OperatingStatus);
                // Dieselben Parameter wie die Schachtseite fuer eine nicht verwaltete Liste.
                var spalte = DataGridComboColumnFactory.Create(FieldKeys.OperatingStatus, "Status", "StatusOptions", "Status",
                    (_, _) => { }, (_, _) => { }, allowFreeText: false, bindIsProjectReady: false,
                    useSelectedItemWhenNotFreeText: false);
                spalte.CellEditingTemplate.Seal();
                var combo = (ComboBox)spalte.CellEditingTemplate.LoadContent();
                combo.ItemsSource = SchachtTabellenAuswahl.MitAltwert(liste, gespeichert);
                combo.DataContext = s;
                var host = new Window { Content = combo, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000, Width = 300, Height = 60 };
                host.Show();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                Assert.Equal(gespeichert, combo.SelectionBoxItem?.ToString());

                // Waehlt jemand den leeren Eintrag, darf die Bindung das Feld nicht still leeren.
                combo.SelectedItem = "";
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(gespeichert, s.GetFieldValue(FieldKeys.OperatingStatus));
                Assert.False(s.FieldMeta[FieldKeys.OperatingStatus].UserEdited);
                host.Close();
            }

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }
}
