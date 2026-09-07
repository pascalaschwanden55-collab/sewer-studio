using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

/// <summary>
/// Schachtansicht rechts neben der Liste (Nova-Etappe 2): Grundriss, Fakten und Schaeden des
/// gewaehlten Schachts, nur lesend. Anders als die Haltungs-Uebersicht liest das Panel seine
/// Schaeden selbst aus <see cref="SchachtRecord.Protocol"/> - es gibt kein eigenes ViewModel-Feld
/// dafuer. Der Knopf "Protokoll (PDF)" fuehrt ueber denselben Aktionsweg wie die alte
/// Schachtansicht.
///
/// Fix-Runde 1/2: Ein In-Place-Neuaufbau des gewaehlten Schachts (zum Beispiel "Aktualisieren"
/// liest das Protokoll neu ein) laesst <c>Record</c> referenzgleich; die Fakten-Felder ziehen ueber
/// die normale WPF-Bindung an <c>Fields[...]</c> ohnehin nach, aber Schaeden und Grundriss werden
/// nur einmal beim Binden berechnet. Das Panel meldet sich deshalb zusaetzlich auf
/// <see cref="SchachtRecord.PropertyChanged"/> an und rechnet bei jeder Meldung neu; die alte
/// Meldung wird beim Wechsel und beim Entladen wieder abgemeldet. Seit Fix-Runde 2 meldet auch
/// der <see cref="SchachtRecord.Protocol"/>-Setter selbst (<c>nameof(Protocol)</c>), ein
/// In-Place-Ersatz des Protokolls kommt also ohne externes Sicherheitsnetz an.
/// </summary>
public partial class SchachtUebersichtPanel : UserControl
{
    private SchachtRecord? _abonniert;

    public SchachtUebersichtPanel()
    {
        InitializeComponent();
        Unloaded += (_, _) => AbmeldenVonRecord();
    }

    public static readonly DependencyProperty RecordProperty = DependencyProperty.Register(
        nameof(Record), typeof(SchachtRecord), typeof(SchachtUebersichtPanel), new PropertyMetadata(null, OnRecordChanged));

    /// <summary>Gewaehlter Schacht; null zeigt eine leere Uebersicht.</summary>
    public SchachtRecord? Record
    {
        get => (SchachtRecord?)GetValue(RecordProperty);
        set => SetValue(RecordProperty, value);
    }

    public static readonly DependencyProperty EntriesProperty = DependencyProperty.Register(
        nameof(Entries), typeof(IReadOnlyList<ProtocolEntry>), typeof(SchachtUebersichtPanel), new PropertyMetadata(null));

    /// <summary>Schaeden des gewaehlten Schachts, aus <c>Record.Protocol.Current.Entries</c> ohne geloeschte Zeilen.</summary>
    public IReadOnlyList<ProtocolEntry>? Entries
    {
        get => (IReadOnlyList<ProtocolEntry>?)GetValue(EntriesProperty);
        private set => SetValue(EntriesProperty, value);
    }

    /// <summary>Knopf "Protokoll (PDF)": oeffnet das Schachtprotokoll ueber denselben Weg wie die Seite.</summary>
    public Action<SchachtRecord>? PdfRequested { get; set; }

    private void Pdf_Click(object sender, RoutedEventArgs e)
    {
        if (Record is { } r)
            PdfRequested?.Invoke(r);
    }

    private static void OnRecordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (SchachtUebersichtPanel)d;
        panel.AbmeldenVonRecord();
        if (e.NewValue is SchachtRecord record)
        {
            record.PropertyChanged += panel.OnRecordPropertyChanged;
            panel._abonniert = record;
        }
        panel.Aktualisiere();
    }

    /// <summary>
    /// Nova-Fixwelle F6: <see cref="SchachtRecord.PropertyChanged"/> feuert auf dem setzenden
    /// Thread — ein Import oder Nachlauf im Hintergrund meldet also nicht auf dem UI-Thread.
    /// <see cref="Aktualisiere"/> setzt aber Abhaengigkeitseigenschaften und die Sichtbarkeit von
    /// Formen; das darf nur der UI-Thread. Muster wie <c>ProjektUebersichtPageViewModel.OnRegisterGeaendert</c>.
    /// </summary>
    private void OnRecordPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Dispatcher.CheckAccess())
            Aktualisiere();
        else
            Dispatcher.BeginInvoke(new Action(Aktualisiere));
    }

    private void AbmeldenVonRecord()
    {
        if (_abonniert is null)
            return;
        _abonniert.PropertyChanged -= OnRecordPropertyChanged;
        _abonniert = null;
    }

    /// <summary>
    /// Berechnet Schaeden, Grundriss und Masstext aus dem aktuellen <see cref="Record"/> neu.
    /// Wird vom abonnierten <see cref="SchachtRecord.PropertyChanged"/> automatisch aufgerufen
    /// (auch bei einem ersetzten <c>Protocol</c>, seit dessen Setter selbst meldet); oeffentlich
    /// nur fuer Tests und einen bewussten manuellen Anstoss.
    /// </summary>
    public void Aktualisiere()
    {
        var record = Record;
        Entries = record?.Protocol?.Current?.Entries?.Where(x => !x.IsDeleted).ToList();
        AktualisiereGrundriss(record);
    }

    /// <summary>Waehlt Kreis/Oval/Rechteck nach der erfassten Schachtform und schreibt den Masstext.</summary>
    private void AktualisiereGrundriss(SchachtRecord? record)
    {
        // F2: Schachtfelder ueber SchachtFeldnamen lesen — der Datensatz fuehrt sie unter der
        // Kopfzeile der Excel-Vorlage, nicht unter dem Katalognamen.
        var form = SchachtformVokabular.Normalisieren(Wert(record, FieldKeys.ShaftShape));
        Kreis.Visibility = Visibility.Collapsed;
        Oval.Visibility = Visibility.Collapsed;
        Quadrat.Visibility = Visibility.Collapsed;
        Shape sichtbar = form switch
        {
            "Oval" or "Rechteckig" => Oval,
            "Quadratisch" => Quadrat,
            _ => Kreis
        };
        sichtbar.Visibility = Visibility.Visible;

        var d1 = (Wert(record, FieldKeys.ShaftDimension1Mm) ?? "").Trim();
        var d2 = (Wert(record, FieldKeys.ShaftDimension2Mm) ?? "").Trim();
        MassText.Text = d1.Length == 0 && d2.Length == 0 ? "" : $"{d1} × {d2}";
    }

    /// <summary>Feldwert eines Schachts unter der Schreibweise, die der Datensatz wirklich fuehrt.</summary>
    private static string? Wert(SchachtRecord? record, string feld)
        => record is null ? null : record.GetFieldValue(SchachtFeldnamen.Feld(record, feld));
}
