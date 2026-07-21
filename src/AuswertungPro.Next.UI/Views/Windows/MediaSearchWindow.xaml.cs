using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class MediaSearchWindow : Window
{
    private readonly IReadOnlyList<HaltungRecord> _records;
    private readonly IDialogService _dialogs;
    private readonly AppSettings _settings;
    private readonly BatchMediaSearchService _mediaSearch;
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

    public MediaSearchWindow(IReadOnlyList<HaltungRecord> records, string? initialFolder, ServiceProvider services)
        : this(records, initialFolder, services.Dialogs, services.Settings, services.BatchMediaSearch)
    {
    }

    public MediaSearchWindow(
        IReadOnlyList<HaltungRecord> records,
        string? initialFolder,
        IDialogService dialogs,
        AppSettings settings,
        BatchMediaSearchService mediaSearch)
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        _records = records ?? throw new ArgumentNullException(nameof(records));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _mediaSearch = mediaSearch ?? throw new ArgumentNullException(nameof(mediaSearch));
        _initialFolder = initialFolder;

        // Hover-Foto-Vorschau: Treffer liefern absolute Foto-Pfade (kein Projekt-Root noetig).
        Behaviors.PhotoHoverPreviewBehavior.SetPhotoPathsSelector(
            ResultGrid, Behaviors.PhotoHoverPreviewSelectors.MediaMatchRowPhotos);

        if (!string.IsNullOrWhiteSpace(_initialFolder))
            FolderBox.Text = _initialFolder;

        Closed += (_, _) => _cts?.Cancel();
        Loaded += (_, _) => EnsureVisibleOnScreen();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var folder = _dialogs.SelectFolder("Medien-Suchordner waehlen", FolderBox.Text);
        if (!string.IsNullOrWhiteSpace(folder))
            FolderBox.Text = folder;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            _dialogs.Warn("Bitte einen gültigen Ordner wählen.", "Medien-Suche");
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
            var results = await Task.Run(() => _mediaSearch.Search(_records, options, progress, ct), ct);

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
            BestEffort.ReportWarning($"[Medien-Suche] Suche fehlgeschlagen: {ex}");
            _dialogs.Error("Die Medien-Suche ist fehlgeschlagen. Details stehen im Tageslog.", "Medien-Suche");
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

        var result = MediaSearchApplyController.Apply(_rows, _settings.LastProjectPath);
        AppliedVideoCount = result.VideoCount;
        AppliedPdfCount = result.PdfCount;
        AppliedFotoCount = result.FotoCount;
        Applied = result.Applied;

        // Persist last folder
        _settings.LastVideoSourceFolder = FolderBox.Text.Trim();
        _settings.Save();

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
