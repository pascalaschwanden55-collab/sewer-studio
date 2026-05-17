using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CancellationTokenSource? _codingAutoProtocolCts;
    private bool _codingAutoProtocolRunning;

    private async void CodingAutoProtocol_Click(object sender, RoutedEventArgs e)
    {
        if (_codingAutoProtocolRunning)
        {
            _codingAutoProtocolCts?.Cancel();
            _codingImportScanCts?.Cancel();
            _codingBulkScanCts?.Cancel();
            SetCodingAiState("KI-Protokoll wird abgebrochen", Color.FromRgb(0x94, 0xA3, 0xB8),
                "laufender Analyse-Schritt wird beendet...");
            return;
        }

        await RunCodingAutoProtocolAsync();
    }

    private async Task RunCodingAutoProtocolAsync()
    {
        if (_codingVm == null || _codingSessionService == null)
            return;

        _codingAutoProtocolRunning = true;
        _codingAutoProtocolCts?.Cancel();
        _codingAutoProtocolCts?.Dispose();
        _codingAutoProtocolCts = new CancellationTokenSource();

        var previousAutoContent = BtnCodingAutoProtocol.Content;
        var previousAnalyzeEnabled = BtnCodingAnalyze.IsEnabled;

        try
        {
            BtnCodingAutoProtocol.Content = "Abbrechen";
            BtnCodingAnalyze.IsEnabled = false;

            SetCodingAiState("KI-Protokoll erstellen", Color.FromRgb(0xF5, 0x9E, 0x0B),
                "Video wird vollstaendig analysiert", pulse: true);

            if (_codingImportEvents.Count > 0)
            {
                SetCodingAiState("KI-Protokoll erstellen", Color.FromRgb(0xF5, 0x9E, 0x0B),
                    "Importstellen werden zuerst geprueft", pulse: true);
                await RunCodingImportScanAsync().ConfigureAwait(true);
                _codingAutoProtocolCts.Token.ThrowIfCancellationRequested();
            }

            SetCodingAiState("KI-Protokoll erstellen", Color.FromRgb(0xF5, 0x9E, 0x0B),
                "Restliches Video wird gescannt", pulse: true);
            await RunCodingFullVideoScanAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            SetCodingAiState("KI-Protokoll abgebrochen", Color.FromRgb(0x94, 0xA3, 0xB8),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
        catch (Exception ex)
        {
            SetCodingAiState("KI-Protokoll Fehler", Color.FromRgb(0xEF, 0x44, 0x44),
                TrimStatus(ex.Message));
        }
        finally
        {
            _codingAutoProtocolRunning = false;
            BtnCodingAutoProtocol.Content = previousAutoContent;
            BtnCodingAnalyze.IsEnabled = previousAnalyzeEnabled;
        }
    }
}
