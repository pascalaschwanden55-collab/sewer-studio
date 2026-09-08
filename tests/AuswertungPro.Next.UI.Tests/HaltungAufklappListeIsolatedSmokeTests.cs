using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Controls;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 1): Das Control baut sich mit den echten App-Ressourcen auf.
///
/// Der Kern der Pruefung: Bei 40 Haltungen entstehen nur Kopfzeilen und KEIN Formular. Erst die
/// aufgeklappte Haltung bekommt eines, immer nur eine (Akkordeon), und jede Feldaenderung laeuft
/// ueber den bestehenden Rueckschreibweg der <see cref="DataPageDetailItemFactory"/>.
///
/// Laeuft wie die anderen WPF-Smoke-Tests in einem eigenen Kindprozess; kein Projekt, kein
/// ViewModel, kein Fensterstart. Ohne Fenster gibt es keinen echten Tastaturfokus — die
/// Tastatur wird deshalb ueber <c>VerarbeiteTaste</c> gefahren, also ueber genau denselben Weg,
/// den der KeyDown-Handler nimmt.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class HaltungAufklappListeIsolatedSmokeTests
{
    private const string Bemerkungen = "Bemerkungen";

    private static readonly string ChildTestName =
        typeof(HaltungAufklappListeIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_zeigt_das_Formular_nur_fuer_die_aufgeklappte_Haltung);

    [Fact]
    public async Task Aufklapp_Liste_laesst_sich_in_eigenem_Wpf_Prozess_aufbauen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(90));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_zeigt_das_Formular_nur_fuer_die_aufgeklappte_Haltung()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var datensaetze = Enumerable.Range(1, 40).Select(Datensatz).ToList();
            var fabrik = new DataPageDetailItemFactory(
                _ => null,
                (record, feld, wert) => record.SetFieldValue(feld, wert, FieldSource.Manual, userEdited: true));

            var liste = new HaltungAufklappListe { ItemsSource = datensaetze };
            var controller = new DataPageAufklappListeController(
                liste,
                () => null,
                record => DataPageRecordDetailsBuilder.Build(record, feld => fabrik.Create(feld, record)));
            controller.Verdrahte();
            Layout(liste);

            // Zugeklappt: 40 Kopfzeilen, kein einziges Formular.
            Assert.Null(liste.Aufgeklappt);
            Assert.Null(liste.Themen);
            Assert.Empty(Alle<RecordDetailsView>(liste));
            // Virtualisierung: Es entstehen nur die sichtbaren Zeilen, nicht alle 40.
            var zeilenAmAnfang = Alle<ListBoxItem>(liste).Count;
            Assert.InRange(zeilenAmAnfang, 1, datensaetze.Count - 1);

            // Aufklappen: genau ein Formular mit den fuenf Themen des Detail-Builders.
            liste.KlappeAuf(datensaetze[0]);
            Layout(liste);

            Assert.Same(datensaetze[0], liste.Aufgeklappt);
            Assert.Same(datensaetze[0], liste.SelectedItem);
            var themen = liste.Themen!;
            Assert.Equal(
                ["Stammdaten", "Bewertung", "Sanierung", "Kosten und Bemerkungen", "Weitere Angaben"],
                themen.Select(t => t.Title));
            Assert.Equal([17, 9, 11, 3], themen.Take(4).Select(t => t.Anzahl));
            Assert.True(themen[4].Anzahl > 0, "Weitere Angaben darf nicht leer sein");
            // Vier Formulare: Das zugeklappte Thema "Weitere Angaben" baut seinen Inhalt erst
            // beim Aufklappen auf — ein collapsed ContentPresenter wird nie gemessen.
            Assert.Equal(4, Alle<RecordDetailsView>(liste).Count);
            Assert.Contains(Alle<AuswertungPro.Next.UI.FluentIcon>(liste),
                icon => icon.RenderTransform is RotateTransform { Angle: 90 });
            // Fix-Runde 1: Der Pfeil sagt, was der Klick tut.
            Assert.Equal(
                PfeilBeschriftungConverter.Zuklappen,
                Pfeilknopf(liste, datensaetze[0]).GetValue(System.Windows.Automation.AutomationProperties.NameProperty));
            Assert.Equal(
                PfeilBeschriftungConverter.Aufklappen,
                Pfeilknopf(liste, datensaetze[1]).GetValue(System.Windows.Automation.AutomationProperties.NameProperty));

            // "Alle auf" klappt auch das fuenfte Thema auf.
            Klick(liste, "Alle Themen aufklappen");
            Layout(liste);
            Assert.Equal(5, Alle<RecordDetailsView>(liste).Count);

            // Feldaenderung im Formular geht ueber den bestehenden Rueckschreibweg.
            var bemerkungen = themen
                .Single(t => t.Title == "Kosten und Bemerkungen")
                .EinzelGruppe[0].Items.Single(i => i.FieldName == Bemerkungen);
            bemerkungen.Value = "Aus dem Formular";
            Assert.Equal("Aus dem Formular", datensaetze[0].GetFieldValue(Bemerkungen));
            Assert.Equal(FieldSource.Manual, datensaetze[0].FieldMeta[Bemerkungen].Source);
            Assert.True(datensaetze[0].FieldMeta[Bemerkungen].UserEdited);

            // Eine Aenderung am Datensatz (Tabelle, Dienst) erscheint sofort im Formular.
            datensaetze[0].SetFieldValue(Bemerkungen, "Von aussen", FieldSource.Manual, userEdited: true);
            Assert.Equal("Von aussen", bemerkungen.Value);

            // Fix-Runde 1: Der Auf-/Zuklappzustand haengt am Thema, nicht am Expander.
            var stammdaten = themen[0];
            Expander(liste, stammdaten).IsExpanded = false;
            Layout(liste);
            Assert.False(stammdaten.IstAufgeklappt);

            // Unter 1100 px stehen die Themen in zwei Spalten statt in einer Zeile. Der Wechsel
            // der Anordnung baut alle Expander neu — der Zustand muss ihn ueberleben.
            Layout(liste, breite: 1000);
            Assert.False(Expander(liste, stammdaten).IsExpanded);
            Assert.False(stammdaten.IstAufgeklappt);
            Layout(liste, breite: 1400);
            Assert.False(Expander(liste, stammdaten).IsExpanded);

            // Scrollen ans Ende und zurueck aendert weder Auswahl noch Zustand.
            var bildlauf = Alle<ScrollViewer>(liste).First();
            bildlauf.ScrollToBottom();
            Layout(liste);
            Assert.Same(datensaetze[0], liste.Aufgeklappt);
            bildlauf.ScrollToTop();
            Layout(liste);
            Assert.Same(datensaetze[0], liste.Aufgeklappt);
            Assert.False(stammdaten.IstAufgeklappt);
            Assert.False(Expander(liste, stammdaten).IsExpanded);

            // Akkordeon: Die naechste Haltung ersetzt das Formular; der alte Abgleich ist beendet.
            liste.KlappeAuf(datensaetze[1]);
            Layout(liste);
            Assert.Same(datensaetze[1], liste.Aufgeklappt);
            Assert.Equal(4, Alle<RecordDetailsView>(liste).Count);
            Assert.Equal(5, liste.Themen!.Count);
            datensaetze[0].SetFieldValue(Bemerkungen, "Nach dem Wechsel", FieldSource.Manual, userEdited: true);
            Assert.Equal("Von aussen", bemerkungen.Value);

            // Fix-Runde 1: Eine Pfeiltaste wechselt nur die Auswahl — sie klappt NICHT auf.
            var zeile2 = Zeile(liste, datensaetze[2]);
            Assert.False(liste.VerarbeiteTaste(Key.Down, zeile2));
            liste.SelectedItem = datensaetze[2];
            Layout(liste);
            Assert.Same(datensaetze[1], liste.Aufgeklappt);

            // Enter auf der Zeile klappt auf und wieder zu.
            Assert.True(liste.VerarbeiteTaste(Key.Enter, zeile2));
            Layout(liste);
            Assert.Same(datensaetze[2], liste.Aufgeklappt);
            Assert.True(liste.VerarbeiteTaste(Key.Enter, Zeile(liste, datensaetze[2])));
            Layout(liste);
            Assert.Null(liste.Aufgeklappt);

            // Fix-Runde 1: Escape AUS einem Eingabefeld klappt nicht zu — die Eingabe wird auf
            // dem normalen Weg zurueckgeschrieben, das Formular bleibt stehen.
            liste.KlappeAuf(datensaetze[3]);
            Layout(liste);
            var editor = Alle<TextBox>(liste)
                .First(t => t.DataContext is RecordDetailItem { FieldName: Bemerkungen });
            var item = (RecordDetailItem)editor.DataContext;
            item.IsEditing = true;
            editor.Text = "Im Feld getippt";
            editor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            Assert.True(liste.VerarbeiteTaste(Key.Escape, editor));
            Assert.Same(datensaetze[3], liste.Aufgeklappt);
            Assert.Equal("Im Feld getippt", datensaetze[3].GetFieldValue(Bemerkungen));
            Assert.Equal(FieldSource.Manual, datensaetze[3].FieldMeta[Bemerkungen].Source);
            Assert.True(datensaetze[3].FieldMeta[Bemerkungen].UserEdited);

            // Erst der zweite Escape, jetzt auf der Zeile, klappt zu.
            Assert.True(liste.VerarbeiteTaste(Key.Escape, Zeile(liste, datensaetze[3])));
            Layout(liste);
            Assert.Null(liste.Aufgeklappt);
            Assert.Null(liste.Themen);
            Assert.Empty(Alle<RecordDetailsView>(liste));

            // Ohne Auswahl: kein Fehltext, keine Ausnahme.
            liste.KlappeAuf(null);
            Layout(liste);
            Assert.Null(liste.Aufgeklappt);
            Assert.Equal(string.Empty, liste.Hinweis);

            var leer = new HaltungAufklappListe();
            Layout(leer);
            Assert.Empty(Alle<RecordDetailsView>(leer));

            controller.Dispose();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    /// <summary>Die erzeugte Zeile dieser Haltung.</summary>
    private static ListBoxItem Zeile(DependencyObject wurzel, HaltungRecord record)
        => Alle<ListBoxItem>(wurzel).First(z => ReferenceEquals(z.DataContext, record));

    /// <summary>Der Pfeilknopf in der Kopfzeile dieser Haltung.</summary>
    private static Button Pfeilknopf(DependencyObject wurzel, HaltungRecord record)
        => Alle<Button>(Zeile(wurzel, record))
            .First(b => b.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) is
                PfeilBeschriftungConverter.Aufklappen or PfeilBeschriftungConverter.Zuklappen);

    /// <summary>Der Expander dieses Themas.</summary>
    private static Expander Expander(DependencyObject wurzel, ThemaAnzeige thema)
        => Alle<Expander>(wurzel).Single(x => ReferenceEquals(x.DataContext, thema));

    /// <summary>Loest den Knopf mit diesem vorlesbaren Namen aus.</summary>
    private static void Klick(DependencyObject wurzel, string name)
    {
        var kandidaten = Alle<Button>(wurzel)
            .Where(b => (string?)b.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) == name)
            .ToList();
        // Genau einer: Ein Formularrahmen je sichtbarer Zeile waere der Fehler, den diese
        // Pruefung am 08.09. gefunden hat (ContentTemplate rendert auch bei Content=null).
        Assert.True(kandidaten.Count == 1, $"Knoepfe mit Namen \"{name}\": {kandidaten.Count} statt 1");
        var knopf = kandidaten[0];
        knopf.RaiseEvent(new RoutedEventArgs(
            System.Windows.Controls.Primitives.ButtonBase.ClickEvent, knopf));
    }

    private static HaltungRecord Datensatz(int nummer)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, $"1000{nummer}-1000{nummer + 1}", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.Street, "Gotthardstrasse", FieldSource.Pdf, false);
        // Schacht_oben/Schacht_unten sind Projektfelder, nicht Katalogspalten. Ohne sie
        // haette das Thema "Stammdaten" nur 15 statt 17 Felder.
        record.SetFieldValue("Schacht_oben", $"1000{nummer}", FieldSource.Pdf, false);
        record.SetFieldValue("Schacht_unten", $"1000{nummer + 1}", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.HoldingLengthMeters, "42.3", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.ConditionClass, (nummer % 5).ToString(), FieldSource.Pdf, false);
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

    private static void Layout(UIElement element, double breite = 1400)
    {
        element.Measure(new Size(breite, 800));
        element.Arrange(new Rect(0, 0, breite, 800));
        element.UpdateLayout();
    }
}
