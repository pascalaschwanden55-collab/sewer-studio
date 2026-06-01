using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

public partial class HaltungsansichtView : UserControl
{
    public HaltungsansichtView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) => RefreshDetail();
    }

    private Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>>? _detailBuilder;

    /// <summary>
    /// Wird von der DataPage gesetzt: baut die editierbaren Detail-Gruppen für eine Haltung
    /// (nutzt den bestehenden Pfad CreateHaltungDetailItem/CommitHaltungDetailField).
    /// Beim Setzen wird das Detail sofort aktualisiert, damit die Zuweisungs-Reihenfolge
    /// (vor oder nach dem Sichtbarwerden) keine Rolle spielt.
    /// </summary>
    public Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>>? DetailBuilder
    {
        get => _detailBuilder;
        set
        {
            _detailBuilder = value;
            RefreshDetail();
        }
    }

    /// <summary>
    /// Von der DataPage gesetzt: führt eine Aktion (actionKey) auf einer Haltung aus,
    /// indem sie die bestehenden DataPage-Handler/Commands aufruft.
    /// </summary>
    public Action<string, HaltungRecord>? ActionRequested { get; set; }

    private void HaltungList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshDetail();
    }

    private void SchadenList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender; _ = e;
        if (HaltungList.SelectedItem is HaltungRecord record)
            ActionRequested?.Invoke("codieren", record);
    }

    private void SchadenAdd_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        if (HaltungList.SelectedItem is HaltungRecord record)
            ActionRequested?.Invoke("codieren", record);
    }

    private void RefreshDetail()
    {
        if (!IsVisible)
            return;

        if (HaltungList.SelectedItem is not HaltungRecord record || DetailBuilder is null)
        {
            Detail.Header = "Keine Haltung gewaehlt";
            Detail.SubHeader = "Links eine Haltung waehlen.";
            Detail.Groups = Array.Empty<RecordDetailGroup>();
            return;
        }

        var name = record.GetFieldValue("Haltungsname");
        Detail.Header = string.IsNullOrWhiteSpace(name) ? "Haltungsdetails" : $"Haltung {name}";
        Detail.SubHeader = "Alle Felder editierbar - Aenderungen erscheinen sofort in der Tabelle.";
        Detail.Groups = DetailBuilder(record);
    }
}
