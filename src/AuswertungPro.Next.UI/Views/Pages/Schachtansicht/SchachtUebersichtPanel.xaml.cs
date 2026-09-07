using System;
using System.Collections.Generic;
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
/// </summary>
public partial class SchachtUebersichtPanel : UserControl
{
    public SchachtUebersichtPanel() => InitializeComponent();

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
        var record = e.NewValue as SchachtRecord;
        panel.Entries = record?.Protocol?.Current?.Entries?.Where(x => !x.IsDeleted).ToList();
        panel.AktualisiereGrundriss(record);
    }

    /// <summary>Waehlt Kreis/Oval/Rechteck nach der erfassten Schachtform und schreibt den Masstext.</summary>
    private void AktualisiereGrundriss(SchachtRecord? record)
    {
        var form = SchachtformVokabular.Normalisieren(record?.GetFieldValue(FieldKeys.ShaftShape));
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

        var d1 = (record?.GetFieldValue(FieldKeys.ShaftDimension1Mm) ?? "").Trim();
        var d2 = (record?.GetFieldValue(FieldKeys.ShaftDimension2Mm) ?? "").Trim();
        MassText.Text = d1.Length == 0 && d2.Length == 0 ? "" : $"{d1} × {d2}";
    }
}
