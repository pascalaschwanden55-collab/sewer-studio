using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed record CostCatalogNpkDuplicateWarning(
    string NpkCode,
    IReadOnlyList<string> Units,
    IReadOnlyList<string> ItemKeys);

public sealed class CostCatalogStore : ICostCatalogStore
{
    private readonly string? _userOverridePath;
    private string? _lastMergedLoadError;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CostCatalogStore(string? userOverridePath = null)
    {
        _userOverridePath = userOverridePath;
    }

    public string? LastUserOverrideLoadError { get; private set; }

    public CostCatalog LoadMerged(string? projectPath)
        => LoadMerged(projectPath, out _);

    /// <summary>
    /// Wie LoadMerged, meldet aber beschaedigte/unlesbare Katalogdateien ueber loadError
    /// (fehlende Dateien sind kein Fehler). Bisher ging ein defekter Default-Katalog still
    /// in einen leeren Katalog ueber — Berichte wirkten vollstaendig, hatten aber keine
    /// Katalogpreise/NPK-Zuordnung mehr. Gleiches Muster wie IProjectCostStoreRepository.Load.
    /// </summary>
    public CostCatalog LoadMerged(string? projectPath, out string? loadError)
    {
        var defaults = ReadCatalog(ResolvePath(projectPath, "cost_catalog.json"), out var defaultError);
        var overrides = LoadUserOverrides();
        loadError = CombineLoadErrors(defaultError, LastUserOverrideLoadError);
        _lastMergedLoadError = loadError;
        return Merge(defaults, overrides);
    }

    private static string? CombineLoadErrors(string? defaultError, string? userOverrideError)
    {
        var userText = string.IsNullOrWhiteSpace(userOverrideError)
            ? null
            : $"Benutzer-Katalog (Overrides): {userOverrideError}";
        if (string.IsNullOrWhiteSpace(defaultError))
            return userText;
        return userText is null ? defaultError : $"{defaultError}\n{userText}";
    }

    public CostCatalog LoadDefault(string? projectPath)
    {
        var path = ResolvePath(projectPath, "cost_catalog.json");
        return ReadCatalog(path);
    }

    public CostCatalog LoadUserOverrides()
    {
        LastUserOverrideLoadError = null;
        var path = ResolveUserOverridePath();
        return ReadCatalog(path, rememberUserOverrideError: true);
    }

    public bool SaveUserOverrides(CostCatalog catalog, out string error)
    {
        error = "";
        if (catalog is null)
        {
            error = "Kostenkatalog fehlt; Speichern ist gesperrt.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_lastMergedLoadError))
        {
            error = $"Katalogdatei konnte nicht geladen werden; Speichern ist gesperrt: {_lastMergedLoadError}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(LastUserOverrideLoadError))
        {
            error = $"User-Override konnte nicht geladen werden; Speichern ist gesperrt: {LastUserOverrideLoadError}";
            return false;
        }

        try
        {
            ValidateCatalogStructure(catalog);
            var path = ResolveUserOverridePath();
            _ = ReadCatalog(path, out var existingLoadError, rememberUserOverrideError: true);
            if (!string.IsNullOrWhiteSpace(existingLoadError))
            {
                error = $"Vorhandener User-Override konnte nicht geladen werden; Speichern ist gesperrt: {existingLoadError}";
                return false;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(catalog, Application.Common.JsonDefaults.Indented);
            AtomicJsonFileWriter.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Wie <see cref="SaveUserOverrides(CostCatalog, out string)"/>, aber NPK-Metadaten,
    /// die unveraendert dem Default entsprechen, werden NICHT in den Override geschrieben
    /// (leer gelassen; der Merge fuellt sie via <see cref="PreserveNpkMetadata"/> wieder
    /// aus dem Default). Sonst friert der Override den heutigen Stand ein und spaetere
    /// NPK-Korrekturen im Default-Katalog erreichen Bestandsnutzer nie (Audit W18).
    /// Aktiv geaenderte NPK-Werte (abweichend vom Default) bleiben erhalten.
    /// </summary>
    public bool SaveUserOverrides(CostCatalog catalog, string? projectPath, out string error)
    {
        if (catalog is null)
        {
            error = "Kostenkatalog fehlt; Speichern ist gesperrt.";
            return false;
        }

        try
        {
            ValidateCatalogStructure(catalog);
        }
        catch (Exception ex)
        {
            error = $"Kostenkatalog ist ungueltig; Speichern ist gesperrt: {ex.Message}";
            return false;
        }

        var defaults = ReadCatalog(
            ResolvePath(projectPath, "cost_catalog.json"),
            out var defaultLoadError);
        if (!string.IsNullOrWhiteSpace(defaultLoadError))
        {
            error = $"Speichern ist gesperrt, weil der Default-Katalog nicht sauber geladen werden konnte: {defaultLoadError}";
            return false;
        }

        var toSave = BuildUserOverridesForSave(catalog, defaults);
        return SaveUserOverrides(toSave, out error);
    }

    public static CostCatalog BuildUserOverridesForSave(CostCatalog catalog, CostCatalog defaults)
    {
        var defaultMap = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in defaults.Items)
        {
            var key = NormalizeKey(d.Key, d.Name);
            if (!string.IsNullOrWhiteSpace(key))
                defaultMap[key] = d;
        }

        var toSave = new CostCatalog
        {
            Version = catalog.Version,
            Currency = catalog.Currency,
            VatRate = catalog.VatRate,
            Items = catalog.Items.Select(CloneItem).ToList()
        };

        foreach (var item in toSave.Items)
        {
            var key = NormalizeKey(item.Key, item.Name);
            if (string.IsNullOrWhiteSpace(key) || !defaultMap.TryGetValue(key, out var def))
                continue;
            if (string.Equals((item.NpkCode ?? "").Trim(), (def.NpkCode ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                item.NpkCode = "";
            if (string.Equals((item.Chapter ?? "").Trim(), (def.Chapter ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                item.Chapter = "";
            if (string.Equals((item.NpkCodeD16 ?? "").Trim(), (def.NpkCodeD16 ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                item.NpkCodeD16 = "";
        }

        return toSave;
    }

    public bool ResetUserOverrides(out string error)
    {
        error = "";
        try
        {
            var path = ResolveUserOverridePath();
            var probe = CostStoreFileProbe.Probe(path);
            if (probe.State == CostStorePathState.Invalid)
            {
                error = probe.Error ?? "User-Override ist nicht sicher zugreifbar.";
                return false;
            }

            if (probe.State == CostStorePathState.File)
                File.Delete(path);

            if (CostStoreFileProbe.Probe(path).State != CostStorePathState.Missing)
            {
                error = "User-Override konnte nicht sicher entfernt werden.";
                return false;
            }

            LastUserOverrideLoadError = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public CostCatalogItem? FindByPosition(CostCatalog catalog, string position)
    {
        if (string.IsNullOrWhiteSpace(position))
            return null;

        var normalized = position.Trim();
        foreach (var item in catalog.Items)
        {
            if (!item.Active)
                continue;
            if (string.Equals(item.Name, normalized, StringComparison.OrdinalIgnoreCase))
                return item;
            if (item.Aliases is not null &&
                item.Aliases.Any(a => string.Equals(a?.Trim(), normalized, StringComparison.OrdinalIgnoreCase)))
                return item;
        }

        return null;
    }

    public bool UpsertByPosition(CostCatalog catalog, string position, decimal? unitPrice, string? unit, bool active, IEnumerable<string>? aliases)
    {
        if (string.IsNullOrWhiteSpace(position))
            return false;

        var existing = FindByPosition(catalog, position);
        if (existing is null)
        {
            var key = BuildKey(position);
            catalog.Items.Add(new CostCatalogItem
            {
                Key = key,
                Name = position.Trim(),
                Unit = unit?.Trim() ?? "",
                Price = unitPrice,
                Active = active,
                Aliases = aliases?.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList() ?? new List<string>()
            });
            return true;
        }

        existing.Name = position.Trim();
        existing.Unit = unit?.Trim() ?? existing.Unit;
        if (unitPrice.HasValue)
            existing.Price = unitPrice;
        existing.Active = active;
        if (aliases is not null)
            existing.Aliases = aliases.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
        return true;
    }

    private static string ResolvePath(string? projectPath, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var dir = Path.GetDirectoryName(projectPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var projectPathCandidate = Path.Combine(dir, "Config", fileName);
                if (CostStoreFileProbe.ShouldUseProjectCandidate(projectPathCandidate))
                    return projectPathCandidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "Config", fileName);
    }

    private string ResolveUserOverridePath()
    {
        if (!string.IsNullOrWhiteSpace(_userOverridePath))
            return _userOverridePath;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AuswertungPro", "cost_catalog.user.json");
    }

    public static IReadOnlyList<CostCatalogNpkDuplicateWarning> FindDuplicateNpkCodesWithDifferentUnits(CostCatalog catalog)
    {
        return (catalog.Items ?? new List<CostCatalogItem>())
            .Where(i => i.Active && !string.IsNullOrWhiteSpace(i.NpkCode))
            .GroupBy(i => i.NpkCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Code = g.Key,
                Units = g.Select(i => (i.Unit ?? "").Trim())
                    .Where(u => u.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Keys = g.Select(i => (i.Key ?? "").Trim())
                    .Where(k => k.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(x => x.Units.Count > 1)
            .Select(x => new CostCatalogNpkDuplicateWarning(x.Code, x.Units, x.Keys))
            .ToList();
    }

    private CostCatalog ReadCatalog(string path, bool rememberUserOverrideError = false)
        => ReadCatalog(path, out _, rememberUserOverrideError);

    private CostCatalog ReadCatalog(string path, out string? loadError, bool rememberUserOverrideError = false)
    {
        try
        {
            loadError = null;
            var probe = CostStoreFileProbe.Probe(path);
            if (probe.State == CostStorePathState.Missing)
                return new CostCatalog();
            if (probe.State != CostStorePathState.File)
                throw new IOException(probe.Error ?? "Katalogdatei ist nicht sicher lesbar.");

            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<CostCatalog>(json, JsonOptions)
                        ?? throw new JsonException("Der Kostenkatalog darf nicht null sein.");
            return Normalize(model);
        }
        catch (Exception ex)
        {
            loadError = $"{Path.GetFileName(path)} ist beschaedigt oder nicht lesbar: {ex.Message}";
            if (rememberUserOverrideError)
                LastUserOverrideLoadError = ex.Message;
            return new CostCatalog();
        }
    }

    private static CostCatalog Normalize(CostCatalog model)
    {
        ValidateCatalogStructure(model);
        var normalized = new CostCatalog
        {
            Version = model.Version > 0 ? model.Version : 1,
            Currency = string.IsNullOrWhiteSpace(model.Currency) ? "CHF" : model.Currency,
            VatRate = model.VatRate,
            Items = new List<CostCatalogItem>()
        };

        foreach (var item in model.Items)
        {
            item.Key ??= "";
            item.Name ??= "";
            item.Unit ??= "";
            item.Type ??= "Fixed";
            item.NpkCode ??= "";
            item.Chapter ??= "";
            item.NpkCodeD16 ??= "";
            normalized.Items.Add(item);
        }

        return normalized;
    }

    private static void ValidateCatalogStructure(CostCatalog model)
    {
        if (model.Items is null)
            throw new InvalidDataException("Das Feld 'items' darf nicht null sein.");

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < model.Items.Count; index++)
        {
            var item = model.Items[index]
                       ?? throw new InvalidDataException($"Kostenposition {index + 1} darf nicht null sein.");
            var key = NormalizeKey(item.Key, item.Name);
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidDataException($"Kostenposition {index + 1} hat weder Schluessel noch Name.");
            if (!keys.Add(key))
                throw new InvalidDataException($"Der normalisierte Kosten-Schluessel '{key}' ist doppelt.");
            if (item.DnPrices is null)
                throw new InvalidDataException($"DN-Preise der Kostenposition '{key}' duerfen nicht null sein.");
            if (item.DnPrices.Any(price => price is null))
                throw new InvalidDataException($"DN-Preise der Kostenposition '{key}' enthalten einen leeren Eintrag.");
            if (item.Aliases is null)
                throw new InvalidDataException($"Aliase der Kostenposition '{key}' duerfen nicht null sein.");
        }
    }

    private static CostCatalog Merge(CostCatalog defaults, CostCatalog overrides)
    {
        var map = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in defaults.Items)
        {
            var key = NormalizeKey(item.Key, item.Name);
            if (string.IsNullOrWhiteSpace(key))
                continue;
            map[key] = CloneItem(item with { Key = key });
        }

        foreach (var item in overrides.Items)
        {
            var key = NormalizeKey(item.Key, item.Name);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var merged = CloneItem(item with { Key = key });
            // NPK-Metadaten aus dem Default behalten, falls ein (evtl. aelterer) Preis-Override
            // sie nicht traegt - sonst gehen NpkCode/Chapter beim Mergen verloren.
            map.TryGetValue(key, out var def);
            map[key] = PreserveNpkMetadata(merged, def);
        }

        return new CostCatalog
        {
            Version = Math.Max(defaults.Version, overrides.Version),
            Currency = string.IsNullOrWhiteSpace(overrides.Currency) ? defaults.Currency : overrides.Currency,
            VatRate = overrides.VatRate != 0 ? overrides.VatRate : defaults.VatRate,
            Items = map.Values
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    /// <summary>
    /// Behält NpkCode/Chapter aus dem Default-Item (<paramref name="fallback"/>), wenn der
    /// Override sie nicht trägt — z.B. ein vor der NPK-Erweiterung gespeicherter Preis-Override.
    /// Preis, DN-Preise, Aktiv etc. aus dem Override bleiben unverändert.
    /// </summary>
    public static CostCatalogItem PreserveNpkMetadata(CostCatalogItem item, CostCatalogItem? fallback)
    {
        if (fallback is not null)
        {
            if (string.IsNullOrWhiteSpace(item.NpkCode))
                item.NpkCode = fallback.NpkCode;
            if (string.IsNullOrWhiteSpace(item.Chapter))
                item.Chapter = fallback.Chapter;
            if (string.IsNullOrWhiteSpace(item.NpkCodeD16))
                item.NpkCodeD16 = fallback.NpkCodeD16;
        }
        return item;
    }

    private static string NormalizeKey(string? key, string? name)
    {
        var raw = string.IsNullOrWhiteSpace(key) ? name : key;
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var buffer = raw.Trim().ToUpperInvariant();
        var cleaned = new string(buffer
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());
        return cleaned.Trim('_');
    }

    private static string BuildKey(string name)
        => NormalizeKey("", name);

    private static CostCatalogItem CloneItem(CostCatalogItem item)
        => new()
        {
            Key = item.Key,
            Name = item.Name,
            Unit = item.Unit,
            Type = item.Type,
            Price = item.Price,
            DnPrices = (item.DnPrices ?? new List<DnPrice>()).Select(p => new DnPrice
            {
                DnFrom = p.DnFrom,
                DnTo = p.DnTo,
                QtyFrom = p.QtyFrom,
                QtyTo = p.QtyTo,
                Price = p.Price
            }).ToList(),
            Active = item.Active,
            Aliases = (item.Aliases ?? new List<string>()).Select(a => a).ToList(),
            NpkCode = item.NpkCode ?? "",
            Chapter = item.Chapter ?? "",
            NpkCodeD16 = item.NpkCodeD16 ?? ""
        };
}
