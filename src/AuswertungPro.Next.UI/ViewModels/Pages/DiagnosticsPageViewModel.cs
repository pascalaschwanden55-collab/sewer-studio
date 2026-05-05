using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DiagnosticsPageViewModel : ObservableObject
{
    [ObservableProperty] private string _logTail = "";

    public IRelayCommand RefreshCommand { get; }

    // Phase 5.1.B Etappe 4 Sub-A: Bundle-ctor entfernt — _sp war tot.
    public DiagnosticsPageViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            var logDir = Path.Combine(AppSettings.AppDataDir, "logs");
            var logPath = Path.Combine(logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
            if (!File.Exists(logPath))
            {
                LogTail = "Noch keine Log-Datei vorhanden.";
                return;
            }

            LogTail = string.Join(Environment.NewLine, File.ReadLines(logPath).TakeLast(200));
        }
        catch (Exception ex)
        {
            LogTail = ex.Message;
        }
    }
}
