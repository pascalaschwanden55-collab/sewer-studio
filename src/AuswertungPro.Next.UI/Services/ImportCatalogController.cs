using AuswertungPro.Next.Application.Protocol;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportCatalogStatus(string Text, bool IsOk);

internal sealed record ImportCatalogReloadResult(
    ImportCatalogStatus Status,
    string? UserError);

/// <summary>Ermittelt den VSA-Katalogstatus und lädt unterstützte Kataloge neu.</summary>
internal sealed class ImportCatalogController
{
    private readonly Func<string?> _getConfiguredSecPath;
    private readonly Func<string?> _getConfiguredNodPath;
    private readonly Func<string?> _getResolvedPath;
    private readonly ICodeCatalogProvider _catalog;
    private readonly ILogger _logger;

    public ImportCatalogController(
        Func<string?> getConfiguredSecPath,
        Func<string?> getConfiguredNodPath,
        Func<string?> getResolvedPath,
        ICodeCatalogProvider catalog,
        ILogger logger)
    {
        _getConfiguredSecPath = getConfiguredSecPath
            ?? throw new ArgumentNullException(nameof(getConfiguredSecPath));
        _getConfiguredNodPath = getConfiguredNodPath
            ?? throw new ArgumentNullException(nameof(getConfiguredNodPath));
        _getResolvedPath = getResolvedPath
            ?? throw new ArgumentNullException(nameof(getResolvedPath));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ImportCatalogStatus GetStatus()
    {
        var resolved = _getResolvedPath();
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            var label = resolved.Contains(" | ", StringComparison.Ordinal)
                ? "SEC+NOD"
                : resolved.Contains("_NOD", StringComparison.OrdinalIgnoreCase)
                    ? "NOD"
                    : "SEC";
            return new ImportCatalogStatus(
                $"VSA-2019-Katalog ({label}): {resolved}",
                IsOk: true);
        }

        var configuredNod = _getConfiguredNodPath();
        if (!string.IsNullOrWhiteSpace(configuredNod))
        {
            return new ImportCatalogStatus(
                $"VSA-Katalog (NOD): {configuredNod} (nicht gefunden)",
                IsOk: false);
        }

        var configuredSec = _getConfiguredSecPath();
        if (!string.IsNullOrWhiteSpace(configuredSec))
        {
            return new ImportCatalogStatus(
                $"VSA-Katalog (SEC): {configuredSec} (nicht gefunden)",
                IsOk: false);
        }

        return new ImportCatalogStatus(
            "VSA-Katalog (SEC/NOD): nicht konfiguriert",
            IsOk: false);
    }

    public ImportCatalogReloadResult Reload()
    {
        string? userError = null;
        try
        {
            switch (_catalog)
            {
                case XmlCodeCatalogProvider xml:
                    xml.Reload();
                    break;
                case JsonCodeCatalogProvider json:
                    json.Reload();
                    break;
                case CompositeCodeCatalogProvider composite:
                    composite.Reload();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VSA-Katalog konnte nicht neu geladen werden.");
            userError = "Der VSA-Katalog konnte nicht neu geladen werden. Technische Details stehen im Tageslog.";
        }

        return new ImportCatalogReloadResult(GetStatus(), userError);
    }
}
