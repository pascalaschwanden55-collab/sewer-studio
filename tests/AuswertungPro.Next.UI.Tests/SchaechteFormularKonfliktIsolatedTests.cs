using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// W01 fuer Schaechte, sichtbarer Teil: Eine verworfene Formulareingabe steht als Hinweis in der
/// Kopfzeile genau des Formulars, das sie gezeigt hat — in der Liste die aufgeklappte Zeile, in
/// der Tabelle die Eingabefelder-Schublade. Dieselbe Weiche wie bei den Haltungen
/// (<see cref="DataPageAnsichtUmschalter.MeldeKonflikt"/>).
///
/// Laeuft wie die anderen WPF-Tests in einem eigenen Kindprozess; kein Projekt, kein ViewModel.
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class SchaechteFormularKonfliktIsolatedTests
{
    private const string Feld = "Bemerkung";

    private static readonly string ChildTestName =
        typeof(SchaechteFormularKonfliktIsolatedTests).FullName
        + "."
        + nameof(Kindprozess_zeigt_den_Konflikthinweis_im_sichtbaren_Formular);

    [Fact]
    public async Task Konflikthinweis_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(90));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_zeigt_den_Konflikthinweis_im_sichtbaren_Formular()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var datensaetze = new ObservableCollection<SchachtRecord>(Enumerable.Range(1, 3).Select(Datensatz));
            var liste = new SchachtAufklappListe { ItemsSource = datensaetze };
            var schublade = new HaltungFelderDrawer();
            SchaechteAnsichtUmschalter? umschalter = null;
            // Wie die Seite: Commit in alle Schreibweisen, Konflikt an die Weiche.
            var builder = new SchaechteRecordDetailsBuilder(
                _ => Array.Empty<string>(),
                _ => null,
                (record, feld, wert) =>
                {
                    foreach (var key in feld.AlleKeys)
                        record.SetFieldValue(key, wert ?? "", FieldSource.Manual, userEdited: true);
                },
                konfliktGemeldet: (feld, aktuell, eingabe) => umschalter!.MeldeKonflikt(feld, aktuell, eingabe));
            umschalter = new SchaechteAnsichtUmschalter(
                new SchaechteAnsichtUmschalter.Elemente(
                    new DataGrid(), new Border(), liste, new Border(),
                    new MenuItem(), new MenuItem(), new MenuItem(), schublade),
                () => null,
                () => { },
                (_, _) => { });
            var controller = new SchaechteAufklappListeController(
                liste, () => null, record => builder.Build(Array.Empty<string>(), record));
            controller.Verdrahte();
            umschalter.Waehle("liste");

            liste.KlappeAuf(datensaetze[0]);
            Layout(liste);
            Assert.True(umschalter.ListeZeigtFormular);
            var item = liste.Themen!.SelectMany(t => t.EinzelGruppe).SelectMany(g => g.Items)
                .Single(i => i.FieldName == Feld);

            // Die Tabelle korrigiert denselben Wert, waehrend das Formular den Fokus hat.
            item.IsEditing = true;
            datensaetze[0].SetFieldValue(Feld, "Tabellenkorrektur", FieldSource.Manual, userEdited: true);
            item.Value = "Alt + Zusatz";

            Assert.Equal("Tabellenkorrektur", datensaetze[0].GetFieldValue(Feld));
            Assert.Contains("„Tabellenkorrektur“", liste.Hinweis);
            Assert.Contains("„Alt + Zusatz“", liste.Hinweis);
            Assert.Empty(schublade.Hinweis);
            Layout(liste);
            Assert.Contains(Alle<TextBlock>(liste), t => t.Text == liste.Hinweis && t.Visibility == Visibility.Visible);

            // Ein neu aufgebautes Formular beginnt ohne Hinweis.
            controller.AktualisiereFormular();
            Assert.Empty(liste.Hinweis);

            // In der Tabelle gehoert der Hinweis in die Eingabefelder-Schublade.
            umschalter.Waehle("tabelle");
            Assert.False(umschalter.ListeZeigtFormular);
            umschalter.MeldeKonflikt(Feld, "Neu", "Alt");
            Assert.Contains("„Neu“", schublade.Hinweis);
            Assert.Empty(liste.Hinweis);

            // Detailfenster (Review PR #75): Der Hinweis erscheint im Fenster, in dem eingegeben
            // wurde - nicht in Liste oder Schublade dahinter.
            schublade.Hinweis = string.Empty;
            var fensterGruppen = builder.Build(Array.Empty<string>(), datensaetze[1]);
            var fenster = new AuswertungPro.Next.UI.Views.Windows.RecordDetailsWindow("Schachtdetails", "Schacht S2", "", fensterGruppen);
            var fensterFeld = fensterGruppen.SelectMany(g => g.Items).Single(i => i.FieldName == Feld);
            datensaetze[1].SetFieldValue(Feld, "Tabellenkorrektur", FieldSource.Manual, userEdited: true);
            fensterFeld.Value = "Alt + Zusatz";

            Assert.Equal("Tabellenkorrektur", datensaetze[1].GetFieldValue(Feld));
            Assert.Contains("„Alt + Zusatz“", fenster.Hinweis);
            Assert.Empty(schublade.Hinweis);
            Assert.Empty(liste.Hinweis);
            var hinweisText = Assert.IsType<TextBlock>(fenster.FindName("KonfliktHinweisText"));
            Assert.Equal(fenster.Hinweis, hinweisText.Text);
            Assert.Equal(Visibility.Visible, hinweisText.Visibility);
            fenster.Close();

            controller.Dispose();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static SchachtRecord Datensatz(int nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", $"S{nummer}", FieldSource.Pdf, false);
        record.SetFieldValue(Feld, "Alt", FieldSource.Pdf, false);
        return record;
    }

    private static List<T> Alle<T>(DependencyObject wurzel) where T : DependencyObject
    {
        var treffer = new List<T>();
        Sammle(wurzel, treffer);
        return treffer;
    }

    private static void Sammle<T>(DependencyObject knoten, List<T> treffer) where T : DependencyObject
    {
        var anzahl = VisualTreeHelper.GetChildrenCount(knoten);
        for (var i = 0; i < anzahl; i++)
        {
            var kind = VisualTreeHelper.GetChild(knoten, i);
            if (kind is T passend)
                treffer.Add(passend);
            Sammle(kind, treffer);
        }
    }

    private static void Layout(UIElement element, double breite = 1400, double hoehe = 800)
    {
        element.Measure(new Size(breite, hoehe));
        element.Arrange(new Rect(0, 0, breite, hoehe));
        element.UpdateLayout();
    }
}
