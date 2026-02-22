using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ExportPageViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;

    [ObservableProperty] private string _lastResult = "";

    public IRelayCommand ExportCommand { get; }
    public IRelayCommand ExportSchaechteCommand { get; }

    public ExportPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell;
        _sp = sp;
        ExportCommand = new RelayCommand(Export);
        ExportSchaechteCommand = new RelayCommand(ExportSchaechte);
    }

    private void Export()
    {
        var outPath = _sp.Dialogs.SaveFile("Export (Haltungen.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
        if (outPath is null) return;

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Haltungen.xlsx");
        var res = _sp.ExcelExport.ExportToTemplate(_shell.Project, templatePath, outPath, headerRow: 11, startRow: 12);
        LastResult = res.Ok ? $"Exportiert: {outPath}" : $"Fehler: {res.ErrorMessage}";
        _shell.SetStatus(res.Ok ? "Exportiert" : "Export fehlgeschlagen");
    }

    private void ExportSchaechte()
    {
        var outPath = _sp.Dialogs.SaveFile("Export (Schaechte.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
        if (outPath is null) return;

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Schächte.xlsx");
        if (!File.Exists(templatePath))
        {
            LastResult = $"Fehler: Vorlage nicht gefunden ({templatePath})";
            _shell.SetStatus("Export fehlgeschlagen");
            return;
        }

        var res = _sp.ExcelExport.ExportSchaechteToTemplate(_shell.Project, templatePath, outPath, headerRow: 12, startRow: 13);
        LastResult = res.Ok ? $"Exportiert: {outPath}" : $"Fehler: {res.ErrorMessage}";
        _shell.SetStatus(res.Ok ? "Exportiert" : "Export fehlgeschlagen");
    }
}
