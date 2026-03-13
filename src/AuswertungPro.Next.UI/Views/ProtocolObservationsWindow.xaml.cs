using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.UI.Views;

public partial class ProtocolObservationsWindow : Window
{
    private readonly HaltungRecord _record;
    private readonly Project _project;
    private readonly ServiceProvider _sp;
    private readonly string? _videoPath;
    private readonly string? _projectFolder;
    private readonly ProtocolDocument _doc;
    private readonly Action _markDirty;
    private readonly ObservableCollection<ProtocolEntry> _entries = new();
    private bool _isOpeningDialog;
    private bool _isRefreshingEntries;

    public ProtocolObservationsWindow(
        HaltungRecord record,
        Project project,
        ServiceProvider sp,
        string? videoPath,
        string? projectFolder,
        Action markDirty)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _record = record;
        _project = project;
        _sp = sp;
        _videoPath = videoPath;
        _projectFolder = projectFolder;
        _markDirty = markDirty;

        _doc = EnsureDocument(record);
        HeaderText.Text = string.IsNullOrWhiteSpace(record.GetFieldValue("Haltungsname"))
            ? "Beobachtungen / Schaeden"
            : $"Beobachtungen / Schaeden - {record.GetFieldValue("Haltungsname")}";
        RefreshRevisionHeader();

        LoadEntries();
        EntriesGrid.ItemsSource = _entries;

        NewButton.Click += (_, _) => AddEntry();
        CopyButton.Click += (_, _) => CopyEntry();
        DeleteButton.Click += (_, _) => DeleteEntry();
        OverlayButton.Click += (_, _) => OverlayEntry();
        TrainButton.Click += (_, _) => TrainEntry();
        ExportPdfButton.Click += (_, _) => ExportPdf();
        CloseButton.Click += (_, _) => Close();

        StartNachprotokollButton.Click += (_, _) => StartNachprotokoll();
        StartNeuButton.Click += (_, _) => StartNeuProtokoll();
        RestoreOriginalButton.Click += (_, _) => RestoreOriginal();
        HistoryButton.Click += (_, _) => ShowHistory();
    }

    private ProtocolDocument EnsureDocument(HaltungRecord record)
    {
        if (record.Protocol is not null)
        {
            record.Protocol.Current ??= new ProtocolRevision { Comment = "Arbeitskopie", Entries = new List<ProtocolEntry>() };
            if (record.Protocol.Original.Entries.Count == 0
                && record.Protocol.Current.Entries.Count == 0
                && record.VsaFindings is { Count: > 0 })
            {
                var imported = BuildImportedEntries(record);
                record.Protocol = _sp.Protocols.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", imported, null);
            }

            return record.Protocol;
        }

        var entries = record.VsaFindings is { Count: > 0 }
            ? BuildImportedEntries(record)
            : Array.Empty<ProtocolEntry>();
        var doc = _sp.Protocols.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
        record.Protocol = doc;
        return doc;
    }

    private void LoadEntries()
    {
        ResortActiveEntries();
    }

    private void RefreshRevisionHeader()
    {
        var rev = _doc.Current;
        var who = string.IsNullOrWhiteSpace(rev.CreatedBy) ? "unbekannt" : rev.CreatedBy;
        RevisionText.Text = $"Revision: {rev.Comment} / {rev.CreatedAt:dd.MM.yyyy HH:mm} / {who}";
    }

    private ProtocolEntry? SelectedEntry => EntriesGrid.SelectedItem as ProtocolEntry;

    private void AddEntry()
    {
        var entry = new ProtocolEntry { Source = ProtocolEntrySource.Manual };
        if (!OpenObservationDialog(entry))
            return;

        _doc.Current.Entries.Add(entry);
        _doc.Current.Changes.Add(new ProtocolChange
        {
            Kind = ProtocolChangeKind.Add,
            EntryId = entry.EntryId,
            After = SerializeEntry(entry)
        });
        ResortActiveEntries(entry);
        MarkDirty();
        RefreshRevisionHeader();
    }

    private void CopyEntry()
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show("Bitte zuerst eine Beobachtung waehlen.", "Protokoll", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var json = SerializeEntry(entry);
        var copy = JsonSerializer.Deserialize<ProtocolEntry>(json) ?? new ProtocolEntry();
        copy.EntryId = Guid.NewGuid();
        copy.Source = ProtocolEntrySource.Manual;

        if (!OpenObservationDialog(copy))
            return;

        _doc.Current.Entries.Add(copy);
        _doc.Current.Changes.Add(new ProtocolChange
        {
            Kind = ProtocolChangeKind.Add,
            EntryId = copy.EntryId,
            After = SerializeEntry(copy)
        });
        ResortActiveEntries(copy);
        MarkDirty();
        RefreshRevisionHeader();
    }

    private void DeleteEntry()
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show("Bitte zuerst eine Beobachtung waehlen.", "Protokoll", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show("Beobachtung wirklich loeschen?", "Protokoll", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        entry.IsDeleted = true;
        _doc.Current.Changes.Add(new ProtocolChange
        {
            Kind = ProtocolChangeKind.Delete,
            EntryId = entry.EntryId,
            Before = SerializeEntry(entry)
        });
        _entries.Remove(entry);
        MarkDirty();
        RefreshRevisionHeader();
    }

    private void EntriesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isOpeningDialog || _isRefreshingEntries)
            return;

        var entry = SelectedEntry;
        if (entry is null)
            return;

        var before = SerializeEntry(entry);
        if (!OpenObservationDialog(entry))
            return;

        _doc.Current.Changes.Add(new ProtocolChange
        {
            Kind = ProtocolChangeKind.Edit,
            EntryId = entry.EntryId,
            Before = before,
            After = SerializeEntry(entry)
        });
        ResortActiveEntries(entry);
        MarkDirty();
        RefreshRevisionHeader();
    }

    private bool OpenObservationDialog(ProtocolEntry entry)
    {
        if (_sp.CodeCatalog is null)
        {
            MessageBox.Show("Code-Katalog ist nicht verfuegbar.", "Protokoll", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        _isOpeningDialog = true;
        try
        {
            var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
                entry,
                entry.MeterStart,
                entry.Zeit);

            var dlg = new Windows.VsaCodeExplorerWindow(explorerVm, _videoPath)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
            {
                var result = dlg.SelectedEntry;
                entry.Code = result.Code;
                entry.Beschreibung = result.Beschreibung;
                entry.CodeMeta = result.CodeMeta;
                entry.MeterStart = result.MeterStart;
                entry.MeterEnd = result.MeterEnd;
                entry.Zeit = result.Zeit;
                entry.IsStreckenschaden = result.IsStreckenschaden;
                entry.FotoPaths = result.FotoPaths;
                return true;
            }
            return false;
        }
        finally
        {
            _isOpeningDialog = false;
        }
    }

    private void OverlayEntry()
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show("Bitte zuerst eine Beobachtung waehlen.", "Protokoll", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_videoPath))
        {
            MessageBox.Show("Kein Video verlinkt. Bitte zuerst Video verknuepfen.", "Video", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var overlayText = BuildOverlayText(entry);
        if (!PlayerWindow.TryShowOverlayOnLast(overlayText, TimeSpan.FromSeconds(6)))
        {
            try
            {
                var options = new PlayerWindowOptions(
                    EnableHardwareDecoding: _sp.Settings.VideoHwDecoding,
                    DropLateFrames: _sp.Settings.VideoDropLateFrames,
                    SkipFrames: _sp.Settings.VideoSkipFrames,
                    FileCachingMs: _sp.Settings.VideoFileCachingMs,
                    NetworkCachingMs: _sp.Settings.VideoNetworkCachingMs,
                    CodecThreads: _sp.Settings.VideoCodecThreads,
                    VideoOutput: _sp.Settings.VideoOutput);
                var window = new PlayerWindow(_videoPath!, options, overlayText);
                window.Owner = this;
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Video konnte nicht gestartet werden: {ex.Message}", "Video", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void StartNachprotokoll()
    {
        var comment = "Nachprotokoll";
        _sp.Protocols.StartNachprotokoll(_doc, user: null, comment: comment);
        LoadEntries();
        EntriesGrid.Items.Refresh();
        MarkDirty();
        RefreshRevisionHeader();
    }

    private void StartNeuProtokoll()
    {
        var comment = "Neu protokolliert (leer)";
        _sp.Protocols.StartNeuProtokoll(_doc, user: null, comment: comment);
        LoadEntries();
        EntriesGrid.Items.Refresh();
        MarkDirty();
        RefreshRevisionHeader();
    }

    private void RestoreOriginal()
    {
        var confirm = MessageBox.Show("Original-Protokoll wiederherstellen?", "Protokoll",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        _sp.Protocols.RestoreOriginal(_doc, user: null);
        LoadEntries();
        EntriesGrid.Items.Refresh();
        MarkDirty();
        RefreshRevisionHeader();
    }

    private void ShowHistory()
    {
        var dlg = new ProtocolHistoryWindow(_doc, _sp.Protocols, () =>
        {
            LoadEntries();
            EntriesGrid.Items.Refresh();
            MarkDirty();
            RefreshRevisionHeader();
        });
        dlg.Owner = this;
        dlg.ShowDialog();
    }

    private void TrainEntry()
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show("Bitte zuerst eine Beobachtung waehlen.", "Training", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ProtocolTrainingStore.AddSample(entry, _record.GetFieldValue("Haltungsname"));
        MessageBox.Show("Trainingseintrag gespeichert.", "Training", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportPdf()
    {
        var holding = _record.GetFieldValue("Haltungsname");
        var defaultName = $"Haltungsprotokoll_{SanitizeFilePart(holding)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Haltungsprotokoll als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new HaltungsprotokollPdfOptions
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var root = _projectFolder;
            if (string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath))
                root = Path.GetDirectoryName(_sp.Settings.LastProjectPath);
            root ??= "";
            var pdf = _sp.ProtocolPdfExporter.BuildHaltungsprotokollPdf(_project, _record, _doc, root, options);
            File.WriteAllBytes(output, pdf);

            MessageBox.Show($"PDF wurde erstellt:\n{output}", "PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF konnte nicht erstellt werden:\n{ex.Message}", "PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string BuildOverlayText(ProtocolEntry entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Code))
            parts.Add(entry.Code.Trim());
        if (!string.IsNullOrWhiteSpace(entry.Beschreibung))
            parts.Add(entry.Beschreibung.Trim());
        if (entry.MeterStart.HasValue || entry.MeterEnd.HasValue)
        {
            var m1 = entry.MeterStart?.ToString("0.00") ?? "-";
            var m2 = entry.MeterEnd?.ToString("0.00") ?? "-";
            parts.Add(entry.IsStreckenschaden ? $"Strecke {m1} - {m2} m" : $"Meter {m1} - {m2}");
        }

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string SanitizeFilePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Haltung";
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Length > 80 ? value.Substring(0, 80) : value;
    }

    private void MarkDirty()
    {
        SyncPrimaryDamagesFromCurrentEntries();
        _record.ModifiedAtUtc = DateTime.UtcNow;
        _markDirty();
    }

    private void SyncPrimaryDamagesFromCurrentEntries()
    {
        var lines = BuildPrimaryDamageLinesFromCurrentEntries();
        var primaryDamages = XtfPrimaryDamageFormatter.DeduplicateText(string.Join("\n", lines));
        var current = _record.GetFieldValue("Primaere_Schaeden");

        if (string.Equals(current, primaryDamages, StringComparison.Ordinal))
            return;

        _record.SetFieldValue("Primaere_Schaeden", primaryDamages, FieldSource.Manual, userEdited: true);
    }

    private IReadOnlyList<string> BuildPrimaryDamageLinesFromCurrentEntries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in _doc.Current.Entries.Where(e => !e.IsDeleted))
        {
            var code = (entry.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            // Deduplicate by code + meter position
            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            var key = $"{code.ToUpperInvariant()}|{meterKey}";
            if (!seen.Add(key))
                continue;

            var line = code;
            if (meter.HasValue)
                line += $" @{meter.Value.ToString("0.###", CultureInfo.InvariantCulture)}m";

            var description = BuildPrimaryDamageDescription(entry);
            if (!string.IsNullOrWhiteSpace(description))
                line += $" ({description})";

            lines.Add(line);
        }

        return lines;
    }

    private static string? BuildPrimaryDamageDescription(ProtocolEntry entry)
    {
        var raw = !string.IsNullOrWhiteSpace(entry.Beschreibung)
            ? entry.Beschreibung
            : entry.CodeMeta?.Notes;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var singleLine = string.Join(" ",
            raw.Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0));

        var compact = string.Join(" ",
            singleLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return compact;
    }

    private static string SerializeEntry(ProtocolEntry entry)
        => JsonSerializer.Serialize(entry);

    private void ResortActiveEntries(ProtocolEntry? selectedEntry = null)
    {
        var active = _doc.Current.Entries
            .Where(e => !e.IsDeleted)
            .Select((entry, index) => new
            {
                Entry = entry,
                Index = index,
                MeterStart = TryGetPrimaryOrderingMeter(entry),
                MeterEnd = TryGetSecondaryOrderingMeter(entry)
            })
            .OrderBy(x => x.MeterStart.HasValue ? 0 : 1)
            .ThenBy(x => x.MeterStart ?? double.MaxValue)
            .ThenBy(x => x.MeterEnd.HasValue ? 0 : 1)
            .ThenBy(x => x.MeterEnd ?? double.MaxValue)
            .ThenBy(x => x.Entry.Code ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Index)
            .Select(x => x.Entry)
            .ToList();

        var deleted = _doc.Current.Entries.Where(e => e.IsDeleted).ToList();
        _doc.Current.Entries.Clear();
        foreach (var entry in active)
            _doc.Current.Entries.Add(entry);
        foreach (var entry in deleted)
            _doc.Current.Entries.Add(entry);

        _isRefreshingEntries = true;
        try
        {
            _entries.Clear();
            foreach (var entry in active)
                _entries.Add(entry);

            var target = selectedEntry ?? SelectedEntry;
            if (target is not null && active.Contains(target))
                EntriesGrid.SelectedItem = target;
        }
        finally
        {
            _isRefreshingEntries = false;
        }

        EntriesGrid.Items.Refresh();
    }

    private static double? TryGetPrimaryOrderingMeter(ProtocolEntry entry)
    {
        var direct = entry.MeterStart ?? entry.MeterEnd;
        if (direct.HasValue)
            return direct;

        if (entry.CodeMeta?.Parameters is null)
            return null;

        var keys = new[] { "vsa.distanz", "Distance" };
        foreach (var key in keys)
        {
            if (!entry.CodeMeta.Parameters.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;
            var normalized = raw.Trim().Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return null;
    }

    private static double? TryGetSecondaryOrderingMeter(ProtocolEntry entry)
    {
        var direct = entry.MeterEnd ?? entry.MeterStart;
        if (direct.HasValue && !double.IsNaN(direct.Value) && !double.IsInfinity(direct.Value))
            return direct.Value;

        return TryGetPrimaryOrderingMeter(entry);
    }

    private IReadOnlyList<ProtocolEntry> BuildImportedEntries(HaltungRecord record)
    {
        var list = new List<ProtocolEntry>();
        foreach (var f in record.VsaFindings)
        {
            var mStart = f.MeterStart ?? f.SchadenlageAnfang;
            var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
            if (mStart is null && !string.IsNullOrWhiteSpace(f.Raw))
                mStart = TryParseMeterFromRaw(f.Raw);
            if (mEnd is null && !string.IsNullOrWhiteSpace(f.Raw))
                mEnd = TryParseSecondMeterFromRaw(f.Raw);
            var time = ParseMpegTime(f.MPEG)
                       ?? (f.Timestamp is null ? null : f.Timestamp.Value.TimeOfDay);
            if (time is null && !string.IsNullOrWhiteSpace(f.Raw))
            {
                var rawTime = TryParseTimeFromRaw(f.Raw);
                time = ParseMpegTime(rawTime);
                if (string.IsNullOrWhiteSpace(f.MPEG) && !string.IsNullOrWhiteSpace(rawTime))
                    f.MPEG = rawTime;
            }

            var beschreibung = f.Raw?.Trim() ?? string.Empty;
            var code = f.KanalSchadencode?.Trim() ?? string.Empty;
            // Beschreibung aus dem VSA-Katalog auflösen, wenn Raw leer oder nur Kuerzel
            if ((string.IsNullOrWhiteSpace(beschreibung) || beschreibung.Length <= 3) &&
                !string.IsNullOrWhiteSpace(code) &&
                _sp.CodeCatalog.TryGet(code, out var codeDef) &&
                !string.IsNullOrWhiteSpace(codeDef.Title))
            {
                beschreibung = codeDef.Title;
            }

            var entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = beschreibung,
                MeterStart = mStart,
                MeterEnd = mEnd,
                IsStreckenschaden = mStart.HasValue && mEnd.HasValue && mEnd >= mStart,
                Mpeg = f.MPEG,
                Zeit = time,
                Source = ProtocolEntrySource.Imported
            };

            {
                var importParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(f.Quantifizierung1))
                {
                    importParams["vsa.q1"] = f.Quantifizierung1.Trim();
                    importParams["Quantifizierung1"] = f.Quantifizierung1.Trim();
                }
                if (!string.IsNullOrWhiteSpace(f.Quantifizierung2))
                {
                    importParams["vsa.q2"] = f.Quantifizierung2.Trim();
                    importParams["Quantifizierung2"] = f.Quantifizierung2.Trim();
                }
                if (!string.IsNullOrWhiteSpace(f.SchadenlageAnfang?.ToString()) || !string.IsNullOrWhiteSpace(f.SchadenlageEnde?.ToString()))
                {
                    if (f.SchadenlageAnfang.HasValue)
                        importParams["vsa.uhr.von"] = f.SchadenlageAnfang.Value.ToString("0", CultureInfo.InvariantCulture);
                    if (f.SchadenlageEnde.HasValue)
                        importParams["vsa.uhr.bis"] = f.SchadenlageEnde.Value.ToString("0", CultureInfo.InvariantCulture);
                }
                if (mStart.HasValue)
                    importParams["vsa.distanz"] = mStart.Value.ToString("0.00", CultureInfo.InvariantCulture);
                if (time.HasValue)
                    importParams["vsa.video"] = time.Value.TotalHours >= 1
                        ? time.Value.ToString(@"hh\:mm\:ss")
                        : time.Value.ToString(@"mm\:ss");

                if (importParams.Count > 0)
                {
                    entry.CodeMeta = new ProtocolEntryCodeMeta
                    {
                        Code = entry.Code,
                        Parameters = importParams,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }

    private static readonly System.Text.RegularExpressions.Regex RawMeterRegex =
        new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex RawTimeRegex =
        new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static double? TryParseMeterFromRaw(string raw)
    {
        var match = RawMeterRegex.Match(raw);
        if (!match.Success)
            return null;

        var text = match.Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static double? TryParseSecondMeterFromRaw(string raw)
    {
        var matches = RawMeterRegex.Matches(raw);
        if (matches.Count < 2)
            return null;

        var text = matches[1].Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string? TryParseTimeFromRaw(string raw)
    {
        var match = RawTimeRegex.Match(raw);
        return match.Success ? match.Groups[1].Value : null;
    }
}
