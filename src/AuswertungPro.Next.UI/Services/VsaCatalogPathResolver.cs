using System;
using System.Collections.Generic;
using System.Threading;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Protocol;

namespace AuswertungPro.Next.UI.Services;

public sealed record VsaCatalogPathResolution(
    string? SectionCatalogPath,
    string? NodeCatalogPath,
    string? KekManifestPath,
    IReadOnlyList<string> XmlCatalogPaths,
    IReadOnlyList<string> SourcePaths,
    string? DisplayPath);

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Datei- und Ordnersuche
/// liegt im injizierbaren <see cref="IVsaCatalogPathResolver"/>.
/// </summary>
public static class VsaCatalogPathResolver
{
    public const string SectionCatalogPathEnvVar =
        VsaCatalogPathNames.SectionCatalogPathEnvironmentVariable;
    public const string SectionCatalogRootEnvVar =
        VsaCatalogPathNames.SectionCatalogRootEnvironmentVariable;
    public const string NodeCatalogPathEnvVar =
        VsaCatalogPathNames.NodeCatalogPathEnvironmentVariable;
    public const string NodeCatalogRootEnvVar =
        VsaCatalogPathNames.NodeCatalogRootEnvironmentVariable;
    public const string KekManifestPathEnvVar =
        VsaCatalogPathNames.KekManifestPathEnvironmentVariable;
    public const string KekManifestFileName = VsaCatalogPathNames.KekManifestFileName;

    private static IVsaCatalogPathResolver _current = new VsaCatalogFilePathResolver();

    internal static IVsaCatalogPathResolver CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(IVsaCatalogPathResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Volatile.Write(ref _current, resolver);
    }

    public static VsaCatalogPathResolution Resolve(
        AppSettings settings,
        string? baseDirectory = null,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var result = CompatibilityService.Resolve(ToRequest(
            settings,
            baseDirectory,
            getEnvironmentVariable));
        return new VsaCatalogPathResolution(
            result.SectionCatalogPath,
            result.NodeCatalogPath,
            result.KekManifestPath,
            result.XmlCatalogPaths,
            result.SourcePaths,
            result.DisplayPath);
    }

    public static string? ResolveTextFallbackPath(AppSettings settings, string? resolvedXmlPath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return CompatibilityService.ResolveTextFallbackPath(
            settings.VsaCatalogSecXmlPath,
            resolvedXmlPath);
    }

    internal static VsaCatalogPathRequest ToRequest(
        AppSettings settings,
        string? baseDirectory = null,
        Func<string, string?>? getEnvironmentVariable = null)
        => new(
            settings.VsaCatalogSecXmlPath,
            settings.VsaCatalogNodXmlPath,
            settings.WinCanCatalogDirectory,
            settings.LastProjectPath,
            baseDirectory,
            getEnvironmentVariable);
}
