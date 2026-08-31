using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Controls;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Menuepunkt "Beim Kanton nachschlagen" muss auch wirklich am Feld
/// haengen. <see cref="RecordDetailItem.KannNachschlagen"/> allein beweist das
/// nicht: Es sagt nur, dass der Punkt sichtbar WAERE — ob das Feld ueberhaupt
/// ein Kontextmenue traegt, entscheidet die Vorlage.
///
/// Genau daran ist es bei den Haltungen gescheitert. Ein Auswahlfeld ohne
/// verwaltete Optionsliste (FunktionHierarchisch, Nutzungsart) bekam
/// <c>ContextMenu = null</c>, weil die Regel nur nach der Optionsverwaltung
/// fragte. Der Rechtsklick zeigte dort gar nichts — waehrend er an den
/// Schaechten funktionierte, wo dieselben Felder verwaltete Dropdowns sind.
///
/// Laeuft im isolierten WPF-Kindprozess wie der Bindungsrauchtest: Die
/// Vorlagen greifen auf das Theme zu, und ein Application-Objekt ist
/// prozessweit und einmalig.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class NachschlagKontextmenueTests
{
    private static readonly string ChildTestName =
        typeof(NachschlagKontextmenueTests).FullName
        + "."
        + nameof(Kindprozess_prueft_die_Kontextmenues);

    private sealed class NichtsTuerBefehl : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }

    [Fact]
    public async Task Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern()
    {
        Assert.Null(System.Windows.Application.Current);

        var ergebnis = await WpfIsolatedTestProcess.RunAsync(
            ChildTestName,
            TimeSpan.FromSeconds(120));

        Assert.False(ergebnis.TimedOut, ergebnis.DescribeFailure());
        Assert.True(ergebnis.ExitCode == 0, ergebnis.DescribeFailure());
        Assert.True(ergebnis.ChildScenarioCompleted, ergebnis.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_prueft_die_Kontextmenues()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var befunde = new List<string>();

            // 1. Der Fall, an dem es gescheitert ist: Auswahlliste aus dem
            //    Feldkatalog, aber ohne Bearbeiten/Zuruecksetzen.
            Pruefe(befunde, "Auswahlfeld ohne verwaltete Optionen",
                ComboMenue(Auswahlfeld("FunktionHierarchisch", verwaltet: false)),
                erwarteNachschlag: true);

            // 2. Ein verwaltetes Auswahlfeld behaelt beides.
            var verwaltet = ComboMenue(Auswahlfeld("Eigentuemer", verwaltet: true));
            Pruefe(befunde, "verwaltetes Auswahlfeld", verwaltet, erwarteNachschlag: true);
            if (verwaltet is null
                || !Punkte(verwaltet).Any(t => t.Contains("bearbeiten", StringComparison.OrdinalIgnoreCase)))
            {
                befunde.Add("verwaltetes Auswahlfeld: Die Optionsverwaltung fehlt im Menue.");
            }

            // 3. Ohne Quelle und ohne Optionen darf gar kein Menue erscheinen -
            //    sonst poppt ein leeres Kaestchen auf.
            if (ComboMenue(Auswahlfeld("Bemerkungen", verwaltet: false)) is not null)
                befunde.Add("Auswahlfeld ohne Quelle: traegt ein Menue, obwohl es nichts zu zeigen gibt.");

            // 4. Ein gefuelltes Textfeld behaelt Ausschneiden/Kopieren/Einfuegen.
            if (TextMenue(Textfeld("Bemerkungen", "steht schon was drin")) is not null)
            {
                befunde.Add(
                    "gefuelltes Textfeld: eigenes Menue statt des Windows-Standardmenues - "
                    + "das nimmt dem Feld Ausschneiden/Kopieren/Einfuegen.");
            }

            // 5. Die Strassenuebernahme haengt am leeren Strassenfeld - auch
            //    an einem Auswahlfeld ohne Optionsverwaltung.
            var strasse = ComboMenue(Strassenfeld(""));
            if (strasse is null
                || !Punkte(strasse).Any(t => t.Contains("übernehmen", StringComparison.OrdinalIgnoreCase)))
            {
                befunde.Add("leeres Strassenfeld: die Uebernahme vom Nachbarbauteil fehlt im Menue.");
            }

            // 5b. Dasselbe im Menue der verwalteten Auswahlfelder. Beide
            //     Menues fuehren den Punkt getrennt - eine Sabotage an dem
            //     einen laesst den anderen unberuehrt, und dann prueft nur
            //     die Haelfte.
            var strasseVerwaltet = ComboMenue(Strassenfeld("", verwaltet: true));
            if (strasseVerwaltet is null
                || !Punkte(strasseVerwaltet).Any(t => t.Contains("übernehmen", StringComparison.OrdinalIgnoreCase)))
            {
                befunde.Add("verwaltetes Strassenfeld: die Uebernahme vom Nachbarbauteil fehlt im Menue.");
            }

            // 6. Ein gefuelltes Strassenfeld traegt gar kein eigenes Menue.
            if (ComboMenue(Strassenfeld("Linden")) is not null)
                befunde.Add("gefuelltes Strassenfeld: traegt ein Menue, obwohl nichts zu uebernehmen ist.");

            // 7. Leeres Textfeld mit Quelle: Nachschlag vorhanden.
            Pruefe(befunde, "leeres Textfeld mit Quelle",
                TextMenue(Textfeld("Haltungslaenge_m", "")),
                erwarteNachschlag: true);

            // 7b. Ein geleertes Feld muss den Rechtsklick sofort wieder
            //     anbieten - ohne die Seite neu aufzubauen. Die Vorlage
            //     entscheidet ueber einen Ausloeser; der greift nur, wenn das
            //     Feld die Aenderung auch meldet.
            var geleert = Auswahlfeld("FunktionHierarchisch", verwaltet: false, wert: "Hauptleitung");
            var box = ComboBoxFuer(geleert);
            if (box.ContextMenu is not null)
                befunde.Add("gefuelltes Feld: traegt schon vor dem Leeren ein Menue.");

            geleert.Value = "";
            Warte();

            if (box.ContextMenu is null)
            {
                befunde.Add(
                    "geleertes Feld: kein Kontextmenue - nach dem Loeschen muss der "
                    + "Nachschlag sofort wieder erscheinen.");
            }
            else if (!Punkte(box.ContextMenu).Any(t => t.Contains("nachschlagen", StringComparison.OrdinalIgnoreCase)))
            {
                befunde.Add("geleertes Feld: Menue ohne Nachschlag-Punkt.");
            }

            // 7c. Ein nachgeschlagener Eigentuemer steht nicht in der festen
            //     Liste ("Abwasser Uri" gegen die Kurzform "AWU"). Er muss
            //     trotzdem sichtbar bleiben und die erste Bedienung darf ihn
            //     nicht durch den ersten Listeneintrag ersetzen.
            befunde.AddRange(PruefeFremdenEigentuemer());

            // 8. Das Stapelfenster muss sich ueberhaupt laden lassen. Ein
            //    erfundener Ressourcenname faellt in WPF sonst still aus -
            //    genau so entstand das unlesbare Vorschlagsfenster.
            try
            {
                var stapel = new StrassenUebernahmeWindow(
                    "Probe",
                    "Schacht",
                    [new StrassenUebernahmeZeile("36262", "Linden", "Haltung 36262-36275")],
                    ["36268"]);
                stapel.Measure(new Size(700, 600));

                if (stapel.Gewaehlt.Count != 0)
                    befunde.Add("Stapelfenster: liefert eine Auswahl, bevor bestaetigt wurde.");
            }
            catch (Exception ex)
            {
                befunde.Add($"Stapelfenster laesst sich nicht laden: {ex.GetType().Name}: {ex.Message}");
            }

            Assert.True(befunde.Count == 0,
                $"{befunde.Count} Befunde am Kontextmenue:{Environment.NewLine}  "
                + string.Join(Environment.NewLine + "  ", befunde));

            app.Shutdown();
        });

        WpfIsolatedTestProcess.MarkChildScenarioCompleted();
    }

    private static void Pruefe(
        List<string> befunde, string fall, ContextMenu? menue, bool erwarteNachschlag)
    {
        if (menue is null)
        {
            befunde.Add($"{fall}: kein Kontextmenue am Feld - der Rechtsklick zeigt nichts.");
            return;
        }

        var hat = Punkte(menue).Any(t => t.Contains("nachschlagen", StringComparison.OrdinalIgnoreCase));
        if (hat != erwarteNachschlag)
            befunde.Add($"{fall}: Nachschlag-Punkt vorhanden={hat}, erwartet={erwarteNachschlag}.");
    }

    /// <summary>
    /// Bewusst ohne Sichtbarkeitsfilter: Ein Kontextmenue erhaelt seinen
    /// DataContext erst vom PlacementTarget, also beim Oeffnen. Vorher stehen
    /// alle Ausloeser auf Anfang, und Visibility saehe hier zufaellig aus.
    /// Ob ein Punkt sichtbar wird, entscheidet KannNachschlagen — dafuer gibt
    /// es die FeldNachschlagMenueTests. Hier geht es darum, ob er ueberhaupt
    /// am Feld haengt.
    /// </summary>
    private static string[] Punkte(ContextMenu menue)
        => menue.Items.OfType<MenuItem>()
            .Select(m => m.Header?.ToString() ?? "")
            .ToArray();

    private static RecordDetailItem Auswahlfeld(string feld, bool verwaltet, string wert = "")
        => new(
            label: feld,
            value: wert,
            commitValue: _ => { },
            isCombo: true,
            allowFreeText: true,
            options: new[] { "a", "b" },
            editOptionsCommand: verwaltet ? new NichtsTuerBefehl() : null,
            nachschlagenCommand: new NichtsTuerBefehl())
        {
            FieldName = feld,
            BauteilArt = BauteilArt.Haltung
        };

    /// <summary>
    /// Ein Strassenfeld ohne Kantonsquelle: bei den Haltungen genau so.
    /// Nur die Uebernahme vom Nachbarbauteil hat dort etwas zu bieten.
    /// </summary>
    private static RecordDetailItem Strassenfeld(string wert, bool verwaltet = false)
        => new(
            label: "Strasse",
            value: wert,
            commitValue: _ => { },
            isCombo: true,
            allowFreeText: true,
            options: new[] { "a", "b" },
            editOptionsCommand: verwaltet ? new NichtsTuerBefehl() : null,
            strasseUebernehmenCommand: new NichtsTuerBefehl())
        {
            FieldName = "Strasse",
            BauteilArt = BauteilArt.Haltung
        };

    private static RecordDetailItem Textfeld(string feld, string wert)
        => new(
            label: feld,
            value: wert,
            commitValue: _ => { },
            nachschlagenCommand: new NichtsTuerBefehl())
        {
            FieldName = feld,
            BauteilArt = BauteilArt.Haltung
        };

    /// <summary>
    /// Baut das Eigentuemerfeld so, wie die Seiten es bauen, setzt einen Wert
    /// ausserhalb der Liste und laesst WPF die Bindung zurueckschreiben —
    /// genau das passiert beim ersten Klick ins Feld.
    /// </summary>
    private static IEnumerable<string> PruefeFremdenEigentuemer()
    {
        if (!GridDropdownFieldPolicy.TryResolve("Eigentuemer", out var spec))
        {
            yield return "Eigentuemer: keine Dropdown-Regel gefunden.";
            yield break;
        }

        var item = new RecordDetailItem(
            label: "Eigentuemer",
            value: "Abwasser Uri",
            commitValue: _ => { },
            isCombo: true,
            allowFreeText: spec.AllowFreeText,
            options: new[] { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" })
        { FieldName = "Eigentuemer", BauteilArt = BauteilArt.Haltung };

        var quelle = new RecordDetailsView();
        var vorlage = spec.AllowFreeText ? "EditableComboEditorTemplate" : "FixedComboEditorTemplate";
        var halter = new ContentControl
        {
            ContentTemplate = (DataTemplate)quelle.Resources[vorlage],
            Content = item
        };
        Zeige(halter, quelle);

        var combo = SucheCombo(halter);
        if (combo is null)
        {
            yield return "Eigentuemer: kein Auswahlfeld in der Vorlage.";
            yield break;
        }

        if (combo.Text != "Abwasser Uri" && combo.SelectedItem?.ToString() != "Abwasser Uri")
        {
            yield return
                "Eigentuemer: Der nachgeschlagene Wert ist im Feld nicht sichtbar "
                + $"(Text='{combo.Text}', Auswahl='{combo.SelectedItem}').";
        }

        // Das machen SelectionChanged und LostKeyboardFocus der Detailansicht.
        var eigenschaft = item.AllowFreeText
            ? ComboBox.TextProperty
            : System.Windows.Controls.Primitives.Selector.SelectedItemProperty;
        combo.GetBindingExpression(eigenschaft)?.UpdateSource();

        if (item.Value != "Abwasser Uri")
        {
            yield return
                $"Eigentuemer: Nach der ersten Bedienung steht '{item.Value}' im Feld "
                + "statt des nachgeschlagenen Werts.";
        }
    }

    private static ContextMenu? ComboMenue(RecordDetailItem item) => ComboBoxFuer(item).ContextMenu;

    private static ComboBox ComboBoxFuer(RecordDetailItem item)
    {
        var quelle = new RecordDetailsView();
        var box = new ComboBox
        {
            Style = (Style)quelle.Resources["DetailComboStyle"],
            DataContext = item
        };
        Zeige(box, quelle);
        return box;
    }

    private static ContextMenu? TextMenue(RecordDetailItem item)
    {
        var quelle = new RecordDetailsView();
        var halter = new ContentControl
        {
            ContentTemplate = (DataTemplate)quelle.Resources["TextEditorTemplate"],
            Content = item
        };
        Zeige(halter, quelle);

        var box = SucheTextBox(halter);
        Assert.NotNull(box);
        return box!.ContextMenu;
    }

    /// <summary>
    /// Das Feld braucht die Ressourcen der Detailansicht und einen echten
    /// Layoutlauf - erst dann werten die Ausloeser der Vorlage aus.
    /// </summary>
    /// <summary>Laesst WPF die Ausloeser der Vorlage neu bewerten.</summary>
    private static void Warte()
    {
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            () => { }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private static void Zeige(FrameworkElement inhalt, RecordDetailsView quelle)
    {
        var wirt = new Grid();
        foreach (var schluessel in quelle.Resources.Keys)
            wirt.Resources[schluessel] = quelle.Resources[schluessel];
        wirt.Children.Add(inhalt);

        wirt.Measure(new Size(400, 200));
        wirt.Arrange(new Rect(0, 0, 400, 200));
        wirt.UpdateLayout();
    }

    private static ComboBox? SucheCombo(DependencyObject wurzel)
    {
        if (wurzel is ComboBox gefunden)
            return gefunden;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(wurzel); i++)
        {
            var treffer = SucheCombo(VisualTreeHelper.GetChild(wurzel, i));
            if (treffer is not null)
                return treffer;
        }

        return null;
    }

    private static TextBox? SucheTextBox(DependencyObject wurzel)
    {
        if (wurzel is TextBox gefunden)
            return gefunden;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(wurzel); i++)
        {
            var treffer = SucheTextBox(VisualTreeHelper.GetChild(wurzel, i));
            if (treffer is not null)
                return treffer;
        }

        return null;
    }
}
