using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class BeobachtungenWindow : Window
{
    private readonly ObservableCollection<ProtocolEntry> _entries;
    private readonly ICommand? _openProtocolCommand;
    private readonly object? _commandParameter;
    private Action? _vsaUpdateAction;
    private Action? _syncHoldingFieldsAction;

    public BeobachtungenWindow(
        ObservableCollection<ProtocolEntry> entries,
        string? holdingName,
        ICommand? openProtocolCommand,
        object? commandParameter,
        Action? vsaUpdateAction = null,
        Action? syncHoldingFieldsAction = null)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _entries = entries;
        _openProtocolCommand = openProtocolCommand;
        _commandParameter = commandParameter;
        _vsaUpdateAction = vsaUpdateAction;
        _syncHoldingFieldsAction = syncHoldingFieldsAction;

        EntriesGrid.ItemsSource = _entries;

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
        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        var sp = App.Services as ServiceProvider;
        var resolved = TryResolvePath(rawPath, sp?.Settings.LastProjectPath) ?? rawPath;
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
        {
            MessageBox.Show($"Foto nicht gefunden:\n{rawPath}", "Foto",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = resolved,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Foto konnte nicht geoeffnet werden:\n{ex.Message}", "Foto",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFilmLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
            return;

        var entry = fe.Tag as ProtocolEntry ?? fe.DataContext as ProtocolEntry;
        if (entry is null)
            return;

        var targetTime = entry.Zeit ?? ParseMpegTime(entry.Mpeg);
        if (targetTime is null)
            return;

        PlayerWindow.TrySeekTo(targetTime.Value);
    }

    private static string? TryResolvePath(string? raw, string? lastProjectPath)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (File.Exists(path))
            return path;
        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(lastProjectPath))
        {
            var baseDir = Path.GetDirectoryName(lastProjectPath);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                if (File.Exists(combined))
                    return combined;
            }
        }
        return null;
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
}
