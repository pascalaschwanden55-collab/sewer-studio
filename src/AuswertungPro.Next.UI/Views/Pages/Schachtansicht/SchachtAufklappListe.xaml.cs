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

namespace AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

/// <summary>
/// Nova, Aufklapp-Liste (Task 6): die Schaechte als Uebersichtsliste, in der genau EIN Schacht
/// aufgeklappt ist (Akkordeon) und darunter alle Felder in ihren Themen zeigt.
///
/// Zweitcontrol zu <c>HaltungAufklappListe</c> mit denselben WPF-freien Bausteinen (siehe
/// Doku-Kopf der XAML-Datei): <see cref="HaltungAufklappTastenregel"/> fuer Tastatur und
/// Fokus, <see cref="HaltungThemenGruppierung"/> fuer die Themenbildung. Der Formularaufbau
/// samt Live-Abgleich uebernimmt <see cref="SchaechteAufklappListeController"/> — kein
/// zweiter Schreibweg auf den Datensatz.
/// </summary>
public partial class SchachtAufklappListe : UserControl
{
    /// <summary>Name des Rahmens um das Formular; die Knoepfe "Alle auf/zu" finden ihn darueber.</summary>
    private const string FormularWurzelName = "FormularWurzel";

    public SchachtAufklappListe() => InitializeComponent();

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(SchachtAufklappListe), new PropertyMetadata(null));

    /// <summary>Die Schaechte; die Seite bindet dieselbe Ansicht wie die Tabelle.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(SchachtRecord), typeof(SchachtAufklappListe),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>Der gewaehlte Schacht; die Seite bindet ihn auf <c>Selected</c> des ViewModels.</summary>
    public SchachtRecord? SelectedItem
    {
        get => (SchachtRecord?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty AufgeklapptProperty = DependencyProperty.Register(
        nameof(Aufgeklappt), typeof(SchachtRecord), typeof(SchachtAufklappListe),
        new PropertyMetadata(null, (d, _) => ((SchachtAufklappListe)d).AufgeklapptChanged?.Invoke(d, EventArgs.Empty)));

    /// <summary>
    /// Der aufgeklappte Schacht, <c>null</c> = keiner. Es ist immer hoechstens einer: Eine
    /// Auswahl per Pfeiltaste klappt bewusst NICHT auf.
    /// </summary>
    public SchachtRecord? Aufgeklappt
    {
        get => (SchachtRecord?)GetValue(AufgeklapptProperty);
        private set => SetValue(AufgeklapptProperty, value);
    }

    /// <summary>Wird bei jedem Wechsel von <see cref="Aufgeklappt"/> ausgeloest.</summary>
    public event EventHandler? AufgeklapptChanged;

    /// <summary>
    /// Baut die Themen eines Schachts. Die Seite reicht denselben Builder herein wie fuer die
    /// Eingabefelder-Schublade (<c>SchaechteRecordDetailsBuilder</c>); der Controller ruft ihn.
    /// </summary>
    public Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>>? DetailBuilder { get; set; }

    public static readonly DependencyProperty ThemenProperty = DependencyProperty.Register(
        nameof(Themen), typeof(IReadOnlyList<ThemaAnzeige>), typeof(SchachtAufklappListe),
        new PropertyMetadata(null));

    /// <summary>Die Themen des aufgeklappten Schachts; ohne aufgeklappten Schacht <c>null</c>.</summary>
    public IReadOnlyList<ThemaAnzeige>? Themen
    {
        get => (IReadOnlyList<ThemaAnzeige>?)GetValue(ThemenProperty);
        private set => SetValue(ThemenProperty, value);
    }

    /// <summary>Setzt die Themen; nur der Controller schreibt sie.</summary>
    internal void ZeigeThemen(IReadOnlyList<ThemaAnzeige>? themen) => Themen = themen;

    public static readonly DependencyProperty ProtokollCommandProperty = DependencyProperty.Register(
        nameof(ProtokollCommand), typeof(ICommand), typeof(SchachtAufklappListe), new PropertyMetadata(null));

    /// <summary>Protokoll oeffnen; die Seite setzt ihren vorhandenen Befehl (kein zweiter Weg).</summary>
    public ICommand? ProtokollCommand
    {
        get => (ICommand?)GetValue(ProtokollCommandProperty);
        set => SetValue(ProtokollCommandProperty, value);
    }

    public static readonly DependencyProperty ZeilenMenueProperty = DependencyProperty.Register(
        nameof(ZeilenMenue), typeof(ContextMenu), typeof(SchachtAufklappListe), new PropertyMetadata(null));

    /// <summary>
    /// Das Kontextmenue der Zeile. Die Seite reicht dasselbe Menue herein, das auch an der
    /// Tabelle haengt; es gibt keinen zweiten Befehlsweg.
    /// </summary>
    public ContextMenu? ZeilenMenue
    {
        get => (ContextMenu?)GetValue(ZeilenMenueProperty);
        set => SetValue(ZeilenMenueProperty, value);
    }

    /// <summary>
    /// Klappt <paramref name="record"/> auf und schliesst den bisher offenen Schacht. Die Auswahl
    /// folgt dem Aufklappen — nicht umgekehrt. <c>null</c> klappt nur zu.
    /// </summary>
    public void KlappeAuf(SchachtRecord? record)
    {
        if (record is null)
        {
            KlappeZu();
            return;
        }

        SelectedItem = record;
        Liste.ScrollIntoView(record);
        Aufgeklappt = record;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (ReferenceEquals(Aufgeklappt, record))
                Liste.ScrollIntoView(record);
        }));
    }

    /// <summary>Schliesst den aufgeklappten Schacht; die Auswahl bleibt bestehen.</summary>
    public void KlappeZu() => Aufgeklappt = null;

    /// <summary>
    /// Scrollt die gewaehlte Zeile in Sicht. Die Seite ruft das nach einem Auswahlwechsel von
    /// aussen — die Liste klappt dabei bewusst nichts auf.
    /// </summary>
    public void ScrolleZurAuswahl()
    {
        if (SelectedItem is not { } record)
            return;

        Liste.ScrollIntoView(record);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (ReferenceEquals(SelectedItem, record))
                Liste.ScrollIntoView(record);
        }));
    }

    /// <summary>
    /// Rechtsklick waehlt zuerst die getroffene Zeile aus — wie in der Tabelle. Sonst wirkte
    /// das Menue auf den vorher gewaehlten Schacht.
    /// </summary>
    private void Liste_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject quelle
            && VisualTreeSafe.FindAncestor<ListBoxItem>(quelle)?.DataContext is SchachtRecord record)
        {
            SelectedItem = record;
        }
    }

    /// <summary>Auf- oder zuklappen; derselbe Schacht nochmals klappt ihn zu.</summary>
    public void Schalte(SchachtRecord? record)
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
        Schalte((sender as FrameworkElement)?.DataContext as SchachtRecord);
        e.Handled = true;
    }

    private void Liste_KeyDown(object sender, KeyEventArgs e)
    {
        if (VerarbeiteTaste(e.Key, e.OriginalSource as DependencyObject))
            e.Handled = true;
    }

    /// <summary>
    /// Wendet die geteilte <see cref="HaltungAufklappTastenregel"/> auf eine Taste an und meldet,
    /// ob sie verbraucht wurde. Eigene Methode, damit die Regel samt Wirkung ohne echtes
    /// Tastaturgeraet pruefbar bleibt.
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
                Schalte(zeile?.DataContext as SchachtRecord);
                return true;
            case AufklappTastenAktion.Zuklappen:
                KlappeZu();
                return true;
            case AufklappTastenAktion.FokusAufZeile:
                zeile?.Focus();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Doppelklick auf die Kopfzeile klappt auf oder zu. Ein Doppelklick IM Formular klappt
    /// nicht zu.
    /// </summary>
    private void Liste_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject quelle || IstImFormular(quelle))
            return;

        var zeile = VisualTreeSafe.FindAncestor<ListBoxItem>(quelle);
        if (zeile?.DataContext is not SchachtRecord record)
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

    private static bool IstImFormular(DependencyObject knoten) => FormularWurzel(knoten) is not null;

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
