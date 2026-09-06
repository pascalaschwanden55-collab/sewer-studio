using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>
/// Uebersicht rechts neben der Liste: Eckdaten und Primaere Schaeden der gewaehlten Haltung,
/// nur lesend. Der Doppelklick auf einen Schaden fuehrt auf denselben Weg wie die
/// Haltungsansicht (Beobachtungen).
/// </summary>
public partial class HaltungUebersichtPanel : UserControl
{
    public HaltungUebersichtPanel() => InitializeComponent();

    public static readonly DependencyProperty RecordProperty = DependencyProperty.Register(
        nameof(Record), typeof(HaltungRecord), typeof(HaltungUebersichtPanel));

    /// <summary>Gewaehlte Haltung; null zeigt eine leere Uebersicht.</summary>
    public HaltungRecord? Record
    {
        get => (HaltungRecord?)GetValue(RecordProperty);
        set => SetValue(RecordProperty, value);
    }

    public static readonly DependencyProperty EntriesProperty = DependencyProperty.Register(
        nameof(Entries), typeof(IEnumerable), typeof(HaltungUebersichtPanel));

    /// <summary>Primaere Schaeden der gewaehlten Haltung (ProtocolEntry-Liste des ViewModels).</summary>
    public IEnumerable? Entries
    {
        get => (IEnumerable?)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    /// <summary>Doppelklick auf einen Schaden: dieselbe Aktion wie in der Haltungsansicht (Beobachtungen).</summary>
    public Action<HaltungRecord>? BeobachtungenRequested { get; set; }

    private void Schaden_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Record is { } r)
            BeobachtungenRequested?.Invoke(r);
    }
}
