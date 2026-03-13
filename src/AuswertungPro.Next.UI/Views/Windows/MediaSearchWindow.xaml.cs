using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class MediaSearchWindow : Window
{
    private readonly IReadOnlyList<HaltungRecord> _records;
    private readonly string? _initialFolder;
    private CancellationTokenSource? _cts;
    private List<MediaMatchRow>? _rows;

    /// <summary>True if the user clicked "Anwenden" and changes were applied.</summary>
    public bool Applied { get; private set; }

    /// <summary>Number of video links that were applied.</summary>
    public int AppliedVideoCount { get; private set; }

    /// <summary>Number of PDF links that were applied.</summary>
    public int AppliedPdfCount { get; private set; }

    /// <summary>Number of photos that were applied to protocol entries.</summary>
    public int AppliedFotoCount { get; private set; }

    public MediaSearchWindow(IReadOnlyList<HaltungRecord> records, string? initialFolder)
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        _records = records;
        _initialFolder = initialFolder;

        if (!string.IsNullOrWhiteSpace(_initialFolder))
            FolderBox.Text = _initialFolder;

        Closed += (_, _) => _cts?.Cancel();
        Loaded += (_, _) => EnsureVisibleOnScreen();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var sp = (ServiceProvider)App.Services;
        var folder = sp.Dialogs.SelectFolder("Medien-Suchordner waehlen", FolderBox.Text);
        if (!string.IsNullOrWhiteSpace(folder))
            FolderBox.Text = folder;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show("Bitte einen gueltigen Ordner waehlen.", "Medien-Suche",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StartButton.IsEnabled = false;
        CancelSearchButton.IsEnabled = true;
        ApplyButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var options = new BatchMediaSearchOptions
        {
            SearchFolder = folder,
            OverwriteExisting = OverwriteCheck.IsChecked == true,
            SearchPdfs = PdfCheck.IsChecked == true,
            SearchPhotos = FotoCheck.IsChecked == true,
            Recursive = RecursiveCheck.IsChecked == true
        };

        // Index-Phase: indeterminate (animiert), Matching-Phase: echte Prozentwerte
        ProgressBar.IsIndeterminate = true;
        ProgressBar.Value = 0;

        var progress = new Progress<(int current, int total, string status)>(p =>
        {
            if (p.total <= 0)
            {
                ProgressBar.IsIndeterminate = true;
            }
            else
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Maximum = p.total;
                ProgressBar.Value = p.current;
            }
            ProgressText.Text = p.status;
        });

        try
        {
            var service = new BatchMediaSearchService();
            var results = await Task.Run(() => service.Search(_records, options, progress, ct), ct);

            _rows = results.Select(r => new MediaMatchRow(r)).ToList();
            ResultGrid.ItemsSource = _rows;

            var found = results.Count(r => r.VideoStatus == MediaMatchStatus.Found);
            var ambiguous = results.Count(r => r.VideoStatus == MediaMatchStatus.Ambiguous);
            var notFound = results.Count(r => r.VideoStatus == MediaMatchStatus.NotFound);
            var alreadyLinked = results.Count(r => r.VideoStatus == MediaMatchStatus.AlreadyLinked);
            var fotosFound = results.Count(r => r.FotoStatus == MediaMatchStatus.Found);
            var totalFotos = results.Sum(r => r.FotoPaths.Count);

            var fotoSummary = options.SearchPhotos ? $" | Fotos: {fotosFound} Haltungen ({totalFotos} Dateien)" : "";
            SummaryText.Text = $"{found} gefunden, {ambiguous} mehrdeutig, {notFound} nicht gefunden, {alreadyLinked} bereits verlinkt{fotoSummary}";
            ApplyButton.IsEnabled = _rows.Any(r => r.Apply);
        }
        catch (OperationCanceledException)
        {
            SummaryText.Text = "Suche abgebrochen.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler bei der Suche:\n{ex.Message}", "Medien-Suche",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StartButton.IsEnabled = true;
            CancelSearchButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelSearch_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_rows is null) return;

        int videoCount = 0;
        int pdfCount = 0;
        int fotoCount = 0;

        foreach (var row in _rows.Where(r => r.Apply))
        {
            if (!string.IsNullOrWhiteSpace(row.VideoPath)
                && row.Match.VideoStatus is MediaMatchStatus.Found or MediaMatchStatus.Ambiguous)
            {
                row.Match.Record.SetFieldValue("Link", row.VideoPath, FieldSource.Unknown, userEdited: false);
                videoCount++;
            }

            if (!string.IsNullOrWhiteSpace(row.PdfPath)
                && row.Match.PdfStatus is MediaMatchStatus.Found or MediaMatchStatus.Ambiguous)
            {
                row.Match.Record.SetFieldValue("PDF_Path", row.PdfPath, FieldSource.Unknown, userEdited: false);
                pdfCount++;
            }

            // Apply photos to protocol entries
            if (row.Match.FotoPaths.Count > 0)
            {
                var record = row.Match.Record;

                // Protokoll anlegen falls keines existiert
                if (record.Protocol is null)
                {
                    var haltungId = record.GetFieldValue("Haltungsname") ?? "";
                    record.Protocol = new AuswertungPro.Next.Domain.Protocol.ProtocolDocument
                    {
                        HaltungId = haltungId,
                        Original = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                        {
                            Comment = "Medien-Import",
                            Entries = new List<AuswertungPro.Next.Domain.Protocol.ProtocolEntry>()
                        },
                        Current = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                        {
                            Comment = "Arbeitskopie",
                            Entries = new List<AuswertungPro.Next.Domain.Protocol.ProtocolEntry>()
                        }
                    };
                }

                record.Protocol.Current ??= new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                {
                    Comment = "Arbeitskopie",
                    Entries = new List<AuswertungPro.Next.Domain.Protocol.ProtocolEntry>()
                };

                var entries = record.Protocol.Current.Entries
                    .Where(entry => !entry.IsDeleted)
                    .ToList();

                foreach (var fotoPath in row.Match.FotoPaths)
                {
                    var meter = TryParseMeterFromFileName(Path.GetFileNameWithoutExtension(fotoPath));
                    if (meter is not null && entries.Count > 0)
                    {
                        var best = entries
                            .Where(entry => entry.MeterStart is not null)
                            .OrderBy(entry => Math.Abs(entry.MeterStart!.Value - meter.Value))
                            .FirstOrDefault();

                        if (best is not null && Math.Abs(best.MeterStart!.Value - meter.Value) <= 1.0)
                        {
                            if (!best.FotoPaths.Contains(fotoPath, StringComparer.OrdinalIgnoreCase))
                            {
                                best.FotoPaths.Add(fotoPath);
                                fotoCount++;
                            }
                            continue;
                        }
                    }

                    // Kein Meter-Match oder keine Entries: Foto-Entry anlegen
                    if (entries.Count > 0)
                    {
                        var first = entries[0];
                        if (!first.FotoPaths.Contains(fotoPath, StringComparer.OrdinalIgnoreCase))
                        {
                            first.FotoPaths.Add(fotoPath);
                            fotoCount++;
                        }
                    }
                    else
                    {
                        // Keine Beobachtungen vorhanden: Platzhalter-Entry anlegen
                        var placeholder = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                        {
                            Code = "",
                            Beschreibung = "Foto (automatisch zugeordnet)",
                            MeterStart = meter,
                            Source = AuswertungPro.Next.Domain.Protocol.ProtocolEntrySource.Imported,
                            FotoPaths = new List<string> { fotoPath }
                        };
                        record.Protocol.Current.Entries.Add(placeholder);
                        entries.Add(placeholder);
                        fotoCount++;
                    }
                }
            }
        }

        AppliedVideoCount = videoCount;
        AppliedPdfCount = pdfCount;
        AppliedFotoCount = fotoCount;
        Applied = videoCount > 0 || pdfCount > 0 || fotoCount > 0;

        // Persist last folder
        var sp = (ServiceProvider)App.Services;
        sp.Settings.LastVideoSourceFolder = FolderBox.Text.Trim();
        sp.Settings.Save();

        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void EnsureVisibleOnScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width) Width = area.Width - 20;
        if (Height > area.Height) Height = area.Height - 20;
        if (Left < area.Left) Left = area.Left;
        if (Top < area.Top) Top = area.Top;
        if (Left + Width > area.Right) Left = area.Right - Width;
        if (Top + Height > area.Bottom) Top = area.Bottom - Height;
    }

    private static double? TryParseMeterFromFileName(string name)
    {
        var m = Regex.Match(name, @"(?<m>\d{1,3}([.,]\d{1,2})?)\s*m?$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var s = m.Groups["m"].Value.Replace(',', '.');
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return val;

        return null;
    }
}

/// <summary>Row model for the results DataGrid with INotifyPropertyChanged for the Apply checkbox.</summary>
public sealed class MediaMatchRow : INotifyPropertyChanged
{
    private bool _apply;
    private string? _videoPath;
    private string? _pdfPath;

    public MediaMatch Match { get; }

    public string Haltungsname => Match.Haltungsname;

    public string VideoStatusText => Match.VideoStatus switch
    {
        MediaMatchStatus.Found => "Gefunden",
        MediaMatchStatus.Ambiguous => "Mehrdeutig",
        MediaMatchStatus.AlreadyLinked => "Verlinkt",
        _ => "Nicht gefunden"
    };

    public string PdfStatusText => Match.PdfStatus switch
    {
        MediaMatchStatus.Found => "Gefunden",
        MediaMatchStatus.Ambiguous => "Mehrdeutig",
        MediaMatchStatus.AlreadyLinked => "Verlinkt",
        _ => "Nicht gefunden"
    };

    public string FotoStatusText => Match.FotoStatus switch
    {
        MediaMatchStatus.Found => "Gefunden",
        _ => "Nicht gefunden"
    };

    public int FotoCount => Match.FotoPaths.Count;

    public string? VideoPath
    {
        get => _videoPath;
        set { _videoPath = value; OnPropertyChanged(); }
    }

    public string? PdfPath
    {
        get => _pdfPath;
        set { _pdfPath = value; OnPropertyChanged(); }
    }

    public bool Apply
    {
        get => _apply;
        set { _apply = value; OnPropertyChanged(); }
    }

    public MediaMatchRow(MediaMatch match)
    {
        Match = match;
        _apply = match.Apply;
        _videoPath = match.VideoPath;
        _pdfPath = match.PdfPath;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
