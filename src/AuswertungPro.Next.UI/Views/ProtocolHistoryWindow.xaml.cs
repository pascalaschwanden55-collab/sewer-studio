using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views;

public sealed partial class ProtocolHistoryWindow : Window
{
    private readonly ProtocolDocument _doc;
    private readonly IProtocolService _protocols;
    private readonly Action _onRestored;
    private readonly ObservableCollection<HistoryRow> _rows = new();

    public ProtocolHistoryWindow(ProtocolDocument doc, IProtocolService protocols, Action onRestored)
    {
        InitializeComponent();
        _doc = doc;
        _protocols = protocols;
        _onRestored = onRestored;

        LoadRows();
        HistoryGrid.ItemsSource = _rows;
        HistoryGrid.SelectionChanged += (_, _) => UpdatePreview();

        RestoreButton.Click += (_, _) => RestoreSelected();
        CloseButton.Click += (_, _) => Close();
    }

    private void LoadRows()
    {
        _rows.Clear();
        foreach (var rev in _doc.History)
        {
            _rows.Add(new HistoryRow
            {
                Revision = rev,
                CreatedAt = rev.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                Comment = rev.Comment ?? "",
                CreatedBy = rev.CreatedBy ?? "",
                EntryCount = rev.Entries.Count
            });
        }
    }

    private void RestoreSelected()
    {
        var row = HistoryGrid.SelectedItem as HistoryRow;
        if (row?.Revision is null)
        {
            MessageBox.Show("Bitte eine Revision auswaehlen.", "Historie", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show("Diese Revision wiederherstellen?", "Historie", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        _protocols.RestoreRevision(_doc, row.Revision, user: null, comment: "Wiederhergestellt aus Historie");
        _onRestored();
        LoadRows();
    }

    private void UpdatePreview()
    {
        var row = HistoryGrid.SelectedItem as HistoryRow;
        if (row?.Revision is null)
        {
            PreviewBox.Text = "";
            return;
        }

        var lines = row.Revision.Entries
            .Where(e => !e.IsDeleted)
            .Select(e =>
            {
                var range = e.IsStreckenschaden ? "Strecke" : "Meter";
                var m1 = e.MeterStart?.ToString("0.00") ?? "-";
                var m2 = e.MeterEnd?.ToString("0.00") ?? "-";
                var text = string.IsNullOrWhiteSpace(e.Beschreibung) ? "" : $" | {e.Beschreibung}";
                var time = e.Zeit is null ? "" : $" | Zeit {e.Zeit:hh\\:mm\\:ss}";
                var photos = e.FotoPaths?.Count > 0 ? $" | Fotos {e.FotoPaths.Count}" : "";
                var param = e.CodeMeta?.Parameters is { Count: > 0 }
                    ? " | " + string.Join(", ", e.CodeMeta.Parameters.Select(kv => $"{kv.Key}={kv.Value}"))
                    : "";
                return $"{e.Code} | {range} {m1}-{m2}{text}{param}{time}{photos}";
            })
            .ToList();

        PreviewBox.Text = lines.Count == 0 ? "(keine Beobachtungen)" : string.Join(Environment.NewLine, lines);
    }

    private sealed class HistoryRow
    {
        public ProtocolRevision? Revision { get; set; }
        public string CreatedAt { get; set; } = "";
        public string Comment { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public int EntryCount { get; set; }
    }
}
