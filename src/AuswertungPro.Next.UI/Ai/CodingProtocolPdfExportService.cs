using System;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingProtocolPdfExportService
{
    private readonly Func<int, bool> _confirmPdfExport;
    private readonly Func<HaltungRecord, string?, string, DateTime, CodingProtocolPdfExportPlan> _buildPlan;
    private readonly Func<string, string?> _chooseOutputPath;
    private readonly Func<Project?> _getCurrentProject;
    private readonly Func<Project?, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> _buildPdf;
    private readonly Action<string, byte[]> _saveAndOpen;
    private readonly Action<string> _showPdfExportFailed;
    private readonly Func<DateTime> _now;
    private readonly Func<string> _baseDirectory;

    public CodingProtocolPdfExportService(
        Func<int, bool> confirmPdfExport,
        Func<HaltungRecord, string?, string, DateTime, CodingProtocolPdfExportPlan> buildPlan,
        Func<string, string?> chooseOutputPath,
        Func<Project?> getCurrentProject,
        Func<Project?, HaltungRecord, ProtocolDocument, string, HaltungsprotokollPdfOptions, byte[]> buildPdf,
        Action<string, byte[]> saveAndOpen,
        Action<string> showPdfExportFailed,
        Func<DateTime> now,
        Func<string> baseDirectory)
    {
        _confirmPdfExport = confirmPdfExport ?? throw new ArgumentNullException(nameof(confirmPdfExport));
        _buildPlan = buildPlan ?? throw new ArgumentNullException(nameof(buildPlan));
        _chooseOutputPath = chooseOutputPath ?? throw new ArgumentNullException(nameof(chooseOutputPath));
        _getCurrentProject = getCurrentProject ?? throw new ArgumentNullException(nameof(getCurrentProject));
        _buildPdf = buildPdf ?? throw new ArgumentNullException(nameof(buildPdf));
        _saveAndOpen = saveAndOpen ?? throw new ArgumentNullException(nameof(saveAndOpen));
        _showPdfExportFailed = showPdfExportFailed ?? throw new ArgumentNullException(nameof(showPdfExportFailed));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
    }

    public bool TryOfferPdfExport(HaltungRecord record, ProtocolDocument doc, string? lastProjectPath)
    {
        if (!_confirmPdfExport(doc.Current.Entries.Count))
            return false;

        try
        {
            var plan = _buildPlan(record, lastProjectPath, _baseDirectory(), _now());

            var outputPath = _chooseOutputPath(plan.DefaultFileName);
            if (outputPath == null)
                return false;

            var project = _getCurrentProject();
            var pdf = _buildPdf(project, record, doc, plan.ProjectRoot, plan.Options);
            _saveAndOpen(outputPath, pdf);
            return true;
        }
        catch (Exception ex)
        {
            _showPdfExportFailed(ex.Message);
            return false;
        }
    }
}
