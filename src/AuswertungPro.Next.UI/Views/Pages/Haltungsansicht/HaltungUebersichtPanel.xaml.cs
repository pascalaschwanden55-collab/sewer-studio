using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>
/// Uebersicht rechts neben der Liste: Rohrring, Eckdaten und Primaere Schaeden der gewaehlten
/// Haltung, nur lesend. Der Doppelklick auf einen Schaden fuehrt auf denselben Weg wie die
/// Haltungsansicht (Beobachtungen); der KI-Hinweis fuehrt in den Player.
///
/// Fix-Runde 1: Die gebundene Entries-Sammlung ist beim Haltungswechsel dieselbe Instanz (nur
/// geleert und neu gefuellt, ohne Property-Wechsel). Deshalb wird zusaetzlich auf
/// <see cref="INotifyCollectionChanged"/> gehoert; die alte Sammlung wird beim Wechsel und beim
/// Entladen des Controls wieder abgemeldet.
///
/// Nova-Fixwelle F1: Ring und Liste zeigen nur Schaeden (BA*/BB* nach
/// <see cref="RohrringGeometrie.Schaeden"/>), nicht die Bestandsaufnahme BCD/BCE/BCA.
/// Nova-Fixwelle F5: <see cref="PruefungText"/> und <see cref="VideoText"/> haengen an Feldern des
/// Datensatzes. Wird derselbe Datensatz veraendert (Video verknuepft, Protokoll gesetzt), wechselt
/// die Record-Eigenschaft nicht — deshalb wird zusaetzlich auf
/// <see cref="HaltungRecord.PropertyChanged"/> gehoert, mit derselben An-/Abmeldung wie bei den
/// Entries.
/// </summary>
public partial class HaltungUebersichtPanel : UserControl
{
    private INotifyCollectionChanged? _abonnierteEntries;
    private HaltungRecord? _abonnierterRecord;

    public HaltungUebersichtPanel()
    {
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            AbmeldenVonEntries();
            AbmeldenVonRecord();
        };
    }

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

    /// <summary>Alle Protokolleintraege der gewaehlten Haltung (ProtocolEntry-Liste des ViewModels).</summary>
    public IEnumerable? Entries
    {
        get => (IEnumerable?)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    private static readonly DependencyPropertyKey SchaedenPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Schaeden), typeof(IReadOnlyList<ProtocolEntry>), typeof(HaltungUebersichtPanel),
        new PropertyMetadata(Array.Empty<ProtocolEntry>()));

    public static readonly DependencyProperty SchaedenProperty = SchaedenPropertyKey.DependencyProperty;

    /// <summary>
    /// Nur die Schaeden aus <see cref="Entries"/>, schwerste zuerst. Quelle fuer die Liste
    /// "Primaere Schaeden" und fuer den Rohrring.
    /// </summary>
    public IReadOnlyList<ProtocolEntry> Schaeden => (IReadOnlyList<ProtocolEntry>)GetValue(SchaedenProperty);

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
        panel.AbmeldenVonRecord();
        if (e.NewValue is HaltungRecord record)
        {
            record.PropertyChanged += panel.OnRecordPropertyChanged;
            panel._abonnierterRecord = record;
        }
        panel.AktualisiereRecordTexte();
        // Pruefung/Video haengen an Record, offene KI-Befunde an Entries — beide Ableitungen
        // beim Haltungswechsel gemeinsam neu ziehen, falls Entries bereits gebunden ist.
        panel.AktualisiereSchaeden();
    }

    private static void OnEntriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (HaltungUebersichtPanel)d;
        panel.AbmeldenVonEntries();
        if (e.NewValue is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged += panel.OnEntriesCollectionChanged;
            panel._abonnierteEntries = incc;
        }
        panel.AktualisiereSchaeden();
    }

    private void OnEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => AktualisiereSchaeden();

    /// <summary>
    /// Nova-Fixwelle F5: Eine Feldaenderung am gewaehlten Datensatz (Video verknuepft,
    /// Protokoll ersetzt, Zustandsklasse gesetzt) zieht Pruefung, Video und die Schadenliste nach.
    /// </summary>
    private void OnRecordPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Wie beim Schachtpanel (F6): Die Meldung kommt auf dem setzenden Thread; die
        // Abhaengigkeitseigenschaften gehoeren dem UI-Thread.
        if (Dispatcher.CheckAccess())
        {
            AktualisiereRecordTexte();
            AktualisiereSchaeden();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AktualisiereRecordTexte();
                AktualisiereSchaeden();
            }));
        }
    }

    private void AbmeldenVonEntries()
    {
        if (_abonnierteEntries is null)
            return;
        _abonnierteEntries.CollectionChanged -= OnEntriesCollectionChanged;
        _abonnierteEntries = null;
    }

    private void AbmeldenVonRecord()
    {
        if (_abonnierterRecord is null)
            return;
        _abonnierterRecord.PropertyChanged -= OnRecordPropertyChanged;
        _abonnierterRecord = null;
    }

    /// <summary>Pruefstatus und Videoname aus dem aktuellen Stand von <see cref="Record"/>.</summary>
    private void AktualisiereRecordTexte()
    {
        if (Record is { } record)
        {
            PruefungText = HaltungPruefstatus.Text(HaltungPruefstatus.Bestimme(record));
            var link = record.GetFieldValue(FieldKeys.Link);
            VideoText = string.IsNullOrWhiteSpace(link) ? "kein Video" : Path.GetFileName(link);
        }
        else
        {
            PruefungText = string.Empty;
            VideoText = "kein Video";
        }
    }

    /// <summary>Schadenliste und offene KI-Befunde aus dem aktuellen Stand von <see cref="Entries"/>.</summary>
    private void AktualisiereSchaeden()
    {
        var entries = (Entries?.OfType<ProtocolEntry>() ?? Enumerable.Empty<ProtocolEntry>()).ToList();
        SetValue(SchaedenPropertyKey, RohrringGeometrie.Schaeden(entries));
        // Offene KI-Vorschlaege zaehlen ueber ALLE Eintraege: Auch ein unbestaetigter
        // Bogen oder Rohranfang braucht die fachliche Bestaetigung.
        OffeneKiBefunde = entries.Count(x => !x.IsDeleted && x.Ai is { Accepted: false });
    }
}
