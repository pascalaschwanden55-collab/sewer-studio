using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.Collections;
using static AuswertungPro.Next.Infrastructure.Costs.CostCalculatorLogicService;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class MeasureBlockVm : ObservableObject
{
    private IReadOnlyDictionary<string, CostCatalogItem> _catalog;
    private readonly Dictionary<string, int> _templateLineOrderByItemKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly CostCalculatorMeasureInputStateController _inputState = new();
    private bool _applyingPrices;
    private bool _enforcingInstallationRule;
    private bool _enforcingEndManschetteRule;

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

        if (template is not null)
        {
            for (var i = 0; i < template.Lines.Count; i++)
            {
                var itemKey = template.Lines[i].ItemKey?.Trim();
                if (!string.IsNullOrWhiteSpace(itemKey) && !_templateLineOrderByItemKey.ContainsKey(itemKey))
                    _templateLineOrderByItemKey[itemKey] = i;
            }

            var ordered = CostCalculatorLineOrderController.OrderTemplateLines(template.Lines);

            foreach (var line in ordered)
                Lines.Add(CreateLine(line));
        }

        AttachLines();
        EnforceInstallationRule();
        UpdateTotal();
    }

    public void LoadFrom(MeasureCost measure)
    {
        _inputState.ApplyDnText(measure.Dn?.ToString() ?? "", value => DnText = value);

        _inputState.ApplyLengthText(measure.LengthMeters?.ToString("0.00") ?? "", value => LengthText = value);

        _inputState.ApplyConnectionsText("", value => ConnectionsText = value);

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
                IsQtyOverridden = line.IsQtyOverridden,
                PriceHint = line.PriceHint
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
        var invalidLine = Lines.FirstOrDefault(
            line => line.Selected && (line.Qty < 0m || line.UnitPrice < 0m));
        if (invalidLine is not null)
        {
            throw new InvalidOperationException(
                $"Die ausgewaehlte Kostenposition '{invalidLine.Text}' enthaelt eine negative Menge oder einen negativen Preis.");
        }

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
            IsQtyOverridden = l.IsQtyOverridden,
            PriceHint = l.PriceHint
        }).ToList();

        var meterLinesHaveValidLength = SelectedMeterLinesHavePositiveLength();
        var total = lines
            .Where(l => l.Selected && (meterLinesHaveValidLength || !IsMeterUnit(l.Unit)))
            .Sum(l => l.Qty * l.UnitPrice);

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
            _inputState.ApplyDnText(dn, value => DnText = value);
            ApplyCatalogPrices();
            EnforceEndManschetteRule();
        }
    }

    public void SetLengthFromImport(string length)
    {
        if (string.IsNullOrWhiteSpace(LengthText)) // Only set if not already manually entered
        {
            _inputState.ApplyLengthText(length, value => LengthText = value);
            ApplyLengthToLines();
        }
    }

    public void SetConnectionsFromImport(string connections)
    {
        if (string.IsNullOrWhiteSpace(ConnectionsText)) // Only set if not already manually entered
        {
            _inputState.ApplyConnectionsText(connections, value => ConnectionsText = value);
            ApplyConnectionsToLines();
        }
    }

    partial void OnDnTextChanged(string value)
    {
        if (!_inputState.ShouldHandleDnTextChange())
            return;

        ApplyCatalogPrices();
        EnforceEndManschetteRule();
    }

    partial void OnLengthTextChanged(string value)
    {
        if (!_inputState.ShouldHandleLengthTextChange())
            return;

        ApplyLengthToLines();
    }

    partial void OnConnectionsTextChanged(string value)
    {
        if (!_inputState.ShouldHandleConnectionsTextChange())
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
                var candidates = item.DnPrices
                    .Where(x => dn >= x.DnFrom && dn <= x.DnTo)
                    .ToList();
                var usedNearestFallback = false;
                if (candidates.Count == 0)
                {
                    candidates = FindNearestDnCandidates(item.DnPrices, dn.Value);
                    usedNearestFallback = candidates.Count > 0;
                }

                var match = candidates.FirstOrDefault();
                vm.SetSuggestedPrice(match?.Price, match is not null,
                    usedNearestFallback && match is not null ? BuildNearestDnPriceHint(match) : "");
            }
        }

        var connections = ParseDecimal(ConnectionsText);
        if (connections is not null && IsConnectionLine(vm))
        {
            // Historischer Sonderweg: Qty = 1 markiert eine neue Zeile bereits als manuell.
            // Sie uebernimmt die Anschlusszahl trotzdem und behaelt dieses Override-Flag.
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
        => ObservableCollectionOrderController.TryMoveByOffset(Lines, line, -1);

    private void MoveLineDown(CostLineVm? line)
        => ObservableCollectionOrderController.TryMoveByOffset(Lines, line, 1);

    public void SortLines()
    {
        if (Lines.Count <= 1)
            return;

        var ordered = CostCalculatorLineOrderController.OrderLines(Lines, GetTemplateLineOrder);

        ObservableCollectionOrderController.Reorder(Lines, ordered);
    }

    private int GetTemplateLineOrder(string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return int.MaxValue;

        return _templateLineOrderByItemKey.TryGetValue(itemKey.Trim(), out var order)
            ? order
            : int.MaxValue;
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
        var meterLinesHaveValidLength = SelectedMeterLinesHavePositiveLength();
        Total = Lines
            .Where(l => l.Selected && (meterLinesHaveValidLength || !IsMeterUnit(l.Unit)))
            .Sum(l => l.LineTotal);
        BlockChanged?.Invoke();
    }

    private bool SelectedMeterLinesHavePositiveLength()
        => !Lines.Any(l => l.Selected && IsMeterUnit(l.Unit))
           || ParseDecimal(LengthText) is > 0m;

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
        if (length is null or <= 0m)
        {
            UpdateTotal();
            return;
        }

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

        foreach (var line in Lines)
            ApplyConnectionsToLine(line, connections.Value);

        UpdateTotal();
    }

    private static void ApplyConnectionsToLine(CostLineVm line, decimal connections)
    {
        var update = ConnectionQuantityPolicy.Evaluate(
            line.ItemKey,
            line.Text,
            line.Qty,
            line.Selected,
            connections);
        if (!update.IsApplicable)
            return;

        if (update.ShouldDisable)
        {
            line.SetSuggestedQty(ConnectionQuantityPolicy.ResolveSuggestedQuantity(
                update,
                line.IsQtyOverridden) ?? 0m);
            line.IsQtyOverridden = false;
            line.Selected = false;
            line.TransferMarked = false;
            return;
        }

        if (update.ShouldReactivate)
            line.Selected = true;

        if (ConnectionQuantityPolicy.ResolveSuggestedQuantity(
                update,
                line.IsQtyOverridden) is { } quantity)
            line.SetSuggestedQty(quantity);
    }

    private void TryInitializeConnectionsFromLines()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionsText))
            return;

        // Snapshot der VM-Zeilen als Domain-Objekte fuer die Engine.
        var snapshot = Lines.Select(l => new CostLine
        {
            ItemKey = l.ItemKey ?? "",
            Text = l.Text ?? "",
            Qty = l.Qty,
            Selected = l.Selected
        });

        var qty = MeasurePricingEngine.TryReadConnectionsFromLines(snapshot);
        if (qty is null)
            return;

        _inputState.ApplyConnectionsText(
            qty.Value.ToString(CultureInfo.InvariantCulture),
            value => ConnectionsText = value);
    }

    private void ApplyCatalogPricesInternal(bool onlyQtyBased)
    {
        if (_applyingPrices)
            return;

        _applyingPrices = true;
        try
        {
            // Snapshot der VM-Zeilen als Domain-Objekte uebergeben; Engine arbeitet stateless.
            var dn = ParseDn(DnText);
            var snapshot = Lines.Select(l => new CostLine
            {
                ItemKey = l.ItemKey ?? "",
                Text = l.Text ?? "",
                Unit = l.Unit ?? "",
                Qty = l.Qty,
                IsPriceOverridden = l.IsPriceOverridden
            }).ToList();

            var results = MeasurePricingEngine.ComputePrices(snapshot, _catalog, dn, onlyQtyBased);

            // Ergebnisse in die VM-Zeilen zurueckspiegeln (SetSuggestedPrice behaelt
            // die re-entrancy-sichere Semantik bei: LineChanged ausloesen, aber _applyingPrices
            // blockiert den Rueckruf in OnLineChanged).
            for (var i = 0; i < Lines.Count && i < results.Count; i++)
            {
                if (results[i] is { } r)
                    Lines[i].SetSuggestedPrice(r.HasPrice ? r.UnitPrice : null, r.HasPrice, r.PriceHint);
            }
        }
        finally
        {
            _applyingPrices = false;
        }

        UpdatePriceHint();
        UpdateTotal();
    }

    private static bool IsConnectionLine(CostLineVm line)
        => CostCalculatorLogicService.IsConnectionLine(line?.ItemKey, line?.Text);

    private void EnforceInstallationRule()
    {
        if (_enforcingInstallationRule)
            return;

        var requiredInstallKey = GetRequiredInstallationItemKey();
        if (string.IsNullOrWhiteSpace(requiredInstallKey))
            return;
        if (!_catalog.ContainsKey(requiredInstallKey))
            return;

        // Snapshot der VM-Zeilen fuer die Engine (Domain-Typen).
        var snapshot = Lines.Select(l => new CostLine
        {
            ItemKey = l.ItemKey ?? "",
            Group = l.Group ?? "",
            Qty = l.Qty,
            Selected = l.Selected
        }).ToList();

        MeasureRuleService.EnforceInstallationRule(
            snapshot, _catalog, requiredInstallKey,
            out var domainLinesToRemove, out var domainLineToAdd, out var changed);

        if (!changed)
            return;

        _enforcingInstallationRule = true;
        try
        {
            // Zeilen entfernen: per ItemKey im VM finden.
            var removeKeys = domainLinesToRemove.Select(l => l.ItemKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toRemove = Lines
                .Where(l => removeKeys.Contains(l.ItemKey ?? ""))
                .ToList();
            foreach (var line in toRemove)
            {
                line.LineChanged -= OnLineChanged;
                Lines.Remove(line);
            }

            // Zeile hinzufuegen: AddLineFromCatalogKey haelt Event-Subscriptions und Preise korrekt.
            if (domainLineToAdd is not null &&
                !Lines.Any(l => IsItemKey(l, domainLineToAdd.ItemKey)))
            {
                AddLineFromCatalogKey(domainLineToAdd.ItemKey);
            }

            // Pflicht-Zeile reaktivieren, falls sie deaktiviert war.
            var requiredLine = Lines.FirstOrDefault(l => IsItemKey(l, requiredInstallKey));
            if (requiredLine is not null)
            {
                if (!requiredLine.Selected)
                    requiredLine.Selected = true;
                if (requiredLine.Qty <= 0m)
                    requiredLine.SetSuggestedQty(1m);
            }
        }
        finally
        {
            _enforcingInstallationRule = false;
        }

        UpdateTotal();
    }

    // Linerende-Manschette nur ab DN 200 automatisch (2 Stk = Anfang + Ende).
    // Unter DN 200 wird die Endmanschetten-Zeile deaktiviert. Manuelle Mengen
    // (IsQtyOverridden) bleiben unangetastet; bei unbekanntem DN greift keine Regel.
    private void EnforceEndManschetteRule()
    {
        if (_enforcingEndManschetteRule)
            return;

        var dn = ParseDn(DnText);

        // Snapshot fuer die Engine bauen.
        var snapshot = Lines.Select(l => new CostLine
        {
            ItemKey = l.ItemKey ?? "",
            Selected = l.Selected,
            Qty = l.Qty,
            IsQtyOverridden = l.IsQtyOverridden
        }).ToList();

        MeasureRuleService.EnforceEndManschetteRule(snapshot, dn, out var changed);
        if (!changed)
            return;

        // Ergebnisse in die VM-Zeilen zurueckspiegeln.
        _enforcingEndManschetteRule = true;
        try
        {
            for (var i = 0; i < Lines.Count && i < snapshot.Count; i++)
            {
                var s = snapshot[i];
                var vm = Lines[i];

                if (vm.Selected != s.Selected)
                    vm.Selected = s.Selected;

                if (vm.IsQtyOverridden != s.IsQtyOverridden)
                    vm.IsQtyOverridden = s.IsQtyOverridden;

                // SetSuggestedQty verwenden, damit IsQtyOverridden nicht ungewollt gesetzt wird.
                if (vm.Qty != s.Qty)
                    vm.SetSuggestedQty(s.Qty);
            }
        }
        finally
        {
            _enforcingEndManschetteRule = false;
        }

        UpdateTotal();
    }

    private string? GetRequiredInstallationItemKey()
        => MeasureRuleService.GetRequiredInstallationItemKey(MeasureId, MeasureName);

    private static bool IsInstallationLine(CostLineVm? line)
        => CostCalculatorLogicService.IsInstallationLine(line?.Group, line?.ItemKey);

    private static bool IsItemKey(CostLineVm? line, string key)
        => CostCalculatorLogicService.IsItemKey(line?.ItemKey, key);

}
