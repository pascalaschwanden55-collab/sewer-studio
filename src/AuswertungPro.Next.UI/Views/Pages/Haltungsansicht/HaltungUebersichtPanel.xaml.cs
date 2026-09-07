using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>
/// Uebersicht rechts neben der Liste: Rohrring, Eckdaten und Primaere Schaeden der gewaehlten
/// Haltung, nur lesend. Der Doppelklick auf einen Schaden fuehrt auf denselben Weg wie die
/// Haltungsansicht (Beobachtungen); der KI-Hinweis fuehrt in den Player.
/// </summary>
public partial class HaltungUebersichtPanel : UserControl
{
    public HaltungUebersichtPanel() => InitializeComponent();

    public static readonly DependencyProperty RecordProperty = DependencyProperty.Register(
        nameof(Record), typeof(HaltungRecord), typeof(HaltungUebersichtPanel), new PropertyMetadata(null, OnRecordChanged));

    /// <summary>Gewaehlte Haltung; null zeigt eine leere Uebersicht.</summary>
    public HaltungRecord? Record
    {
        get => (HaltungRecord?)GetValue(RecordProperty);
        set => SetValue(RecordProperty, value);
    }

    public static readonly DependencyProperty EntriesProperty = DependencyProperty.Register(
        nameof(Entries), typeof(IEnumerable), typeof(HaltungUebersichtPanel), new PropertyMetadata(null, OnEntriesChanged));

    /// <summary>Primaere Schaeden der gewaehlten Haltung (ProtocolEntry-Liste des ViewModels).</summary>
    public IEnumerable? Entries
    {
        get => (IEnumerable?)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    public static readonly DependencyProperty PruefungTextProperty = DependencyProperty.Register(
        nameof(PruefungText), typeof(string), typeof(HaltungUebersichtPanel), new PropertyMetadata(string.Empty));

    /// <summary>Fachlicher Pruefstatus der gewaehlten Haltung (Inventar 8.1), aus HaltungPruefstatus.</summary>
    public string PruefungText
    {
        get => (string)GetValue(PruefungTextProperty);
        private set => SetValue(PruefungTextProperty, value);
    }

    public static readonly DependencyProperty VideoTextProperty = DependencyProperty.Register(
        nameof(VideoText), typeof(string), typeof(HaltungUebersichtPanel), new PropertyMetadata("kein Video"));

    /// <summary>Dateiname des verknuepften Videos, sonst "kein Video".</summary>
    public string VideoText
    {
        get => (string)GetValue(VideoTextProperty);
        private set => SetValue(VideoTextProperty, value);
    }

    public static readonly DependencyProperty OffeneKiBefundeProperty = DependencyProperty.Register(
        nameof(OffeneKiBefunde), typeof(int), typeof(HaltungUebersichtPanel), new PropertyMetadata(0));

    /// <summary>Anzahl der noch nicht bestaetigten KI-Vorschlaege in den aktuellen Schaeden.</summary>
    public int OffeneKiBefunde
    {
        get => (int)GetValue(OffeneKiBefundeProperty);
        private set => SetValue(OffeneKiBefundeProperty, value);
    }

    /// <summary>Doppelklick auf einen Schaden: dieselbe Aktion wie in der Haltungsansicht (Beobachtungen).</summary>
    public Action<HaltungRecord>? BeobachtungenRequested { get; set; }

    /// <summary>Knopf "Im Player pruefen" im KI-Hinweis: oeffnet das Video dieser Haltung.</summary>
    public Action<HaltungRecord>? PlayerRequested { get; set; }

    private void Schaden_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Record is { } r)
            BeobachtungenRequested?.Invoke(r);
    }

    private void ImPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (Record is { } r)
            PlayerRequested?.Invoke(r);
    }

    private static void OnRecordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (HaltungUebersichtPanel)d;
        if (e.NewValue is HaltungRecord record)
        {
            panel.PruefungText = HaltungPruefstatus.Text(HaltungPruefstatus.Bestimme(record));
            var link = record.GetFieldValue(FieldKeys.Link);
            panel.VideoText = string.IsNullOrWhiteSpace(link) ? "kein Video" : Path.GetFileName(link);
        }
        else
        {
            panel.PruefungText = string.Empty;
            panel.VideoText = "kein Video";
        }
    }

    private static void OnEntriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (HaltungUebersichtPanel)d;
        var entries = (e.NewValue as IEnumerable)?.OfType<ProtocolEntry>() ?? Enumerable.Empty<ProtocolEntry>();
        panel.OffeneKiBefunde = entries.Count(x => x.Ai is { Accepted: false });
    }
}
