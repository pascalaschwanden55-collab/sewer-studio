using System;
using System.Globalization;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Vsa;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class VsaPageViewModel : ObservableObject
{
    private readonly Func<Project> _getProject;
    private readonly object _collectionLock;
    private readonly Func<string?> _getProjectPath;
    private readonly Func<string?> _getExplicitPdfToTextPath;
    private readonly IStoredImportFilePathResolver _storedImportFilePaths;
    private readonly IXtfImportService _xtfImport;
    private readonly IPdfImportService _pdfImport;
    private readonly IVsaEvaluationService _vsaEvaluation;
    private readonly IMeasureRecommendationService _measureRecommendation;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _createImportRestorePoint;
    private readonly Action _refreshTitleAndDirty;

    [ObservableProperty] private string _summary = "Noch keine Berechnung.";

    /// <summary>S10: True waehrend die (synchrone) Bewertung im Hintergrund laeuft.</summary>
    [ObservableProperty] private bool _isBusy;

    public IAsyncRelayCommand RunCommand { get; }

    public VsaPageViewModel(ShellViewModel shell, ServiceProvider sp)
        : this(
            getProject: () => shell.Project,
            collectionLock: shell.CollectionLock,
            getProjectPath: () => sp.Settings.LastProjectPath,
            getExplicitPdfToTextPath: () => sp.Diagnostics.ExplicitPdfToTextPath,
            storedImportFilePaths: sp.StoredImportFilePaths,
            xtfImport: sp.XtfImport,
            pdfImport: sp.PdfImport,
            vsaEvaluation: sp.Vsa,
            measureRecommendation: sp.MeasureRecommendation,
            setStatus: shell.SetStatus,
            createImportRestorePoint: shell.TryCreateImportRestorePoint,
            refreshTitleAndDirty: shell.RefreshTitleAndDirty)
    {
    }

    public VsaPageViewModel(
        Func<Project> getProject,
        object collectionLock,
        Func<string?> getProjectPath,
        Func<string?> getExplicitPdfToTextPath,
        IStoredImportFilePathResolver storedImportFilePaths,
        IXtfImportService xtfImport,
        IPdfImportService pdfImport,
        IVsaEvaluationService vsaEvaluation,
        IMeasureRecommendationService measureRecommendation,
        Action<string> setStatus,
        Action<string> createImportRestorePoint,
        Action refreshTitleAndDirty)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _collectionLock = collectionLock ?? throw new ArgumentNullException(nameof(collectionLock));
        _getProjectPath = getProjectPath ?? throw new ArgumentNullException(nameof(getProjectPath));
        _getExplicitPdfToTextPath = getExplicitPdfToTextPath ?? throw new ArgumentNullException(nameof(getExplicitPdfToTextPath));
        _storedImportFilePaths = storedImportFilePaths ?? throw new ArgumentNullException(nameof(storedImportFilePaths));
        _xtfImport = xtfImport ?? throw new ArgumentNullException(nameof(xtfImport));
        _pdfImport = pdfImport ?? throw new ArgumentNullException(nameof(pdfImport));
        _vsaEvaluation = vsaEvaluation ?? throw new ArgumentNullException(nameof(vsaEvaluation));
        _measureRecommendation = measureRecommendation ?? throw new ArgumentNullException(nameof(measureRecommendation));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _createImportRestorePoint = createImportRestorePoint ?? throw new ArgumentNullException(nameof(createImportRestorePoint));
        _refreshTitleAndDirty = refreshTitleAndDirty ?? throw new ArgumentNullException(nameof(refreshTitleAndDirty));
        // S10: AsyncRelayCommand sperrt Mehrfachstarts automatisch und verlagert die
        // Bewertung in den Hintergrund, damit die App nicht einfriert.
        RunCommand = new AsyncRelayCommand(RunAsync);
    }

    private async System.Threading.Tasks.Task RunAsync()
    {
        // AsyncRelayCommand sperrt Mehrfachstarts bereits selbst (CanExecute waehrend des Laufs).
        IsBusy = true;
        Summary = "VSA-Bewertung laeuft, bitte warten...";
        _setStatus("VSA-Bewertung läuft...");
        try
        {
            // Run() liest gespeicherte XTF-/PDF-Quellen erneut ein und kann das Projekt veraendern.
            // Deshalb gilt hier dasselbe Sicherheitsnetz wie bei den sichtbaren Importknoepfen.
            if (HasStoredImportSources())
                _createImportRestorePoint("VSA-Daten");

            // Import/Bewertung sind synchron und potenziell langlaufend -> in den Hintergrund.
            // Run() mutiert Project.Data (ObservableCollection). Der Schreiber MUSS denselben Lock
            // halten, den EnableCollectionSynchronization fuer die UI-Lesezugriffe nutzt, sonst
            // entstehen Cross-Thread-Collection-Fehler. Skalare Property-/Item-PropertyChanged
            // marshallt WPFs Binding-Engine selbst auf den UI-Thread.
            await System.Threading.Tasks.Task.Run(() =>
            {
                lock (_collectionLock)
                {
                    Run();
                }
            });
        }
        catch (Exception ex)
        {
            Summary = $"Fehler: {UserError.DescribeAndReport(ex, "VSA-Bewertung")}";
            _setStatus("VSA fehlgeschlagen");
        }
        finally
        {
            IsBusy = false;
            _refreshTitleAndDirty(); // SuggestMeasuresForAll kann Project.Dirty gesetzt haben
        }
    }

    private bool HasStoredImportSources()
        => HasStoredImportSource("XTF_StoredFiles") || HasStoredImportSource("PDF_StoredFiles");

    private bool HasStoredImportSource(string key)
        => _getProject().Metadata.TryGetValue(key, out var value)
           && !string.IsNullOrWhiteSpace(value);

    private void Run()
    {
        var project = _getProject();
        var projectPath = _getProjectPath();
        // Import-Reihenfolge: XTF/M150/MDB primaer, PDF sekundaer
        var xtfFiles = _storedImportFilePaths.ResolveExistingFiles(
            project.Metadata,
            "XTF_StoredFiles",
            projectPath);
        var importSb = new StringBuilder();
        importSb.AppendLine($"Import-Quellen: XTF/M150/MDB={xtfFiles.Count}");

        var xtfFound = 0;
        var xtfCreated = 0;
        var xtfUpdated = 0;
        var xtfUncertain = 0;
        var xtfErrors = 0;

        if (xtfFiles.Count > 0)
        {
            var resImport = _xtfImport.ImportXtfFiles(xtfFiles, project);
            if (!resImport.Ok || resImport.Value is null)
            {
                Summary = $"Fehler: {resImport.ErrorMessage}";
                _setStatus("VSA fehlgeschlagen");
                return;
            }

            xtfFound += resImport.Value.Found;
            xtfCreated += resImport.Value.Created;
            xtfUpdated += resImport.Value.Updated;
            xtfUncertain += resImport.Value.Uncertain;
            xtfErrors += resImport.Value.Errors;
        }

        var pdfFiles = _storedImportFilePaths.ResolveExistingFiles(
            project.Metadata,
            "PDF_StoredFiles",
            projectPath);
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
                var resPdf = _pdfImport.ImportPdf(pdf, project, _getExplicitPdfToTextPath(), fillMissingOnly: true);
                if (!resPdf.Ok || resPdf.Value is null)
                {
                    Summary = $"Fehler: {resPdf.ErrorMessage}";
                    _setStatus("VSA fehlgeschlagen");
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

        var res = _vsaEvaluation.Evaluate(project);
        if (!res.Ok || res.Value is null)
        {
            Summary = importSb.ToString() + $"\nFehler: {res.ErrorMessage}";
            _setStatus("VSA fehlgeschlagen");
            return;
        }

        // Summarize
        var count = project.Data.Count;
        var avgD = project.Data
            .Select(r => double.TryParse(r.GetFieldValue("VSA_Zustandsnote_D").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (double?)null)
            .Where(d => d is not null).Select(d => d!.Value).DefaultIfEmpty(4.0).Average();

        // Nach VSA-Bewertung: automatisch Sanierungsmassnahmen fuer betroffene Haltungen vorschlagen
        var measureResult = SuggestMeasuresForAll();

        var diag = project.Metadata.TryGetValue("VSA_Diag", out var d) ? d : "";
        var measureInfo = measureResult.Filled > 0
            ? $"\nSanierungsmassnahmen: {measureResult.Filled} Haltungen befuellt, {measureResult.Skipped} uebersprungen."
            : "";
        Summary = importSb.ToString() +
                  $"\nBerechnet für {count} Records. Ø Zustandsnote D: {avgD:0.00}.\n" +
                  (string.IsNullOrWhiteSpace(diag) ? "" : (diag + "\n")) +
                  measureInfo +
                  "\nHinweis: Klassifizierungstabellen sind im Skeleton nur beispielhaft.";
        _setStatus("VSA berechnet" + (measureResult.Filled > 0 ? $" + {measureResult.Filled} Maßnahmen" : ""));
    }

    private record struct MeasureBatchResult(int Filled, int Skipped, int NoSuggestion);

    private MeasureBatchResult SuggestMeasuresForAll()
    {
        var project = _getProject();
        var filled = 0;
        var skipped = 0;
        var noSuggestion = 0;

        foreach (var record in project.Data)
        {
            var pruefung = (record.GetFieldValue("Pruefungsresultat") ?? "").Trim();
            var existing = (record.GetFieldValue("Empfohlene_Sanierungsmassnahmen") ?? "").Trim();
            var hasDamage = record.VsaFindings is not null && record.VsaFindings.Count > 0
                || !string.IsNullOrWhiteSpace(record.GetFieldValue("Primaere_Schaeden"));

            // Manuell bearbeitete Massnahmen nicht ueberschreiben
            if (!string.IsNullOrWhiteSpace(existing))
            {
                var meta = record.FieldMeta.GetValueOrDefault("Empfohlene_Sanierungsmassnahmen");
                if (meta is not null && meta.UserEdited)
                {
                    skipped++;
                    continue;
                }
            }

            // Nur Records mit Sanierungsbedarf/beobachten oder Schadenscodes
            if (!string.Equals(pruefung, "Sanierungsbedarf", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pruefung, "beobachten", StringComparison.OrdinalIgnoreCase)
                && !hasDamage)
            {
                skipped++;
                continue;
            }

            var rec = _measureRecommendation.Recommend(record, maxSuggestions: 5);
            if (rec.Measures.Count == 0)
            {
                noSuggestion++;
                continue;
            }

            var value = string.Join(Environment.NewLine, rec.Measures);
            record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", value, FieldSource.Unknown, userEdited: false);

            if (rec.EstimatedTotalCost is not null)
                record.SetFieldValue("Kosten", rec.EstimatedTotalCost.Value.ToString("0.00", CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
            if (rec.RenovierungInlinerM is not null)
                record.SetFieldValue("Renovierung_Inliner_m", rec.RenovierungInlinerM.Value.ToString("0.00", CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
            if (rec.RenovierungInlinerStk is not null)
                record.SetFieldValue("Renovierung_Inliner_Stk", rec.RenovierungInlinerStk.Value.ToString(CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
            if (rec.AnschluesseVerpressen is not null)
                record.SetFieldValue("Anschluesse_verpressen", rec.AnschluesseVerpressen.Value.ToString(CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
            if (rec.ReparaturManschette is not null)
                record.SetFieldValue("Reparatur_Manschette", rec.ReparaturManschette.Value.ToString(CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
            if (rec.ReparaturKurzliner is not null)
                record.SetFieldValue("Reparatur_Kurzliner", rec.ReparaturKurzliner.Value.ToString(CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);

            filled++;
        }

        if (filled > 0)
        {
            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;
        }

        return new MeasureBatchResult(filled, skipped, noSuggestion);
    }
}

