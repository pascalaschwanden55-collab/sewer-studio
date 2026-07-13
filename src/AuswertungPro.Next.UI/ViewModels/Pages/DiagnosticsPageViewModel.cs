using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DiagnosticsPageViewModel : ObservableObject
{
    private readonly ILogTailReader _logTailReader;

    [ObservableProperty] private string _logTail = "";

    public IRelayCommand RefreshCommand { get; }

    public DiagnosticsPageViewModel(ILogTailReader logTailReader)
    {
        _logTailReader = logTailReader ?? throw new ArgumentNullException(nameof(logTailReader));
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            var result = _logTailReader.ReadToday(maximumLines: 200);
            if (!string.IsNullOrWhiteSpace(result.UserMessage))
            {
                LogTail = result.UserMessage;
                return;
            }

            if (!result.FileExists)
            {
                LogTail = "Noch keine Log-Datei vorhanden.";
                return;
            }

            LogTail = string.Join(Environment.NewLine, result.Lines);
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Diagnose] Log-Anzeige fehlgeschlagen: {ex}");
            LogTail = "Tageslog konnte nicht gelesen werden. Details stehen im Programmlog.";
        }
    }
}
