using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DiagnosticsPageViewModel : ObservableObject
{
    private readonly ServiceProvider _sp;

    [ObservableProperty] private string _logTail = "";

    public IRelayCommand RefreshCommand { get; }

    public DiagnosticsPageViewModel(ServiceProvider sp)
    {
        _sp = sp;
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

            var lines = File.ReadAllLines(logPath);
            LogTail = string.Join(Environment.NewLine, lines.TakeLast(200));
        }
        catch (Exception ex)
        {
            LogTail = ex.Message;
        }
    }
}
