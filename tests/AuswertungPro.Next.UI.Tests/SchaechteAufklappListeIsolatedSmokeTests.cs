using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Controls;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 6): Das Schacht-Control baut sich mit den echten
/// App-Ressourcen auf. Kopie von <see cref="HaltungAufklappListeIsolatedSmokeTests"/> mit
/// getauschten Typen (<see cref="SchachtAufklappListe"/>, <see cref="SchachtRecord"/>,
/// <see cref="SchaechteAufklappListeController"/>, <see cref="SchaechteRecordDetailsBuilder"/>).
///
/// Der Kern der Pruefung: Bei 20 Schaechten entstehen nur Kopfzeilen und KEIN Formular. Erst der
/// aufgeklappte Schacht bekommt eines (Akkordeon), eine Feldaenderung stempelt Manual/UserEdited,
/// und die Zustandsklasse (Handwert!) wird nur bei echter Auswahl gestempelt.
///
/// Laeuft wie die anderen WPF-Smoke-Tests in einem eigenen Kindprozess; kein Projekt, kein
/// ViewModel, kein Fensterstart.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class SchaechteAufklappListeIsolatedSmokeTests
{
    private const string Strasse = "Strasse";
    private const string Zustandsklasse = "Zustandsklasse";

    private static readonly string ChildTestName =
        typeof(SchaechteAufklappListeIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_zeigt_das_Formular_nur_fuer_den_aufgeklappten_Schacht);

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
    public void Kindprozess_zeigt_das_Formular_nur_fuer_den_aufgeklappten_Schacht()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var datensaetze = new ObservableCollection<SchachtRecord>(Enumerable.Range(1, 20).Select(Datensatz));
            var builder = new SchaechteRecordDetailsBuilder(
                _ => Array.Empty<string>(),
                _ => null,
                (record, feld, wert) =>
                {
                    foreach (var key in feld.AlleKeys)
                        record.SetFieldValue(key, wert ?? "", FieldSource.Manual, userEdited: true);
                });

            var liste = new SchachtAufklappListe { ItemsSource = datensaetze };
            var controller = new SchaechteAufklappListeController(
                liste,
                () => null,
                record => builder.Build(Array.Empty<string>(), record));
            controller.Verdrahte();
            // Kleinere Flaeche als beim uebrigen Test: Bei voller Hoehe (800px) passen alle 20
            // Kopfzeilen ins Bild und die Virtualisierung liesse sich nicht zeigen.
            Layout(liste, hoehe: 240);

            // Zugeklappt: 20 Kopfzeilen, kein einziges Formular.
            Assert.Null(liste.Aufgeklappt);
            Assert.Null(liste.Themen);
            Assert.Empty(Alle<RecordDetailsView>(liste));
            // Virtualisierung: Es entstehen nur die sichtbaren Zeilen, nicht alle 20.
            var zeilenAmAnfang = Alle<ListBoxItem>(liste).Count;
            Assert.InRange(zeilenAmAnfang, 1, datensaetze.Count - 1);

            // Aufklappen: fuenf Themen mit Zaehlern.
            liste.KlappeAuf(datensaetze[0]);
            Layout(liste);

            Assert.Same(datensaetze[0], liste.Aufgeklappt);
            Assert.Same(datensaetze[0], liste.SelectedItem);
            var themen = liste.Themen!;
            Assert.Equal(
                ["Stammdaten", "Zustand und Inspektion", "Sanierung und Kosten", "Dokumente und Medien", "Weitere Angaben"],
                themen.Select(t => t.Title));
            Assert.Equal([7, 6, 3, 3], themen.Take(4).Select(t => t.Anzahl));
            Assert.True(themen[4].Anzahl > 0, "Weitere Angaben darf nicht leer sein");
            Assert.Equal(4, Alle<RecordDetailsView>(liste).Count);
            Assert.Contains(Alle<AuswertungPro.Next.UI.FluentIcon>(liste),
                icon => icon.RenderTransform is RotateTransform { Angle: 90 });

            // Der Pfeil sagt, was der Klick tut — "Schacht", nicht "Haltung".
            Assert.Equal(
                "Schacht zuklappen",
                Pfeilknopf(liste, datensaetze[0]).GetValue(System.Windows.Automation.AutomationProperties.NameProperty));
            Assert.Equal(
                "Schacht aufklappen",
                Pfeilknopf(liste, datensaetze[1]).GetValue(System.Windows.Automation.AutomationProperties.NameProperty));

            // "Alle auf" klappt auch das fuenfte Thema auf.
            Klick(liste, "Alle Themen aufklappen");
            Layout(liste);
            Assert.Equal(5, Alle<RecordDetailsView>(liste).Count);

            // Feldaenderung im Formular geht ueber den bestehenden Rueckschreibweg und stempelt
            // Manual/UserEdited.
            var strasse = themen[0].EinzelGruppe[0].Items.Single(i => i.FieldName == Strasse);
            strasse.Value = "Neue Strasse";
            Assert.Equal("Neue Strasse", datensaetze[0].GetFieldValue(Strasse));
            Assert.Equal(FieldSource.Manual, datensaetze[0].FieldMeta[Strasse].Source);
            Assert.True(datensaetze[0].FieldMeta[Strasse].UserEdited);

            // Eine Aenderung am Datensatz (Tabelle, Dienst) erscheint sofort im Formular.
            datensaetze[0].SetFieldValue(Strasse, "Von aussen", FieldSource.Manual, userEdited: true);
            Assert.Equal("Von aussen", strasse.Value);

            // Zustandsklasse: reiner Handwert. Derselbe Wert (blosses Anklicken/Oeffnen der
            // Auswahl) darf NICHT stempeln — RecordDetailItem.Value schreibt nur bei echter
            // Aenderung. Der Ausgangswert stammt aus dem PDF (Source=Pdf, nicht UserEdited).
            var zustandsklasse = themen[1].EinzelGruppe[0].Items.Single(i => i.FieldName == Zustandsklasse);
            Assert.True(zustandsklasse.IsCombo);
            Assert.Equal("1", zustandsklasse.Value);
            Assert.Equal(FieldSource.Pdf, datensaetze[0].FieldMeta[Zustandsklasse].Source);
            Assert.False(datensaetze[0].FieldMeta[Zustandsklasse].UserEdited);

            zustandsklasse.Value = "1"; // derselbe Wert: keine echte Auswahl
            Assert.Equal(FieldSource.Pdf, datensaetze[0].FieldMeta[Zustandsklasse].Source);
            Assert.False(datensaetze[0].FieldMeta[Zustandsklasse].UserEdited);

            zustandsklasse.Value = "4"; // echte Auswahl
            Assert.Equal("4", datensaetze[0].GetFieldValue(Zustandsklasse));
            Assert.Equal(FieldSource.Manual, datensaetze[0].FieldMeta[Zustandsklasse].Source);
            Assert.True(datensaetze[0].FieldMeta[Zustandsklasse].UserEdited);

            // Akkordeon: Der naechste Schacht ersetzt das Formular; der alte Abgleich ist beendet.
            liste.KlappeAuf(datensaetze[1]);
            Layout(liste);
            Assert.Same(datensaetze[1], liste.Aufgeklappt);
            Assert.Equal(5, liste.Themen!.Count);

            // Pfeiltaste wechselt nur die Auswahl — sie klappt NICHT auf.
            var zeile2 = Zeile(liste, datensaetze[2]);
            Assert.False(liste.VerarbeiteTaste(Key.Down, zeile2));
            liste.SelectedItem = datensaetze[2];
            Layout(liste);
            Assert.Same(datensaetze[1], liste.Aufgeklappt);

            // Enter auf der Zeile klappt auf und wieder zu; Escape auf der Zeile klappt zu.
            Assert.True(liste.VerarbeiteTaste(Key.Enter, zeile2));
            Layout(liste);
            Assert.Same(datensaetze[2], liste.Aufgeklappt);
            Assert.True(liste.VerarbeiteTaste(Key.Escape, Zeile(liste, datensaetze[2])));
            Layout(liste);
            Assert.Null(liste.Aufgeklappt);
            Assert.Null(liste.Themen);
            Assert.Empty(Alle<RecordDetailsView>(liste));

            // Ohne Auswahl: kein Fehltext, keine Ausnahme.
            liste.KlappeAuf(null);
            Layout(liste);
            Assert.Null(liste.Aufgeklappt);

            var leer = new SchachtAufklappListe();
            Layout(leer);
            Assert.Empty(Alle<RecordDetailsView>(leer));

            // Verschwindet der aufgeklappte Schacht (geloescht, Projektwechsel), klappt die
            // Liste wirklich zu — sonst bliebe der Pfeil gedreht.
            liste.KlappeAuf(datensaetze[5]);
            Layout(liste);
            Assert.NotNull(liste.Themen);

            datensaetze.RemoveAt(5);
            controller.AktualisiereFormular();
            Layout(liste);
            Assert.Null(liste.Aufgeklappt);
            Assert.Null(liste.Themen);
            Assert.Empty(Alle<RecordDetailsView>(liste));

            controller.Dispose();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    /// <summary>Die erzeugte Zeile dieses Schachts.</summary>
    private static ListBoxItem Zeile(DependencyObject wurzel, SchachtRecord record)
        => Alle<ListBoxItem>(wurzel).First(z => ReferenceEquals(z.DataContext, record));

    /// <summary>Der Pfeilknopf in der Kopfzeile dieses Schachts.</summary>
    private static Button Pfeilknopf(DependencyObject wurzel, SchachtRecord record)
        => Alle<Button>(Zeile(wurzel, record))
            .First(b => b.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) is
                "Schacht aufklappen" or "Schacht zuklappen");

    /// <summary>Loest den Knopf mit diesem vorlesbaren Namen aus.</summary>
    private static void Klick(DependencyObject wurzel, string name)
    {
        var kandidaten = Alle<Button>(wurzel)
            .Where(b => (string?)b.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) == name)
            .ToList();
        Assert.True(kandidaten.Count == 1, $"Knoepfe mit Namen \"{name}\": {kandidaten.Count} statt 1");
        var knopf = kandidaten[0];
        knopf.RaiseEvent(new RoutedEventArgs(
            System.Windows.Controls.Primitives.ButtonBase.ClickEvent, knopf));
    }

    /// <summary>
    /// Deckt alle fuenf Themen non-trivial ab: Stammdaten (7), Zustand und Inspektion (6),
    /// Sanierung und Kosten (3), Dokumente und Medien (3) und mindestens ein Feld, das in
    /// keine dieser Gruppen passt ("Fotos" -> Weitere Angaben, SchaechteColumnPolicy).
    /// </summary>
    private static SchachtRecord Datensatz(int nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", $"S{nummer}", FieldSource.Pdf, false);
        record.SetFieldValue(Strasse, "Gotthardstrasse", FieldSource.Pdf, false);
        record.SetFieldValue("Funktion", "Kontrollschacht", FieldSource.Pdf, false);
        record.SetFieldValue("Material", "Beton", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "600", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.ShaftDimension2Mm, "600", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.ShaftShape, "Rund", FieldSource.Pdf, false);

        record.SetFieldValue(Zustandsklasse, (nummer % 5).ToString(), FieldSource.Pdf, false);
        record.SetFieldValue("Pruefungsresultat", "dicht", FieldSource.Pdf, false);
        record.SetFieldValue("Referenzpruefung", "", FieldSource.Pdf, false);
        record.SetFieldValue("Gewaesserschutz", "", FieldSource.Pdf, false);
        record.SetFieldValue("Grundwasserspiegel", "", FieldSource.Pdf, false);
        record.SetFieldValue("Dichtheit", "", FieldSource.Pdf, false);

        record.SetFieldValue(FieldKeys.RenovationDecision, "Ja", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.RecommendedRehabilitationMeasures, "Sanierung XY", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.Cost, "1000", FieldSource.Pdf, false);

        record.SetFieldValue(FieldKeys.PdfPath, "", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.PdfEigen, "", FieldSource.Pdf, false);
        record.SetFieldValue(FieldKeys.Link, "", FieldSource.Pdf, false);

        record.SetFieldValue("Fotos", "", FieldSource.Pdf, false);
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
