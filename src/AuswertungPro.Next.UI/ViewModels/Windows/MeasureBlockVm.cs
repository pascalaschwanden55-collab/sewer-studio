using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

// MeasureBlockVm + MeasureTemplateListItem + CatalogItemOption — aus
// CostCalculatorViewModel.cs in eigene Datei extrahiert (Slice 11a). Pure
// ViewModel-Logik fuer einzelne Sanierungs-Massnahmen-Bloecke (Material,
// DN, Laenge, Anschluesse, einzelne Kostenzeilen).
public sealed class MeasureTemplateListItem
{
    public MeasureTemplate Template { get; }
    public string Id => Template.Id;
    public string Name => Template.Name;
    public bool Disabled => Template.Disabled;
    public string DisplayName => Disabled ? $"{Name} (deaktiviert)" : Name;

    public MeasureTemplateListItem(MeasureTemplate template)
    {
        Template = template;
    }
}

public sealed record CatalogItemOption(string Key, string Group, string DisplayName);

public sealed partial class MeasureBlockVm : ObservableObject
{
    private const string InstallUvAnlageKey = "INSTALL_UV_ANLAGE";
    private const string InstallHlAnlageKey = "INSTALL_HL_ANLAGE";

    private static readonly string[] GroupOrder =
    {
        "Installation",
        "Vorarbeiten",
        "Hauptarbeit",
        "Qualitaetskontrolle",
        "Qualitaet",
        "Sonstiges"
    };
    private IReadOnlyDictionary<string, CostCatalogItem> _catalog;
    private readonly Dictionary<string, int> _templateLineOrderByItemKey = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressDnUpdate;
    private bool _suppressLengthUpdate;
    private bool _suppressConnectionsUpdate;
    private bool _applyingPrices;
    private bool _enforcingInstallationRule;

    public string MeasureId { get; }
    public string MeasureName { get; }

    public ObservableCollection<CostLineVm> Lines { get; } = new();

    /// <summary>Available catalog positions for the "Add line" ComboBox.</summary>
    public ObservableCollection<CatalogItemOption> AvailableCatalogItems { get; } = new();

    [ObservableProperty] private CatalogItemOption? _selectedCatalogItem;
    [ObservableProperty] private string _dnText = "";
    [ObservableProperty] private string _lengthText = "";
    [ObservableProperty] private string _connectionsText = "";
    [ObservableProperty] private string _priceHint = "";
    [ObservableProperty] private decimal _total;

    public IRelayCommand AddLineCommand { get; }
    public IRelayCommand<CostLineVm> RemoveLineCommand { get; }
    public IRelayCommand<CostLineVm> MoveLineUpCommand { get; }
    public IRelayCommand<CostLineVm> MoveLineDownCommand { get; }
    public IRelayCommand SortLinesCommand { get; }

    public event Action? BlockChanged;

    public MeasureBlockVm(MeasureTemplate? template, IReadOnlyDictionary<string, CostCatalogItem> catalog)
    {
        _catalog = catalog;
        MeasureId = template?.Id ?? "";
        MeasureName = template?.Name ?? "Unbekannt";

        RebuildAvailableCatalogItems();

        AddLineCommand = new RelayCommand(AddLine, () => SelectedCatalogItem is not null);
        RemoveLineCommand = new RelayCommand<CostLineVm>(RemoveLine);
        MoveLineUpCommand = new RelayCommand<CostLineVm>(MoveLineUp);
        MoveLineDownCommand = new RelayCommand<CostLineVm>(MoveLineDown);
        SortLinesCommand = new RelayCommand(SortLines);

        if (template is not null)
        {
            for (var i = 0; i < template.Lines.Count; i++)
            {
                var itemKey = template.Lines[i].ItemKey?.Trim();
                if (!string.IsNullOrWhiteSpace(itemKey) && !_templateLineOrderByItemKey.ContainsKey(itemKey))
                    _templateLineOrderByItemKey[itemKey] = i;
            }

            var ordered = template.Lines
                .Select((line, index) => new
                {
                    Line = line,
                    Index = index,
                    Order = GetGroupOrder(line.Group)
                })
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Index)
                .Select(x => x.Line)
                .ToList();

            foreach (var line in ordered)
                Lines.Add(CreateLine(line));
        }

        AttachLines();
        EnforceInstallationRule();
        UpdateTotal();
    }

    public void LoadFrom(MeasureCost measure)
    {
        _suppressDnUpdate = true;
        DnText = measure.Dn?.ToString() ?? "";
        _suppressDnUpdate = false;

        _suppressLengthUpdate = true;
        LengthText = measure.LengthMeters?.ToString("0.00") ?? "";
        _suppressLengthUpdate = false;

        _suppressConnectionsUpdate = true;
        ConnectionsText = "";
        _suppressConnectionsUpdate = false;

        Lines.Clear();
        foreach (var line in measure.Lines)
        {
            var vm = new CostLineVm
            {
                Group = line.Group,
                ItemKey = line.ItemKey,
                Text = line.Text,
                Unit = line.Unit,
                Qty = line.Qty,
                UnitPrice = line.UnitPrice,
                Selected = line.Selected,
                TransferMarked = line.TransferMarked,
                IsPriceOverridden = line.IsPriceOverridden,
                IsQtyOverridden = line.IsQtyOverridden
            };
            Lines.Add(vm);
        }

        TryInitializeConnectionsFromLines();
        AttachLines();
        EnforceInstallationRule();
        UpdateTotal();
    }

    public MeasureCost ToModel()
    {
        var lines = Lines.Select(l => new CostLine
        {
            Group = l.Group,
            ItemKey = l.ItemKey,
            Text = l.Text,
            Unit = l.Unit,
            Qty = l.Qty,
            UnitPrice = l.UnitPrice,
            Selected = l.Selected,
            TransferMarked = l.TransferMarked,
            IsPriceOverridden = l.IsPriceOverridden,
            IsQtyOverridden = l.IsQtyOverridden
        }).ToList();

        var total = lines.Where(l => l.Selected).Sum(l => l.Qty * l.UnitPrice);

        return new MeasureCost
        {
            MeasureId = MeasureId,
            MeasureName = MeasureName,
            Dn = ParseDn(DnText),
            LengthMeters = ParseDecimal(LengthText),
            Lines = lines,
            Total = total
        };
    }

    public void ApplyCatalogPrices()
    {
        ApplyCatalogPricesInternal(onlyQtyBased: false);
    }

    public void SetDnFromImport(string dn)
    {
        if (string.IsNullOrWhiteSpace(DnText)) // Only set if not already manually entered
        {
            _suppressDnUpdate = true;
            DnText = dn;
            _suppressDnUpdate = false;
            ApplyCatalogPrices();
        }
    }

    public void SetLengthFromImport(string length)
    {
        if (string.IsNullOrWhiteSpace(LengthText)) // Only set if not already manually entered
        {
            _suppressLengthUpdate = true;
            LengthText = length;
            _suppressLengthUpdate = false;
            ApplyLengthToLines();
        }
    }

    public void SetConnectionsFromImport(string connections)
    {
        if (string.IsNullOrWhiteSpace(ConnectionsText)) // Only set if not already manually entered
        {
            _suppressConnectionsUpdate = true;
            ConnectionsText = connections;
            _suppressConnectionsUpdate = false;
            ApplyConnectionsToLines();
        }
    }

    partial void OnDnTextChanged(string value)
    {
        if (_suppressDnUpdate)
            return;

        ApplyCatalogPrices();
    }

    partial void OnLengthTextChanged(string value)
    {
        if (_suppressLengthUpdate)
            return;

        ApplyLengthToLines();
    }

    partial void OnConnectionsTextChanged(string value)
    {
        if (_suppressConnectionsUpdate)
            return;

        ApplyConnectionsToLines();
    }

    private void AttachLines()
    {
        foreach (var line in Lines)
            line.LineChanged += OnLineChanged;
    }

    /// <summary>Replace the catalog reference and rebuild the per-block combo list.</summary>
    public void RefreshCatalog(IReadOnlyDictionary<string, CostCatalogItem> catalog)
    {
        _catalog = catalog;
        RebuildAvailableCatalogItems();
    }

    private void RebuildAvailableCatalogItems()
    {
        AvailableCatalogItems.Clear();
        foreach (var c in _catalog.Values
                     .Where(c => c.Active)
                     .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            AvailableCatalogItems.Add(new CatalogItemOption(c.Key, "", $"{c.Name}  [{c.Unit}]"));
        }
    }

    partial void OnSelectedCatalogItemChanged(CatalogItemOption? value)
    {
        ((RelayCommand)AddLineCommand).NotifyCanExecuteChanged();
    }

    private void AddLine()
    {
        if (SelectedCatalogItem is null)
            return;

        AddLineFromCatalogKey(SelectedCatalogItem.Key);
        SelectedCatalogItem = null;
    }

    /// <summary>Add a catalog position by key. Used by ComboBox and drag-drop.</summary>
    public bool AddLineFromCatalogKey(string key)
    {
        if (!_catalog.TryGetValue(key, out var item))
            return false;

        if (IsInstallationItemKey(item.Key))
        {
            var existingInstallationLines = Lines
                .Where(IsInstallationLine)
                .ToList();

            foreach (var existingLine in existingInstallationLines)
            {
                existingLine.LineChanged -= OnLineChanged;
                Lines.Remove(existingLine);
            }
        }

        var vm = new CostLineVm
        {
            Group = CostCalculatorViewModel.DeriveGroupFromKey(item.Key),
            ItemKey = item.Key,
            Text = item.Name,
            Unit = item.Unit,
            Qty = 1m,
            Selected = true
        };

        // Try to apply a price immediately
        if (string.Equals(item.Type, "Fixed", StringComparison.OrdinalIgnoreCase) && item.Price.HasValue)
            vm.SetSuggestedPrice(item.Price, true);
        else if (string.Equals(item.Type, "ByDN", StringComparison.OrdinalIgnoreCase))
        {
            var dn = ParseDn(DnText);
            if (dn is not null)
            {
                var match = item.DnPrices
                    .FirstOrDefault(x => dn >= x.DnFrom && dn <= x.DnTo);
                vm.SetSuggestedPrice(match?.Price, match is not null);
            }
        }

        var connections = ParseDecimal(ConnectionsText);
        if (connections is not null && IsConnectionLine(vm))
        {
            if (connections.Value <= 0m)
            {
                vm.SetSuggestedQty(0m);
                vm.Selected = false;
                vm.TransferMarked = false;
            }
            else
            {
                vm.SetSuggestedQty(connections.Value);
            }
        }

        vm.LineChanged += OnLineChanged;
        Lines.Add(vm);
        EnforceInstallationRule();
        UpdateTotal();
        return true;
    }

    private void RemoveLine(CostLineVm? line)
    {
        if (line is null)
            return;

        line.LineChanged -= OnLineChanged;
        Lines.Remove(line);
        UpdateTotal();
    }

    private void MoveLineUp(CostLineVm? line)
    {
        if (line is null) return;
        var idx = Lines.IndexOf(line);
        if (idx <= 0) return;
        Lines.Move(idx, idx - 1);
    }

    private void MoveLineDown(CostLineVm? line)
    {
        if (line is null) return;
        var idx = Lines.IndexOf(line);
        if (idx < 0 || idx >= Lines.Count - 1) return;
        Lines.Move(idx, idx + 1);
    }

    public void SortLines()
    {
        if (Lines.Count <= 1)
            return;

        var ordered = Lines
            .Select((line, index) => new
            {
                Line = line,
                Index = index,
                GroupOrder = GetGroupOrder(line.Group),
                TemplateOrder = GetTemplateLineOrder(line.ItemKey),
                Text = line.Text ?? string.Empty,
                ItemKey = line.ItemKey ?? string.Empty
            })
            .OrderBy(x => x.GroupOrder)
            .ThenBy(x => x.TemplateOrder)
            .ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ItemKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Index)
            .Select(x => x.Line)
            .ToList();

        ReorderCollection(Lines, ordered);
    }

    private int GetTemplateLineOrder(string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return int.MaxValue;

        return _templateLineOrderByItemKey.TryGetValue(itemKey.Trim(), out var order)
            ? order
            : int.MaxValue;
    }

    private static void ReorderCollection<T>(ObservableCollection<T> collection, IReadOnlyList<T> ordered)
    {
        for (var targetIndex = 0; targetIndex < ordered.Count; targetIndex++)
        {
            var desired = ordered[targetIndex];
            if (ReferenceEquals(collection[targetIndex], desired))
                continue;

            var currentIndex = collection.IndexOf(desired);
            if (currentIndex >= 0 && currentIndex != targetIndex)
                collection.Move(currentIndex, targetIndex);
        }
    }

    private void OnLineChanged()
    {
        if (_applyingPrices || _enforcingInstallationRule)
            return;

        ApplyCatalogPricesInternal(onlyQtyBased: true);
        EnforceInstallationRule();
    }

    private void UpdateTotal()
    {
        Total = Lines.Sum(l => l.LineTotal);
        BlockChanged?.Invoke();
    }

    private void UpdatePriceHint()
    {
        var missing = Lines.Where(l => l.Selected && l.PriceMissing).Select(l => l.Text).Distinct().ToList();
        PriceHint = missing.Count == 0
            ? ""
            : "Preis nicht gefunden fuer: " + string.Join(", ", missing);
    }

    private CostLineVm CreateLine(MeasureLineTemplate templateLine)
    {
        var item = _catalog.TryGetValue(templateLine.ItemKey, out var found) ? found : null;

        var vm = new CostLineVm
        {
            Group = templateLine.Group,
            ItemKey = templateLine.ItemKey,
            Text = item?.Name ?? templateLine.ItemKey,
            Unit = item?.Unit ?? "",
            Selected = templateLine.Enabled
        };
        // Use SetSuggestedQty so IsQtyOverridden stays false,
        // allowing Linerlaenge to auto-fill meter-based lines later.
        vm.SetSuggestedQty(templateLine.DefaultQty);
        return vm;
    }

    private void ApplyLengthToLines()
    {
        var length = ParseDecimal(LengthText);
        if (length is null)
            return;

        foreach (var line in Lines)
        {
            if (!IsMeterUnit(line.Unit))
                continue;
            if (line.IsQtyOverridden)
                continue;

            line.SetSuggestedQty(length.Value);
        }

        UpdateTotal();
    }

    private void ApplyConnectionsToLines()
    {
        var connections = ParseDecimal(ConnectionsText);
        if (connections is null)
            return;

        var disableConnectionWork = connections.Value <= 0m;

        foreach (var line in Lines)
        {
            if (!IsConnectionLine(line))
                continue;

            if (disableConnectionWork)
            {
                // Explicitly disabling connections should always clear related work items.
                line.SetSuggestedQty(0m);
                line.IsQtyOverridden = false;
                line.Selected = false;
                line.TransferMarked = false;
                continue;
            }

            // Re-enable lines that were switched off by "0 Anschluesse".
            if (!line.Selected && line.Qty == 0m)
                line.Selected = true;

            if (line.IsQtyOverridden)
                continue;

            line.SetSuggestedQty(connections.Value);
        }

        UpdateTotal();
    }

    private void TryInitializeConnectionsFromLines()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionsText))
            return;

        var qty = Lines
            .Where(IsConnectionLine)
            .Where(l => l.Selected && l.Qty > 0)
            .Select(l => l.Qty)
            .DefaultIfEmpty(0m)
            .Max();

        if (qty <= 0)
            return;

        _suppressConnectionsUpdate = true;
        ConnectionsText = qty.ToString(CultureInfo.InvariantCulture);
        _suppressConnectionsUpdate = false;
    }

    private void ApplyCatalogPricesInternal(bool onlyQtyBased)
    {
        if (_applyingPrices)
            return;

        _applyingPrices = true;
        try
        {
            var dn = ParseDn(DnText);
            var length = ParseDecimal(LengthText);

            foreach (var line in Lines)
            {
                if (line.IsPriceOverridden)
                    continue;

                if (!_catalog.TryGetValue(line.ItemKey, out var item))
                {
                    if (!onlyQtyBased)
                        line.SetSuggestedPrice(null, false);
                    continue;
                }

                var hasQtyRules = item.DnPrices.Any(p => p.QtyFrom.HasValue || p.QtyTo.HasValue);
                if (onlyQtyBased && !hasQtyRules)
                    continue;

                if (string.Equals(item.Type, "Fixed", StringComparison.OrdinalIgnoreCase))
                {
                    if (!onlyQtyBased)
                        line.SetSuggestedPrice(item.Price, item.Price.HasValue);
                    continue;
                }

                if (!string.Equals(item.Type, "ByDN", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (dn is null)
                {
                    if (!onlyQtyBased)
                        line.SetSuggestedPrice(null, false);
                    continue;
                }

                var candidates = item.DnPrices
                    .Where(x => dn >= x.DnFrom && dn <= x.DnTo)
                    .ToList();

                if (candidates.Count == 0)
                {
                    // Fallback: use nearest DN bucket when exact DN is not configured.
                    candidates = FindNearestDnCandidates(item.DnPrices, dn.Value);
                    if (candidates.Count == 0)
                    {
                        if (!onlyQtyBased)
                            line.SetSuggestedPrice(null, false);
                        continue;
                    }
                }

                DnPrice? match = null;
                if (hasQtyRules)
                {
                    var qty = line.Qty;
                    match = candidates.FirstOrDefault(x => QtyMatches(x, qty));
                    if (match is null)
                        match = candidates.FirstOrDefault(x => !x.QtyFrom.HasValue && !x.QtyTo.HasValue);
                    if (match is null)
                        match = candidates[0];
                }
                else
                {
                    match = candidates[0];
                }

                line.SetSuggestedPrice(match?.Price, match is not null);
            }

        }
        finally
        {
            _applyingPrices = false;
        }

        UpdatePriceHint();
        UpdateTotal();
    }

    private bool HasPriceForDn(string itemKey, int dn)
    {
        if (!_catalog.TryGetValue(itemKey, out var item))
            return false;

        if (string.Equals(item.Type, "Fixed", StringComparison.OrdinalIgnoreCase))
            return item.Price.HasValue;

        if (!string.Equals(item.Type, "ByDN", StringComparison.OrdinalIgnoreCase))
            return false;

        return item.DnPrices.Any(p => dn >= p.DnFrom && dn <= p.DnTo);
    }

    private static bool QtyMatches(DnPrice price, decimal qty)
    {
        var minOk = !price.QtyFrom.HasValue || qty >= price.QtyFrom.Value;
        var maxOk = !price.QtyTo.HasValue || qty <= price.QtyTo.Value;
        return minOk && maxOk;
    }

    private static List<DnPrice> FindNearestDnCandidates(IEnumerable<DnPrice> prices, int dn)
    {
        var withDistance = prices
            .Select(p => new
            {
                Price = p,
                Distance = dn < p.DnFrom
                    ? p.DnFrom - dn
                    : dn > p.DnTo
                        ? dn - p.DnTo
                        : 0
            })
            .ToList();

        if (withDistance.Count == 0)
            return new List<DnPrice>();

        var minDistance = withDistance.Min(x => x.Distance);
        return withDistance
            .Where(x => x.Distance == minDistance)
            .Select(x => x.Price)
            .OrderBy(x => x.DnFrom)
            .ThenBy(x => x.DnTo)
            .ToList();
    }

    private static int? ParseDn(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return int.TryParse(raw.Trim(), out var dn) ? dn : null;
    }

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (TryParseDecimal(text, out var value))
            return value;

        // Accept entries like "8m" or "2d" by reading the numeric prefix.
        var numericPrefix = new string(text
            .TakeWhile(ch => char.IsDigit(ch) || ch is '+' or '-' or '.' or ',')
            .ToArray());
        if (numericPrefix.Length > 0 && TryParseDecimal(numericPrefix, out value))
            return value;

        return null;
    }

    private static bool IsMeterUnit(string? unit)
        => string.Equals(unit?.Trim(), "m", StringComparison.OrdinalIgnoreCase);

    private static bool IsConnectionLine(CostLineVm line)
    {
        if (line is null)
            return false;

        if (!string.IsNullOrWhiteSpace(line.ItemKey) &&
            line.ItemKey.Contains("ANSCHLUSS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(line.Text) &&
            line.Text.Contains("anschluss", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void EnforceInstallationRule()
    {
        if (_enforcingInstallationRule)
            return;

        var requiredInstallKey = GetRequiredInstallationItemKey();
        if (string.IsNullOrWhiteSpace(requiredInstallKey))
            return;
        if (!_catalog.ContainsKey(requiredInstallKey))
            return;

        var changed = false;
        _enforcingInstallationRule = true;
        try
        {
            var installationLines = Lines
                .Where(IsInstallationLine)
                .ToList();

            if (!installationLines.Any(l => IsItemKey(l, requiredInstallKey)))
            {
                AddLineFromCatalogKey(requiredInstallKey);
                changed = true;
                installationLines = Lines.Where(IsInstallationLine).ToList();
            }

            foreach (var line in installationLines)
            {
                if (IsItemKey(line, requiredInstallKey))
                {
                    if (!line.Selected)
                    {
                        line.Selected = true;
                        changed = true;
                    }
                    if (line.Qty <= 0m)
                    {
                        line.SetSuggestedQty(1m);
                        changed = true;
                    }
                    continue;
                }

                line.LineChanged -= OnLineChanged;
                Lines.Remove(line);
                changed = true;
            }
        }
        finally
        {
            _enforcingInstallationRule = false;
        }

        if (changed)
            UpdateTotal();
    }

    private string? GetRequiredInstallationItemKey()
    {
        var descriptor = $"{MeasureId} {MeasureName}";
        if (descriptor.Contains("GFK", StringComparison.OrdinalIgnoreCase))
            return InstallUvAnlageKey;
        if (descriptor.Contains("NADELFILZ", StringComparison.OrdinalIgnoreCase))
            return InstallHlAnlageKey;

        return null;
    }

    private static bool IsInstallationLine(CostLineVm? line)
    {
        if (line is null)
            return false;

        if (!string.IsNullOrWhiteSpace(line.Group) &&
            line.Group.Trim().Equals("Installation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsInstallationItemKey(line.ItemKey);
    }

    private static bool IsItemKey(CostLineVm? line, string key)
    {
        if (line is null || string.IsNullOrWhiteSpace(line.ItemKey))
            return false;

        return line.ItemKey.Trim().Equals(key, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInstallationItemKey(string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        var key = itemKey.Trim();
        return key.StartsWith("INSTALL_", StringComparison.OrdinalIgnoreCase)
               || key.StartsWith("HL_INSTALL_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
            return true;

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        var normalized = raw.Contains(',')
            ? raw.Replace(',', '.')
            : raw.Replace('.', ',');

        if (!string.Equals(normalized, raw, StringComparison.Ordinal) &&
            decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        value = 0;
        return false;
    }

    private static int GetGroupOrder(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return GroupOrder.Length + 1;

        var trimmed = group.Trim();
        var idx = Array.FindIndex(GroupOrder, g => string.Equals(g, trimmed, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx : GroupOrder.Length + 1;
    }
}
