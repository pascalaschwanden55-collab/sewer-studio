using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingProtocolPdfExportPlan(
    string DefaultFileName,
    string ProjectRoot,
    HaltungsprotokollPdfOptions Options);

public static class CodingProtocolPdfExportPlanner
{
    public static CodingProtocolPdfExportPlan Build(
        HaltungRecord record,
        string? lastProjectPath,
        string baseDirectory,
        DateTime now,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;

        var projectRoot = "";
        if (!string.IsNullOrWhiteSpace(lastProjectPath))
            projectRoot = ProjectFileLocator.ProjectRootFromFile(lastProjectPath)
                          ?? Path.GetDirectoryName(lastProjectPath)
                          ?? "";

        var logoPath = Path.Combine(baseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
        var options = new HaltungsprotokollPdfOptions
        {
            IncludePhotos = true,
            IncludeHaltungsgrafik = true,
            LogoPathAbs = fileExists(logoPath) ? logoPath : null
        };

        return new CodingProtocolPdfExportPlan(
            $"Protokoll_{record.GetFieldValue(FieldKeys.HoldingName) ?? "Haltung"}_{now:yyyyMMdd}.pdf",
            projectRoot,
            options);
    }
}
