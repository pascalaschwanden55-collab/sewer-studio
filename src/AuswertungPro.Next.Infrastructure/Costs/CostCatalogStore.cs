using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class CostCatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CostCatalog LoadMerged(string? projectPath)
    {
        var defaults = LoadDefault(projectPath);
        var overrides = LoadUserOverrides();
        return Merge(defaults, overrides);
    }

    public CostCatalog LoadDefault(string? projectPath)
    {
        var path = ResolvePath(projectPath, "cost_catalog.json");
        return ReadCatalog(path);
    }

    public CostCatalog LoadUserOverrides()
    {
        var path = ResolveUserOverridePath();
        return ReadCatalog(path);
    }

    public bool SaveUserOverrides(CostCatalog catalog, out string error)
    {
        error = "";
        try
        {
            var path = ResolveUserOverridePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool ResetUserOverrides(out string error)
    {
        error = "";
        try
        {
            var path = ResolveUserOverridePath();
            if (File.Exists(path))
                File.Delete(path);
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
                if (File.Exists(projectPathCandidate))
                    return projectPathCandidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "Config", fileName);
    }

    private static string ResolveUserOverridePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AuswertungPro", "cost_catalog.user.json");
    }

    private static CostCatalog ReadCatalog(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new CostCatalog();

            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<CostCatalog>(json, JsonOptions) ?? new CostCatalog();
            return Normalize(model);
        }
        catch
        {
            return new CostCatalog();
        }
    }

    private static CostCatalog Normalize(CostCatalog model)
    {
        var normalized = new CostCatalog
        {
            Version = model.Version > 0 ? model.Version : 1,
            Currency = string.IsNullOrWhiteSpace(model.Currency) ? "CHF" : model.Currency,
            VatRate = model.VatRate,
            Items = new List<CostCatalogItem>()
        };

        foreach (var item in model.Items ?? new List<CostCatalogItem>())
        {
            item.DnPrices ??= new List<DnPrice>();
            item.Aliases ??= new List<string>();
            item.Key ??= "";
            item.Name ??= "";
            item.Unit ??= "";
            item.Type ??= "Fixed";
            normalized.Items.Add(item);
        }

        return normalized;
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

            map[key] = CloneItem(item with { Key = key });
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
            Aliases = (item.Aliases ?? new List<string>()).Select(a => a).ToList()
        };
}
