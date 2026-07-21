using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Reine Regeln fuer Excel-Zieldateien. Ordnererstellung und Dialoge bleiben beim Aufrufer.
/// </summary>
internal static class ExportExcelPathPolicy
{
    internal static string? BuildConfiguredPath(
        string? sharedRoot,
        DistributionTargetConfig cfg,
        IDistributionPatternResolver resolver,
        DateTime date,
        string? fallbackFilePattern = null,
        bool forceFallback = false)
    {
        if (string.IsNullOrWhiteSpace(sharedRoot))
            return null;

        var selectedPattern = forceFallback || string.IsNullOrWhiteSpace(cfg.DateiPattern)
            ? fallbackFilePattern
            : cfg.DateiPattern;
        if (string.IsNullOrWhiteSpace(selectedPattern))
            selectedPattern = "Export";

        var relativePath = resolver.ResolveRelativePath(
            ordnerPattern: null,
            unterordnerPattern: null,
            dateiPattern: selectedPattern,
            context: new DistributionPatternContext(date),
            extension: ".xlsx");
        return Path.Combine(sharedRoot, relativePath);
    }

    internal static string? BuildFixedPath(string? sharedRoot, string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(sharedRoot))
            return null;
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            throw new ArgumentException(
                "Der feste Excel-Dateiname darf nicht leer sein.",
                nameof(fileNameWithoutExtension));
        }

        var safeFileName = ProjectPathResolver.SanitizePathSegment(fileNameWithoutExtension.Trim());
        return Path.Combine(sharedRoot.Trim(), safeFileName + ".xlsx");
    }

    internal static string? BuildCollisionSafePath(
        string? sharedRoot,
        DistributionTargetConfig cfg,
        string fallbackFilePattern,
        DistributionTargetConfig otherCfg,
        string otherFallbackFilePattern,
        IDistributionPatternResolver resolver,
        DateTime date)
    {
        var target = BuildConfiguredPath(
            sharedRoot,
            cfg,
            resolver,
            date,
            fallbackFilePattern);
        if (target is null)
            return null;

        var other = BuildConfiguredPath(
            sharedRoot,
            otherCfg,
            resolver,
            date,
            otherFallbackFilePattern);
        return string.Equals(target, other, StringComparison.OrdinalIgnoreCase)
            ? BuildConfiguredPath(
                sharedRoot,
                cfg,
                resolver,
                date,
                fallbackFilePattern,
                forceFallback: true)
            : target;
    }
}
