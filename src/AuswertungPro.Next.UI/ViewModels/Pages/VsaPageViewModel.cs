using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class VsaPageViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;

    [ObservableProperty] private string _summary = "Noch keine Berechnung.";

    public IRelayCommand RunCommand { get; }

    public VsaPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell;
        _sp = sp;
        RunCommand = new RelayCommand(Run);
    }

    private void Run()
    {
        // Import-Reihenfolge: XTF/M150/MDB primaer, PDF sekundaer
        var xtfFiles = LoadStoredXtfFiles(_sp.Settings.LastProjectPath);
        var importSb = new StringBuilder();
        importSb.AppendLine($"Import-Quellen: XTF/M150/MDB={xtfFiles.Count}");

        var xtfFound = 0;
        var xtfCreated = 0;
        var xtfUpdated = 0;
        var xtfUncertain = 0;
        var xtfErrors = 0;

        if (xtfFiles.Count > 0)
        {
            var resImport = _sp.XtfImport.ImportXtfFiles(xtfFiles, _shell.Project);
            if (!resImport.Ok || resImport.Value is null)
            {
                Summary = $"Fehler: {resImport.ErrorMessage}";
                _shell.SetStatus("VSA fehlgeschlagen");
                return;
            }

            xtfFound += resImport.Value.Found;
            xtfCreated += resImport.Value.Created;
            xtfUpdated += resImport.Value.Updated;
            xtfUncertain += resImport.Value.Uncertain;
            xtfErrors += resImport.Value.Errors;
        }

        var pdfFiles = LoadStoredPdfFiles(_sp.Settings.LastProjectPath);
        importSb.AppendLine($"Import-Quellen: PDF={pdfFiles.Count}");

        var pdfFound = 0;
        var pdfCreated = 0;
        var pdfUpdated = 0;
        var pdfUncertain = 0;
        var pdfErrors = 0;

        if (pdfFiles.Count > 0)
        {
            foreach (var pdf in pdfFiles)
            {
                var resPdf = _sp.PdfImport.ImportPdf(pdf, _shell.Project, _sp.Diagnostics.ExplicitPdfToTextPath);
                if (!resPdf.Ok || resPdf.Value is null)
                {
                    Summary = $"Fehler: {resPdf.ErrorMessage}";
                    _shell.SetStatus("VSA fehlgeschlagen");
                    return;
                }

                pdfFound += resPdf.Value.Found;
                pdfCreated += resPdf.Value.Created;
                pdfUpdated += resPdf.Value.Updated;
                pdfUncertain += resPdf.Value.Uncertain;
                pdfErrors += resPdf.Value.Errors;
            }
        }

        if (xtfFiles.Count > 0)
            importSb.AppendLine($"Daten Stats (XTF/M150/MDB): Found={xtfFound}, Created={xtfCreated}, Updated={xtfUpdated}, Uncertain={xtfUncertain}, Errors={xtfErrors}");
        if (pdfFiles.Count > 0)
            importSb.AppendLine($"PDF Stats: Found={pdfFound}, Created={pdfCreated}, Updated={pdfUpdated}, Uncertain={pdfUncertain}, Errors={pdfErrors}");

        var res = _sp.Vsa.Evaluate(_shell.Project);
        if (!res.Ok || res.Value is null)
        {
            Summary = importSb.ToString() + $"\nFehler: {res.ErrorMessage}";
            _shell.SetStatus("VSA fehlgeschlagen");
            return;
        }

        // Summarize
        var count = _shell.Project.Data.Count;
        var avgD = _shell.Project.Data
            .Select(r => double.TryParse(r.GetFieldValue("VSA_Zustandsnote_D").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (double?)null)
            .Where(d => d is not null).Select(d => d!.Value).DefaultIfEmpty(4.0).Average();

        var diag = _shell.Project.Metadata.TryGetValue("VSA_Diag", out var d) ? d : "";
        Summary = importSb.ToString() +
                  $"\nBerechnet für {count} Records. Ø Zustandsnote D: {avgD:0.00}.\n" +
                  (string.IsNullOrWhiteSpace(diag) ? "" : (diag + "\n")) +
                  "Hinweis: Klassifizierungstabellen sind im Skeleton nur beispielhaft.";
        _shell.SetStatus("VSA berechnet");
    }

    private List<string> LoadStoredXtfFiles(string? projectPath)
    {
        if (!_shell.Project.Metadata.TryGetValue("XTF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        List<string>? list = null;
        try
        {
            list = JsonSerializer.Deserialize<List<string>>(raw);
        }
        catch
        {
            // ignore
        }

        list ??= raw.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        var projectDir = string.IsNullOrWhiteSpace(projectPath) ? "" : (Path.GetDirectoryName(projectPath) ?? "");
        var resolved = new List<string>();
        foreach (var p in list)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            var full = Path.IsPathRooted(p) ? p : (string.IsNullOrWhiteSpace(projectDir) ? p : Path.GetFullPath(Path.Combine(projectDir, p)));
            if (File.Exists(full))
                resolved.Add(full);
        }
        return resolved;
    }

    private List<string> LoadStoredPdfFiles(string? projectPath)
    {
        if (!_shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        List<string>? list = null;
        try
        {
            list = JsonSerializer.Deserialize<List<string>>(raw);
        }
        catch
        {
            // ignore
        }

        list ??= raw.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        var projectDir = string.IsNullOrWhiteSpace(projectPath) ? "" : (Path.GetDirectoryName(projectPath) ?? "");
        var resolved = new List<string>();
        foreach (var p in list)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            var full = Path.IsPathRooted(p) ? p : (string.IsNullOrWhiteSpace(projectDir) ? p : Path.GetFullPath(Path.Combine(projectDir, p)));
            if (File.Exists(full))
                resolved.Add(full);
        }
        return resolved;
    }
}

