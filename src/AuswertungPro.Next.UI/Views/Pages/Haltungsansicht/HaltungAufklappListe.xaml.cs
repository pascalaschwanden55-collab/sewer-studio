using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>
/// Nova, Aufklapp-Liste: die Haltungen als Uebersichtsliste, in der genau EINE Haltung
/// aufgeklappt ist (Akkordeon) und darunter alle Felder in ihren Themen zeigt.
///
/// Das Control stellt nur dar. Die Felder rendert der unveraenderte <c>RecordDetailsView</c>,
/// die Themen kommen aus <see cref="HaltungThemenGruppierung"/>, und den Formularaufbau samt
/// Live-Abgleich uebernimmt der <c>DataPageAufklappListeController</c>. Es gibt hier keinen
/// zweiten Schreibweg auf den Datensatz.
///
/// Wichtig fuer die Virtualisierung: Das Formular haengt an einem <c>ContentControl</c>, dessen
/// Inhalt nur fuer die aufgeklappte Haltung gesetzt wird. Bei 300 Haltungen entstehen deshalb
/// nur Kopfzeilen — nicht 300 Formulare.
/// </summary>
public partial class HaltungAufklappListe : UserControl
{
    /// <summary>Name des Rahmens um das Formular; die Knoepfe "Alle auf/zu" finden ihn darueber.</summary>
    private const string FormularWurzelName = "FormularWurzel";

    public HaltungAufklappListe() => InitializeComponent();

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(HaltungAufklappListe), new PropertyMetadata(null));

    /// <summary>Die Haltungen; die Seite bindet dieselbe Ansicht wie die Tabelle.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(HaltungRecord), typeof(HaltungAufklappListe),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>Die gewaehlte Haltung; die Seite bindet sie auf <c>Selected</c> des ViewModels.</summary>
    public HaltungRecord? SelectedItem
    {
        get => (HaltungRecord?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty AufgeklapptProperty = DependencyProperty.Register(
        nameof(Aufgeklappt), typeof(HaltungRecord), typeof(HaltungAufklappListe),
        new PropertyMetadata(null, (d, _) => ((HaltungAufklappListe)d).AufgeklapptChanged?.Invoke(d, EventArgs.Empty)));

    /// <summary>
    /// Die aufgeklappte Haltung, <c>null</c> = keine. Es ist immer hoechstens eine: Eine
    /// Auswahl per Pfeiltaste klappt bewusst NICHT auf, sonst baute jeder Tastendruck ein
    /// vollstaendiges Formular auf.
    /// </summary>
    public HaltungRecord? Aufgeklappt
    {
        get => (HaltungRecord?)GetValue(AufgeklapptProperty);
        private set => SetValue(AufgeklapptProperty, value);
    }

    /// <summary>Wird bei jedem Wechsel von <see cref="Aufgeklappt"/> ausgeloest.</summary>
    public event EventHandler? AufgeklapptChanged;

    /// <summary>
    /// Baut die Themen einer Haltung. Die Seite reicht denselben Builder herein wie fuer die
    /// Eingabefelder-Schublade; der Controller ruft ihn.
    /// </summary>
    public Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>>? DetailBuilder { get; set; }

    public static readonly DependencyProperty ThemenProperty = DependencyProperty.Register(
        nameof(Themen), typeof(IReadOnlyList<ThemaAnzeige>), typeof(HaltungAufklappListe),
        new PropertyMetadata(null));

    /// <summary>Die Themen der aufgeklappten Haltung; ohne aufgeklappte Haltung <c>null</c>.</summary>
    public IReadOnlyList<ThemaAnzeige>? Themen
    {
        get => (IReadOnlyList<ThemaAnzeige>?)GetValue(ThemenProperty);
        private set => SetValue(ThemenProperty, value);
    }

    /// <summary>Setzt die Themen; nur der Controller schreibt sie.</summary>
    internal void ZeigeThemen(IReadOnlyList<ThemaAnzeige>? themen) => Themen = themen;

    public static readonly DependencyProperty HinweisProperty = DependencyProperty.Register(
        nameof(Hinweis), typeof(string), typeof(HaltungAufklappListe), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Sichtbarer Hinweis in der Kopfzeile des aufgeklappten Eintrags, zum Beispiel eine
    /// verworfene Eingabe nach einer neueren Korrektur am Datensatz (W01). Leer = kein Hinweis.
    /// </summary>
    public string Hinweis
    {
        get => (string)GetValue(HinweisProperty);
        set => SetValue(HinweisProperty, value);
    }

    public static readonly DependencyProperty VideoCommandProperty = DependencyProperty.Register(
        nameof(VideoCommand), typeof(ICommand), typeof(HaltungAufklappListe), new PropertyMetadata(null));

    /// <summary>Video abspielen; die Seite setzt ihren vorhandenen Befehl (kein zweiter Weg).</summary>
    public ICommand? VideoCommand
    {
        get => (ICommand?)GetValue(VideoCommandProperty);
        set => SetValue(VideoCommandProperty, value);
    }

    public static readonly DependencyProperty ProtokollCommandProperty = DependencyProperty.Register(
        nameof(ProtokollCommand), typeof(ICommand), typeof(HaltungAufklappListe), new PropertyMetadata(null));

    /// <summary>Originalprotokoll oeffnen; die Seite setzt ihren vorhandenen Befehl.</summary>
    public ICommand? ProtokollCommand
    {
        get => (ICommand?)GetValue(ProtokollCommandProperty);
        set => SetValue(ProtokollCommandProperty, value);
    }

    /// <summary>
    /// Klappt <paramref name="record"/> auf und schliesst die bisher offene Haltung. Die Auswahl
    /// folgt dem Aufklappen — nicht umgekehrt. <c>null</c> klappt nur zu.
    /// </summary>
    public void KlappeAuf(HaltungRecord? record)
    {
        if (record is null)
        {
            KlappeZu();
            return;
        }

        SelectedItem = record;
        Liste.ScrollIntoView(record);
        Aufgeklappt = record;
        // Zweiter Lauf, nachdem das Formular gebaut und gemessen ist: Die Zeile ist jetzt um ein
        // Vielfaches hoeher, und ohne das rutscht ihr Kopf beim Aufklappen aus dem Bild.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (ReferenceEquals(Aufgeklappt, record))
                Liste.ScrollIntoView(record);
        }));
    }

    /// <summary>Schliesst die aufgeklappte Haltung; die Auswahl bleibt bestehen.</summary>
    public void KlappeZu() => Aufgeklappt = null;

    /// <summary>
    /// Scrollt die gewaehlte Zeile in Sicht. Die Seite ruft das nach einem Auswahlwechsel von
    /// aussen (Pfeiltasten, Suche, Sprung aus dem Dossier) — die Liste klappt dabei bewusst
    /// nichts auf.
    /// </summary>
    public void ScrolleZurAuswahl()
    {
        if (SelectedItem is { } record)
            Liste.ScrollIntoView(record);
    }

    public static readonly DependencyProperty ZeilenMenueProperty = DependencyProperty.Register(
        nameof(ZeilenMenue), typeof(ContextMenu), typeof(HaltungAufklappListe), new PropertyMetadata(null));

    /// <summary>
    /// Das Kontextmenue der Zeile. Die Seite reicht dasselbe Menue herein, das auch an der
    /// Tabelle haengt (Video, Protokoll, Beobachtungen, Position verschieben); es gibt keinen
    /// zweiten Befehlsweg. Spaltenbezogene Punkte hat dieses Menue nicht.
    /// </summary>
    public ContextMenu? ZeilenMenue
    {
        get => (ContextMenu?)GetValue(ZeilenMenueProperty);
        set => SetValue(ZeilenMenueProperty, value);
    }

    /// <summary>
    /// Rechtsklick waehlt zuerst die getroffene Zeile aus — wie in der Tabelle. Sonst wirkte
    /// das Menue auf die vorher gewaehlte Haltung.
    /// </summary>
    private void Liste_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject quelle
            && VisualTreeSafe.FindAncestor<ListBoxItem>(quelle)?.DataContext is HaltungRecord record)
        {
            SelectedItem = record;
        }
    }

    /// <summary>Auf- oder zuklappen; dieselbe Haltung nochmals klappt sie zu.</summary>
    public void Schalte(HaltungRecord? record)
    {
        if (record is null)
            return;
        if (ReferenceEquals(Aufgeklappt, record))
            KlappeZu();
        else
            KlappeAuf(record);
    }

    private void Pfeil_Click(object sender, RoutedEventArgs e)
    {
        Schalte((sender as FrameworkElement)?.DataContext as HaltungRecord);
        e.Handled = true;
    }

    private void Liste_KeyDown(object sender, KeyEventArgs e)
    {
        if (VerarbeiteTaste(e.Key, e.OriginalSource as DependencyObject))
            e.Handled = true;
    }

    /// <summary>
    /// Wendet die <see cref="HaltungAufklappTastenregel"/> auf eine Taste an und meldet, ob sie
    /// verbraucht wurde. Eigene Methode, damit die Regel samt Wirkung ohne echtes Tastaturgeraet
    /// pruefbar bleibt.
    /// </summary>
    internal bool VerarbeiteTaste(Key taste, DependencyObject? quelle)
    {
        var zeile = quelle is null ? null : VisualTreeSafe.FindAncestor<ListBoxItem>(quelle);
        var aktion = HaltungAufklappTastenregel.Bestimme(
            taste,
            inEinerZeile: zeile is not null,
            aufDerZeile: ReferenceEquals(quelle, zeile));

        switch (aktion)
        {
            case AufklappTastenAktion.Schalten:
                Schalte(zeile?.DataContext as HaltungRecord);
                return true;
            case AufklappTastenAktion.Zuklappen:
                KlappeZu();
                return true;
            case AufklappTastenAktion.FokusAufZeile:
                // Der Fokuswechsel loest den normalen LostFocus-Rueckschreibweg des Feldes aus;
                // die Eingabe geht dabei nicht verloren.
                zeile?.Focus();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Doppelklick auf die Kopfzeile klappt auf oder zu. Ein Doppelklick IM Formular (etwa zum
    /// Markieren eines Wortes) darf die Haltung nicht zuklappen.
    /// </summary>
    private void Liste_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject quelle || IstImFormular(quelle))
            return;

        var zeile = VisualTreeSafe.FindAncestor<ListBoxItem>(quelle);
        if (zeile?.DataContext is not HaltungRecord record)
            return;

        Schalte(record);
        e.Handled = true;
    }

    private void AlleAuf_Click(object sender, RoutedEventArgs e) => SetzeAlleThemen(sender, true);

    private void AlleZu_Click(object sender, RoutedEventArgs e) => SetzeAlleThemen(sender, false);

    private static void SetzeAlleThemen(object sender, bool offen)
    {
        if (sender is not DependencyObject knopf || FormularWurzel(knopf) is not { } wurzel)
            return;

        foreach (var expander in Nachfahren<Expander>(wurzel))
            expander.IsExpanded = offen;
    }

    /// <summary>Liegt das Element im aufgeklappten Formular (und nicht in der Kopfzeile)?</summary>
    private static bool IstImFormular(DependencyObject knoten) => FormularWurzel(knoten) is not null;

    /// <summary>
    /// Der Rahmen des Formulars. Er liegt in einer Vorlage, deshalb wird er ueber seinen Namen
    /// im Baum gesucht statt ueber <c>FindName</c> des Controls.
    /// </summary>
    private static FrameworkElement? FormularWurzel(DependencyObject? knoten)
    {
        for (var aktuell = knoten; aktuell is not null; aktuell = VisualTreeSafe.GetParentSafe(aktuell))
        {
            if (aktuell is FrameworkElement element && element.Name == FormularWurzelName)
                return element;
        }

        return null;
    }

    private static IEnumerable<T> Nachfahren<T>(DependencyObject knoten) where T : DependencyObject
    {
        var anzahl = VisualTreeHelper.GetChildrenCount(knoten);
        for (var i = 0; i < anzahl; i++)
        {
            var kind = VisualTreeHelper.GetChild(knoten, i);
            if (kind is T passend)
                yield return passend;

            foreach (var tiefer in Nachfahren<T>(kind))
                yield return tiefer;
        }
    }
}
