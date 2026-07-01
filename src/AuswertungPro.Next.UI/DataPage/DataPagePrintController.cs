using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPagePrintController
{
    private readonly IDialogService _dialogs;
    private readonly Func<string?> _getProjectFolder;
    private readonly Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> _buildAwuPdf;
    private readonly Func<HaltungRecord, HydraulikCalcResult?> _buildHydraulikCalculation;
    private readonly Func<HydraulikPrintOptions?> _selectHydraulikPrintOptions;
    private readonly Func<HaltungRecord, HydraulikCalcResult, HydraulikPrintOptions, Task<byte[]>> _buildHydraulikPdfAsync;
    private readonly string _baseDirectory;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string, byte[]> _writeAllBytes;
    private readonly Func<string, byte[], Task> _writeAllBytesAsync;
    private readonly Func<DateTime> _now;

    public DataPagePrintController(
        IDialogService dialogs,
        ProtocolPdfExporter protocolPdfExporter,
        Func<string?> getProjectFolder,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null)
        : this(
            dialogs,
            getProjectFolder,
            CreateBuildAwuPdf(protocolPdfExporter),
            AppContext.BaseDirectory,
            buildHydraulikCalculation: buildHydraulikCalculation)
    {
    }

    public DataPagePrintController(
        IDialogService dialogs,
        Func<string?> getProjectFolder,
        Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> buildAwuPdf,
        string baseDirectory,
        Func<string, bool>? fileExists = null,
        Action<string, byte[]>? writeAllBytes = null,
        Func<string, byte[], Task>? writeAllBytesAsync = null,
        Func<DateTime>? now = null,
        Func<HaltungRecord, HydraulikCalcResult?>? buildHydraulikCalculation = null,
        Func<HydraulikPrintOptions?>? selectHydraulikPrintOptions = null,
        Func<HaltungRecord, HydraulikCalcResult, HydraulikPrintOptions, Task<byte[]>>? buildHydraulikPdfAsync = null)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _getProjectFolder = getProjectFolder ?? throw new ArgumentNullException(nameof(getProjectFolder));
        _buildAwuPdf = buildAwuPdf ?? throw new ArgumentNullException(nameof(buildAwuPdf));
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory;
        _fileExists = fileExists ?? File.Exists;
        _writeAllBytes = writeAllBytes ?? File.WriteAllBytes;
        _writeAllBytesAsync = writeAllBytesAsync ?? ((path, bytes) => Task.Run(() => _writeAllBytes(path, bytes)));
        _now = now ?? (() => DateTime.Now);
        _buildHydraulikCalculation = buildHydraulikCalculation ?? (_ => null);
        _selectHydraulikPrintOptions = selectHydraulikPrintOptions ?? SelectHydraulikPrintOptionsWithDialog;
        _buildHydraulikPdfAsync = buildHydraulikPdfAsync
            ?? ((record, calc, options) => Task.Run(() => HydraulikPdfBuilder.Build(record, calc, options)));
    }

    public async Task PrintHydraulikPdfAsync(HaltungRecord? record)
    {
        if (record is null)
        {
            _dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Hydraulik PDF");
            return;
        }

        var calc = _buildHydraulikCalculation(record);
        if (calc is null)
        {
            _dialogs.Warn("Hydraulik-Berechnung konnte nicht durchgefuehrt werden.\nBitte DN und Gefaelle pruefen.", "Hydraulik PDF");
            return;
        }

        var selectedOptions = _selectHydraulikPrintOptions();
        if (selectedOptions is null)
            return;

        var holding = record.GetFieldValue("Haltungsname") ?? "Haltung";
        var defaultName = $"Hydraulik_{SanitizeFilenamePart(holding)}_{_now():yyyyMMdd}.pdf";
        var output = _dialogs.SaveFile(
            "Hydraulik-Bericht als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(_baseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = selectedOptions with
            {
                LogoPathAbs = _fileExists(logoPath) ? logoPath : null
            };

            var pdf = await _buildHydraulikPdfAsync(record, calc, options);
            await _writeAllBytesAsync(output, pdf);

            _dialogs.Info($"PDF wurde erstellt:\n{output}", "Hydraulik PDF");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"PDF konnte nicht erstellt werden:\n{ex.Message}", "Hydraulik PDF");
        }
    }

    public void PrintAwuHaltungsprotokollPdf(
        Project project,
        HaltungRecord? record,
        Func<HaltungRecord, ProtocolDocument> ensureProtocolDocument)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(ensureProtocolDocument);

        if (record is null)
        {
            _dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Haltungsprotokoll AWU");
            return;
        }

        var holding = record.GetFieldValue("Haltungsname");
        var defaultName = $"Haltungsprotokoll_AWU_{SanitizeFilenamePart(holding)}_{_now():yyyyMMdd}.pdf";
        var output = _dialogs.SaveFile(
            "Haltungsprotokoll AWU als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var doc = ensureProtocolDocument(record);
            var logoPath = Path.Combine(_baseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new HaltungsprotokollPdfOptions
            {
                LogoPathAbs = _fileExists(logoPath) ? logoPath : null
            };

            var projectFolder = _getProjectFolder() ?? string.Empty;
            var pdf = _buildAwuPdf(project, record, doc, projectFolder, options);

            _writeAllBytes(output, pdf);
            _dialogs.Info($"AWU-Haltungsprotokoll wurde erstellt:\n{output}", "Haltungsprotokoll AWU");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"AWU-Haltungsprotokoll konnte nicht erstellt werden:\n{ex.Message}", "Haltungsprotokoll AWU");
        }
    }

    private static Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> CreateBuildAwuPdf(
        ProtocolPdfExporter protocolPdfExporter)
    {
        ArgumentNullException.ThrowIfNull(protocolPdfExporter);

        return (project, record, document, projectRoot, options) =>
            protocolPdfExporter.BuildHaltungsprotokollPdf(project, record, document, projectRoot, options);
    }

    private static HydraulikPrintOptions? SelectHydraulikPrintOptionsWithDialog()
    {
        var dialog = new HydraulikPrintDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.SelectedOptions : null;
    }

    private static string SanitizeFilenamePart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "unknown";

        foreach (var c in Path.GetInvalidFileNameChars())
            text = text.Replace(c, '_');

        return text.Trim();
    }
}
