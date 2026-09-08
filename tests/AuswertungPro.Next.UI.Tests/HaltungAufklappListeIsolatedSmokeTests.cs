using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Controls;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 1): Das Control baut sich mit den echten App-Ressourcen auf.
///
/// Der Kern der Pruefung: Bei 40 Haltungen entstehen nur Kopfzeilen und KEIN Formular. Erst die
/// aufgeklappte Haltung bekommt eines, immer nur eine (Akkordeon), und jede Feldaenderung laeuft
/// ueber den bestehenden Rueckschreibweg der <see cref="DataPageDetailItemFactory"/>.
///
/// Laeuft wie die anderen WPF-Smoke-Tests in einem eigenen Kindprozess; kein Projekt, kein
/// ViewModel, kein Fensterstart.
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
            Assert.NotEmpty(Alle<System.Windows.Controls.ListBoxItem>(liste));

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

            // Akkordeon: Die naechste Haltung ersetzt das Formular; der alte Abgleich ist beendet.
            liste.KlappeAuf(datensaetze[1]);
            Layout(liste);
            Assert.Same(datensaetze[1], liste.Aufgeklappt);
            Assert.Equal(4, Alle<RecordDetailsView>(liste).Count);
            datensaetze[0].SetFieldValue(Bemerkungen, "Nach dem Wechsel", FieldSource.Manual, userEdited: true);
            Assert.Equal("Von aussen", bemerkungen.Value);

            // Zuklappen: kein Formular mehr, kein Abgleich mehr.
            liste.KlappeZu();
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

    /// <summary>Loest den Knopf mit diesem vorlesbaren Namen aus.</summary>
    private static void Klick(DependencyObject wurzel, string name)
    {
        var kandidaten = Alle<System.Windows.Controls.Button>(wurzel)
            .Where(b => (string?)b.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) == name)
            .ToList();
        // Genau einer: Ein Formularrahmen je sichtbarer Zeile waere der Fehler, den diese
        // Pruefung am 08.09. gefunden hat (ContentTemplate rendert auch bei Content=null).
        Assert.True(kandidaten.Count == 1, $"Knoepfe mit Namen \"{name}\": {kandidaten.Count} statt 1");
        var knopf = kandidaten[0];
        knopf.RaiseEvent(new System.Windows.RoutedEventArgs(
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

    private static void Layout(UIElement element)
    {
        element.Measure(new Size(1400, 800));
        element.Arrange(new Rect(0, 0, 1400, 800));
        element.UpdateLayout();
    }
}
