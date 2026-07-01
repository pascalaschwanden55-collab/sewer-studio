using System;
using System.IO;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPagePrintController
{
    private readonly IDialogService _dialogs;
    private readonly Func<string?> _getProjectFolder;
    private readonly Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> _buildAwuPdf;
    private readonly string _baseDirectory;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string, byte[]> _writeAllBytes;
    private readonly Func<DateTime> _now;

    public DataPagePrintController(
        IDialogService dialogs,
        ProtocolPdfExporter protocolPdfExporter,
        Func<string?> getProjectFolder)
        : this(
            dialogs,
            getProjectFolder,
            CreateBuildAwuPdf(protocolPdfExporter),
            AppContext.BaseDirectory)
    {
    }

    public DataPagePrintController(
        IDialogService dialogs,
        Func<string?> getProjectFolder,
        Func<Project, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> buildAwuPdf,
        string baseDirectory,
        Func<string, bool>? fileExists = null,
        Action<string, byte[]>? writeAllBytes = null,
        Func<DateTime>? now = null)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _getProjectFolder = getProjectFolder ?? throw new ArgumentNullException(nameof(getProjectFolder));
        _buildAwuPdf = buildAwuPdf ?? throw new ArgumentNullException(nameof(buildAwuPdf));
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory;
        _fileExists = fileExists ?? File.Exists;
        _writeAllBytes = writeAllBytes ?? File.WriteAllBytes;
        _now = now ?? (() => DateTime.Now);
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

    private static string SanitizeFilenamePart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "unknown";

        foreach (var c in Path.GetInvalidFileNameChars())
            text = text.Replace(c, '_');

        return text.Trim();
    }
}
