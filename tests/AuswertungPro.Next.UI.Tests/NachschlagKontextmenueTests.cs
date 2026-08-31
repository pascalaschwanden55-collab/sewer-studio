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

            // 5. Leeres Textfeld mit Quelle: Nachschlag vorhanden.
            Pruefe(befunde, "leeres Textfeld mit Quelle",
                TextMenue(Textfeld("Haltungslaenge_m", "")),
                erwarteNachschlag: true);

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

    private static RecordDetailItem Auswahlfeld(string feld, bool verwaltet)
        => new(
            label: feld,
            value: "",
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

    private static ContextMenu? ComboMenue(RecordDetailItem item)
    {
        var quelle = new RecordDetailsView();
        var box = new ComboBox
        {
            Style = (Style)quelle.Resources["DetailComboStyle"],
            DataContext = item
        };
        Zeige(box, quelle);
        return box.ContextMenu;
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
