using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.Player;
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

        // Hover-Foto-Vorschau: Projekt-ROOT fuer relative FotoPaths (gleiche Logik wie ExportPdf).
        Behaviors.PhotoHoverPreviewBehavior.SetProjectRootProvider(
            EntriesGrid,
            ResolvePhotoProjectRoot);

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
            _sp.Dialogs.Info("Bitte zuerst eine Beobachtung waehlen.", "Protokoll");
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
            _sp.Dialogs.Info("Bitte zuerst eine Beobachtung waehlen.", "Protokoll");
            return;
        }

        if (!_sp.Dialogs.Confirm("Beobachtung wirklich loeschen?", "Protokoll"))
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

    private void EntriesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Nur echte Datenzeilen: ein Doppelklick auf die Spaltenueberschrift oder
        // in den Leerraum darf nichts oeffnen. VisualTreeSafe statt VisualTreeHelper,
        // weil GetParent auf einem Text-Run abstuerzt.
        if (VisualTreeSafe.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is null)
            return;

        OpenSelectedEntryForEdit();
    }

    private void EntriesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!ProtocolObservationsEditTriggerPolicy.OpensEditor(e.Key))
            return;

        e.Handled = true;
        OpenSelectedEntryForEdit();
    }

    /// <summary>
    /// Oeffnet die gewaehlte Beobachtung und schreibt die Bearbeitung in die
    /// Revisionsspur. Ausgeloest nur durch Doppelklick oder Enter, nie durch die
    /// blosse Zeilenauswahl.
    /// </summary>
    private void OpenSelectedEntryForEdit()
    {
        var entry = SelectedEntry;
        if (!ProtocolObservationsEditTriggerPolicy.CanOpenEditor(
                hasSelectedEntry: entry is not null,
                isOpeningDialog: _isOpeningDialog,
                isRefreshingEntries: _isRefreshingEntries)
            || entry is null)
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
        if (_sp.CodeSelectionCatalog is null)
        {
            _sp.Dialogs.Info("Code-Katalog ist nicht verfuegbar.", "Protokoll");
            return false;
        }

        _isOpeningDialog = true;
        try
        {
            // Moderner VSA-KEK-2020-Dialog (wie im Player/Live-Codieren) statt des alten
            // Beobachtungskatalogs. Beide arbeiten auf ProtocolEntry; der moderne liefert
            // einen NEUEN Entry (SelectedEntry), dessen Werte wir in den bestehenden
            // Eintrag zurueckspiegeln.
            var vm = new AuswertungPro.Next.UI.ViewModels.Windows.VsaCodeExplorerViewModel(
                entry, entry.MeterStart, entry.Zeit, _sp.CodeSelectionCatalog);
            var dlg = new AuswertungPro.Next.UI.Views.Windows.VsaCodeExplorerWindow(vm, _videoPath, entry.Zeit, _sp.CodeUsage)
            {
                Owner = this,
                // Feste Groesse hat hier Vorrang: der Konstruktor ruft WindowStateManager.Track,
                // das aus dem (mit dem Player geteilten) Zustand ggf. Position/Maximiert wiederherstellt.
                // Normal + zentriert erzwingen, damit die angeforderten 1422x851 immer greifen.
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                WindowState = System.Windows.WindowState.Normal,
                Width = 1422,
                Height = 851
            };
            if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
            {
                AuswertungPro.Next.UI.Ai.Coding.CodingProtocolEntryCopier.CopyEditableValues(dlg.SelectedEntry, entry);
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
            _sp.Dialogs.Info("Bitte zuerst eine Beobachtung waehlen.", "Protokoll");
            return;
        }

        if (string.IsNullOrWhiteSpace(_videoPath))
        {
            _sp.Dialogs.Info("Kein Video verlinkt. Bitte zuerst Video verknuepfen.", "Video");
            return;
        }

        var overlayText = BuildOverlayText(entry);
        if (!PlayerWindow.TryShowOverlayOnLast(overlayText, TimeSpan.FromSeconds(6)))
        {
            try
            {
                var options = PlayerWindowOptions.FromSettings(_sp.Settings);
                var window = new PlayerWindow(_videoPath!, options, overlayText, serviceProvider: _sp);
                window.Owner = this;
                window.Show();
            }
            catch (Exception ex)
            {
                var userMessage = UserError.DescribeAndReport(ex, "Protokollvideo starten");
                _sp.Dialogs.Error($"Video konnte nicht gestartet werden: {userMessage}", "Video");
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
        if (!_sp.Dialogs.Confirm("Original-Protokoll wiederherstellen?", "Protokoll"))
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
            _sp.Dialogs.Info("Bitte zuerst eine Beobachtung waehlen.", "Training");
            return;
        }

        _sp.ProtocolTraining.AddSample(entry, _record.GetFieldValue("Haltungsname"));
        _sp.Dialogs.Info("Trainingseintrag gespeichert.", "Training");
    }

    private async void ExportPdf()
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
            ExportPdfButton.IsEnabled = false;
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new HaltungsprotokollPdfOptions
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var root = _projectFolder;
            if (string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath))
                root = AuswertungPro.Next.Application.Common.ProjectFileLocator.ProjectRootFromFile(_sp.Settings.LastProjectPath)
                       ?? Path.GetDirectoryName(_sp.Settings.LastProjectPath);
            root ??= "";
            await BackgroundFileExportRunner.RunAsync(() =>
            {
                var pdf = _sp.ProtocolPdfExports.BuildHaltungsprotokollPdf(
                    _project,
                    _record,
                    _doc,
                    root,
                    options);
                File.WriteAllBytes(output, pdf);
            });

            _sp.Dialogs.Info($"PDF wurde erstellt:\n{output}", "PDF");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Beobachtungs-PDF erstellen");
            _sp.Dialogs.Error($"PDF konnte nicht erstellt werden:\n{userMessage}", "PDF");
        }
        finally
        {
            ExportPdfButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Projekt-ROOT fuer die Hover-Foto-Vorschau: bevorzugt den Konstruktor-Ordner <c>_projectFolder</c>,
    /// sonst aus dem zuletzt geoeffneten Projektpfad abgeleitet (gleiche Fallbacklogik wie <see cref="ExportPdf"/>).
    /// </summary>
    private string? ResolvePhotoProjectRoot()
    {
        var root = _projectFolder;
        if (string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath))
            root = AuswertungPro.Next.Application.Common.ProjectFileLocator.ProjectRootFromFile(_sp.Settings.LastProjectPath)
                   ?? Path.GetDirectoryName(_sp.Settings.LastProjectPath);
        return root;
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
        var ordered = ProtocolEntryOrdering.Order(_doc.Current.Entries);
        var active = ordered.Where(entry => !entry.IsDeleted).ToList();
        _doc.Current.Entries.Clear();
        foreach (var entry in ordered)
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

    private IReadOnlyList<ProtocolEntry> BuildImportedEntries(HaltungRecord record)
    {
        var list = new List<ProtocolEntry>();
        foreach (var f in record.VsaFindings)
        {
            var mStart = f.MeterStart ?? f.SchadenlageAnfang;
            var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
            if (mStart is null && !string.IsNullOrWhiteSpace(f.Raw))
                mStart = ProtocolFindingRawParser.TryParseMeterFromRaw(f.Raw);
            if (mEnd is null && !string.IsNullOrWhiteSpace(f.Raw))
                mEnd = ProtocolFindingRawParser.TryParseSecondMeterFromRaw(f.Raw);
            var time = ProtocolTimeParser.ParseMpegTime(f.MPEG)
                       ?? (f.Timestamp is null ? null : f.Timestamp.Value.TimeOfDay);
            if (time is null && !string.IsNullOrWhiteSpace(f.Raw))
            {
                var rawTime = ProtocolFindingRawParser.TryParseTimeFromRaw(f.Raw);
                time = ProtocolTimeParser.ParseMpegTime(rawTime);
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
}
