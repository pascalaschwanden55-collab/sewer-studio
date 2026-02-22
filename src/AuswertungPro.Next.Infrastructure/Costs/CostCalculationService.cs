using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models.Costs;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class CostCalculationService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _seedDataDir;
    private readonly string _userDataDir;
    private readonly string _seedCatalogPath;
    private readonly string _legacyUserCatalogPath;
    private readonly string _userCatalogPath;
    private readonly string _seedTemplatesPath;
    private readonly string _userTemplatesPath;

    public CostCalculationService(string projectRoot)
    {
        _seedDataDir = Path.Combine(projectRoot, "Data");
        _userDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AuswertungPro",
            "legacy_costs");
        _seedCatalogPath = Path.Combine(_seedDataDir, "seed_price_catalog.json");
        _legacyUserCatalogPath = Path.Combine(_seedDataDir, "user_catalog.json");
        _userCatalogPath = Path.Combine(_userDataDir, "user_catalog.json");
        _seedTemplatesPath = Path.Combine(_seedDataDir, "measure_templates.json");
        _userTemplatesPath = Path.Combine(_userDataDir, "measure_templates.json");

        EnsureUserDataDirectory();
        EnsureSeedCatalog();
        EnsureSeedTemplates();
    }

    private void EnsureUserDataDirectory()
    {
        if (!Directory.Exists(_userDataDir))
            Directory.CreateDirectory(_userDataDir);
    }

    private void EnsureSeedCatalog()
    {
        if (File.Exists(_userCatalogPath))
            return;

        if (File.Exists(_legacyUserCatalogPath))
        {
            File.Copy(_legacyUserCatalogPath, _userCatalogPath, false);
            return;
        }

        if (File.Exists(_seedCatalogPath))
        {
            File.Copy(_seedCatalogPath, _userCatalogPath, false);
        }
    }
    
    private void EnsureSeedTemplates()
    {
        if (File.Exists(_userTemplatesPath))
            return;
        if (File.Exists(_seedTemplatesPath))
            File.Copy(_seedTemplatesPath, _userTemplatesPath, false);
    }

    public PriceCatalog LoadCatalog()
    {

        PriceCatalog? TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PriceCatalog>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        var user = TryLoad(_userCatalogPath);
        if (user is { Items.Count: > 0 })
            return user;

        var seed = TryLoad(_seedCatalogPath);
        if (seed is { Items.Count: > 0 })
        {
            // Self-heal: if an old/invalid user_catalog.json exists, replace it with the valid seed.
            try
            {
                File.Copy(_seedCatalogPath, _userCatalogPath, overwrite: true);
            }
            catch
            {
                // ignore; we'll still return seed
            }

            return seed;
        }

        return new PriceCatalog();
    }

    public void SaveCatalog(PriceCatalog catalog)
    {
        EnsureUserDataDirectory();
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(catalog, options);
        File.WriteAllText(_userCatalogPath, json);
    }

    public MeasureTemplates LoadTemplates()
    {
        MeasureTemplates? TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<MeasureTemplates>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        var user = TryLoad(_userTemplatesPath);
        if (user is { Templates.Count: > 0 })
            return user;

        var seed = TryLoad(_seedTemplatesPath);
        if (seed is { Templates.Count: > 0 })
        {
            try
            {
                File.Copy(_seedTemplatesPath, _userTemplatesPath, overwrite: true);
            }
            catch
            {
                // ignore; we'll still return seed
            }

            return seed;
        }

        return new MeasureTemplates();
    }

    public void SaveTemplates(MeasureTemplates templates)
    {
        EnsureUserDataDirectory();
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(templates, options);
        File.WriteAllText(_userTemplatesPath, json);
    }

    public string GetCatalogPath() => _userCatalogPath;

    public PriceItem? SelectPriceItemForDn(List<PriceItem> items, string id, int dn)
    {
        var candidates = items.Where(i => i.Id == id).ToList();
        if (candidates.Count == 0) return null;

        if (dn > 0)
        {
            var dnCandidates = candidates
                .Where(i => i.DnMin.HasValue && i.DnMax.HasValue && dn >= i.DnMin.Value && dn <= i.DnMax.Value)
                .OrderBy(i => (i.DnMax!.Value - i.DnMin!.Value))
                .ThenBy(i => i.DnMin!.Value)
                .ToList();

            if (dnCandidates.Count > 0)
                return dnCandidates.First();
        }

        var noDn = candidates.Where(i => !i.DnMin.HasValue || !i.DnMax.HasValue).ToList();
        if (noDn.Count > 0) return noDn.First();

        return candidates.First();
    }

    public decimal ResolveQty(JsonElement qtySpec, Dictionary<string, object> inputs)
    {
        if (qtySpec.ValueKind == JsonValueKind.Number)
            return qtySpec.GetDecimal();

        if (qtySpec.ValueKind == JsonValueKind.String)
        {
            var key = qtySpec.GetString() ?? "";
            if (inputs.TryGetValue(key, out var value))
                return Convert.ToDecimal(value);
            throw new Exception($"Unbekannte qty-Variable: '{key}'");
        }

        throw new Exception($"Ungültiger qty-Typ: {qtySpec.ValueKind}");
    }

    public CalculatedOffer CalculateOffer(MeasureTemplate template, PriceCatalog catalog, MeasureInputs inputs)
    {
        var inputsDict = inputs.ToDictionary();
        var lines = new List<OfferLine>();
        var warnings = new List<string>();

        foreach (var tLine in template.Lines)
        {
            // Optional when condition
            if (!string.IsNullOrWhiteSpace(tLine.When))
            {
                if (!inputsDict.TryGetValue(tLine.When, out var whenValue) || !Convert.ToBoolean(whenValue))
                    continue;
            }

            var qty = ResolveQty(tLine.Qty, inputsDict);
            if (qty <= 0) continue;

            var item = SelectPriceItemForDn(catalog.Items, tLine.ItemRef, inputs.Dn);

            if (item == null || item.UnitPrice == 0)
            {
                warnings.Add($"Preis fehlt: {tLine.ItemRef} (DN {inputs.Dn}). Bitte im Preiskatalog ergänzen.");
                lines.Add(new OfferLine
                {
                    Measure = template.Name,
                    Group = tLine.Group,
                    Label = $"{tLine.ItemRef} (PREIS FEHLT)",
                    Unit = item?.Unit ?? "",
                    Qty = Math.Round(qty, 3),
                    UnitPrice = null,
                    Amount = null,
                    Source = null
                });
                continue;
            }

            var amount = Math.Round(qty * item.UnitPrice, 2);
            var source = item.Source != null ? $"{item.Source.File} / {item.Source.Pos}" : null;

            lines.Add(new OfferLine
            {
                Measure = template.Name,
                Group = tLine.Group,
                Label = item.Label,
                Unit = item.Unit,
                Qty = Math.Round(qty, 3),
                UnitPrice = Math.Round(item.UnitPrice, 2),
                Amount = amount,
                Source = source
            });
        }

        var subTotal = Math.Round(lines.Where(l => l.Amount.HasValue).Sum(l => l.Amount!.Value), 2);
        var rabatt = Math.Round(subTotal * inputs.RabattPct / 100m, 2);
        var afterRabatt = Math.Round(subTotal - rabatt, 2);
        var skonto = Math.Round(afterRabatt * inputs.SkontoPct / 100m, 2);
        var netExcl = Math.Round(afterRabatt - skonto, 2);
        var mwst = Math.Round(netExcl * inputs.MwstPct / 100m, 2);
        var total = Math.Round(netExcl + mwst, 2);

        return new CalculatedOffer
        {
            TemplateId = template.Id,
            Lines = lines,
            Warnings = warnings,
            Totals = new OfferTotals
            {
                SubTotal = subTotal,
                RabattPct = inputs.RabattPct,
                Rabatt = rabatt,
                SkontoPct = inputs.SkontoPct,
                Skonto = skonto,
                NetExclMwst = netExcl,
                MwstPct = inputs.MwstPct,
                Mwst = mwst,
                TotalInclMwst = total,
                Currency = catalog.Currency
            }
        };
    }

    public CalculatedOffer CalculateCombinedOffer(List<MeasureTemplate> templates, PriceCatalog catalog, 
        List<MeasureInputs> inputRows, decimal mwstPct = 8.1m)
    {
        var allLines = new List<OfferLine>();
        var warnings = new List<string>();
        var rabattPct = inputRows.FirstOrDefault()?.RabattPct ?? 0m;
        var skontoPct = inputRows.FirstOrDefault()?.SkontoPct ?? 0m;

        for (int i = 0; i < templates.Count; i++)
        {
            var tpl = templates[i];
            var inp = i < inputRows.Count
                ? inputRows[i]
                : new MeasureInputs
                {
                    RabattPct = rabattPct,
                    SkontoPct = skontoPct,
                    MwstPct = mwstPct
                };
            inp.MwstPct = mwstPct;

            var offer = CalculateOffer(tpl, catalog, inp);
            foreach (var w in offer.Warnings)
                warnings.Add($"[{tpl.Name}] {w}");
            allLines.AddRange(offer.Lines);
        }

        var subTotal = Math.Round(allLines.Where(l => l.Amount.HasValue).Sum(l => l.Amount!.Value), 2);
        var rabatt = Math.Round(subTotal * rabattPct / 100m, 2);
        var afterRabatt = Math.Round(subTotal - rabatt, 2);
        var skonto = Math.Round(afterRabatt * skontoPct / 100m, 2);
        var netExcl = Math.Round(afterRabatt - skonto, 2);
        var mwst = Math.Round(netExcl * mwstPct / 100m, 2);
        var total = Math.Round(netExcl + mwst, 2);

        return new CalculatedOffer
        {
            TemplateId = "combined",
            Lines = allLines,
            Warnings = warnings,
            Totals = new OfferTotals
            {
                SubTotal = subTotal,
                RabattPct = rabattPct,
                Rabatt = rabatt,
                SkontoPct = skontoPct,
                Skonto = skonto,
                NetExclMwst = netExcl,
                MwstPct = mwstPct,
                Mwst = mwst,
                TotalInclMwst = total,
                Currency = catalog.Currency
            }
        };
    }
}
