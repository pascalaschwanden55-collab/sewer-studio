using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Settings;

/// <summary>
/// Gemeinsamer Zustand eines laufenden PC-Ausfall-Schutzes. Die Instanz lebt im
/// ServiceProvider und bleibt deshalb auch bei einem Seitenwechsel erhalten.
/// </summary>
public sealed partial class FullBackupOperationState : ObservableObject
{
    private readonly object _sync = new();
    private CancellationTokenSource? _runCancellation;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private double _percent;
    [ObservableProperty] private string _currentFile = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _lastBackupInfo = "Noch keine Datensicherung erstellt.";

    public bool TryBegin(CancellationToken commandToken, out CancellationToken runToken)
    {
        lock (_sync)
        {
            if (_runCancellation is not null)
            {
                runToken = default;
                return false;
            }

            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(commandToken);
            runToken = _runCancellation.Token;
        }

        Percent = 0;
        CurrentFile = string.Empty;
        StatusText = "Berechne Groessen...";
        IsRunning = true;
        return true;
    }

    public void UpdateProgress(double percent, string? currentFile, string? statusText)
    {
        Percent = Math.Clamp(percent, 0, 100);
        CurrentFile = currentFile ?? string.Empty;
        StatusText = statusText ?? string.Empty;
    }

    public void SetStatus(string? statusText)
        => StatusText = statusText ?? string.Empty;

    public void SetLastBackupInfo(string? lastBackupInfo)
        => LastBackupInfo = lastBackupInfo ?? string.Empty;

    public void Cancel()
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
            cancellation = _runCancellation;

        if (cancellation is null)
            return;

        StatusText = "Abbruch wird ausgefuehrt...";
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Die Sicherung wurde genau zwischen Knopfdruck und Abbruch beendet.
        }
    }

    public void Finish()
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            cancellation = _runCancellation;
            _runCancellation = null;
        }

        CurrentFile = string.Empty;
        IsRunning = false;
        cancellation?.Dispose();
    }
}
