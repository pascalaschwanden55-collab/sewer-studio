using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class BeobachtungenWindow : Window
{
    private readonly ObservableCollection<ProtocolEntry> _entries;
    private readonly AppSettings _settings;
    private readonly BeobachtungenPhotoOpenController _photoOpenController;
    private readonly ICommand? _openProtocolCommand;
    private readonly object? _commandParameter;
    private Action? _vsaUpdateAction;
    private Action? _syncHoldingFieldsAction;

    public BeobachtungenWindow(
        ObservableCollection<ProtocolEntry> entries,
        ServiceProvider services,
        string? holdingName,
        ICommand? openProtocolCommand,
        object? commandParameter,
        Action? vsaUpdateAction = null,
        Action? syncHoldingFieldsAction = null)
        : this(
            entries,
            services?.Settings ?? throw new ArgumentNullException(nameof(services)),
            services.InspectionProtocolFiles,
            services.ShellOpen,
            holdingName,
            openProtocolCommand,
            commandParameter,
            vsaUpdateAction,
            syncHoldingFieldsAction)
    {
    }

    internal BeobachtungenWindow(
        ObservableCollection<ProtocolEntry> entries,
        AppSettings settings,
        IInspectionProtocolFileLocator inspectionProtocolFiles,
        ISafeShellOpenService shellOpen,
        string? holdingName,
        ICommand? openProtocolCommand,
        object? commandParameter,
        Action? vsaUpdateAction = null,
        Action? syncHoldingFieldsAction = null)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _entries = entries;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _photoOpenController = new BeobachtungenPhotoOpenController(
            inspectionProtocolFiles,
            shellOpen);
        _openProtocolCommand = openProtocolCommand;
        _commandParameter = commandParameter;
        _vsaUpdateAction = vsaUpdateAction;
        _syncHoldingFieldsAction = syncHoldingFieldsAction;

        EntriesGrid.ItemsSource = _entries;

        // Hover-Foto-Vorschau: Projekt-ROOT fuer relative FotoPaths (gleiche Aufloesung wie OpenPhotoLink_Click).
        Behaviors.PhotoHoverPreviewBehavior.SetProjectRootProvider(
            EntriesGrid,
            () => ProjectFileLocator.ProjectRootFromFile(_settings.LastProjectPath));

        if (!string.IsNullOrWhiteSpace(holdingName))
        {
            Title = $"Beobachtungen - {holdingName}";
            HeaderText.Text = $"Beobachtungen - {holdingName}";
        }

        ProtocolButton.Click += (_, _) =>
        {
            _openProtocolCommand?.Execute(_commandParameter);
            // Nach Schließen des Protokollfensters sofort Grid-Felder synchronisieren.
            _syncHoldingFieldsAction?.Invoke();
        };

        VsaUpdateButton.Click += (_, _) =>
        {
            _vsaUpdateAction?.Invoke();
        };

        SyncHoldingFieldsButton.Click += (_, _) =>
        {
            _syncHoldingFieldsAction?.Invoke();
        };
    }

    public void UpdateEntries(
        ObservableCollection<ProtocolEntry> entries,
        string? holdingName,
        Action? vsaUpdateAction = null,
        Action? syncHoldingFieldsAction = null)
    {
        EntriesGrid.ItemsSource = entries;
        _vsaUpdateAction = vsaUpdateAction;
        _syncHoldingFieldsAction = syncHoldingFieldsAction;
        if (!string.IsNullOrWhiteSpace(holdingName))
        {
            Title = $"Beobachtungen - {holdingName}";
            HeaderText.Text = $"Beobachtungen - {holdingName}";
        }
        else
        {
            Title = "Beobachtungen";
            HeaderText.Text = "Beobachtungen";
        }
    }

    private void OpenPhotoLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
            return;

        var rawPath = fe.Tag as string;
        var result = _photoOpenController.Open(rawPath, _settings.LastProjectPath);
        if (result.Status == BeobachtungenPhotoOpenStatus.NotFound)
        {
            DialogHost.Current.Info($"Foto nicht gefunden:\n{rawPath}", "Foto");
            return;
        }

        if (result.Status == BeobachtungenPhotoOpenStatus.OpenFailed)
        {
            DialogHost.Current.Error($"Foto konnte nicht geöffnet werden:\n{result.Error}", "Foto");
        }
    }

    private void OpenFilmLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
            return;

        var entry = fe.Tag as ProtocolEntry ?? fe.DataContext as ProtocolEntry;
        if (entry is null)
            return;

        var targetTime = entry.Zeit ?? ProtocolTimeParser.ParseMpegTime(entry.Mpeg);
        if (targetTime is null)
            return;

        PlayerWindow.TrySeekTo(targetTime.Value);
    }
}
