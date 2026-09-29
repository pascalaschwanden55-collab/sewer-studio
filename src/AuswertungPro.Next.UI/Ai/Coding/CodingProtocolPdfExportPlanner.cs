using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

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
        Func<string, bool>? fileExists = null,
        // Gemeinsame Quelle (Optikanalyse 28.09.2026, Aufgabe 15): wird sie gereicht,
        // gilt sie statt der lokalen baseDirectory/fileExists-Berechnung darunter.
        Func<string?>? resolveLogoPath = null)
    {
        fileExists ??= File.Exists;

        var projectRoot = "";
        if (!string.IsNullOrWhiteSpace(lastProjectPath))
            projectRoot = ProjectFileLocator.ProjectRootFromFile(lastProjectPath)
                          ?? "";

        string? logo;
        if (resolveLogoPath is not null)
        {
            logo = resolveLogoPath();
        }
        else
        {
            // Gemeinsame Quelle nur fuer den Standardpfad selbst (Optikanalyse 28.09.2026,
            // Aufgabe 15) - UI/Ai darf keinen Service-Locator verwenden
            // (UiArchitectureGuardTests), deshalb liest dieser Zweig die Einstellung nicht.
            var logoPath = BerichtsLogoResolver.DefaultLogoPath(baseDirectory);
            logo = fileExists(logoPath) ? logoPath : null;
        }

        var options = new HaltungsprotokollPdfOptions
        {
            IncludePhotos = true,
            IncludeHaltungsgrafik = true,
            LogoPathAbs = logo
        };

        return new CodingProtocolPdfExportPlan(
            $"Protokoll_{record.GetFieldValue(FieldKeys.HoldingName) ?? "Haltung"}_{now:yyyyMMdd}.pdf",
            projectRoot,
            options);
    }
}
