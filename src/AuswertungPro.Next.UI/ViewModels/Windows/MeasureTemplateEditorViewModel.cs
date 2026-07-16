using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using LegacyMeasureTemplate = AuswertungPro.Next.Domain.Models.Costs.MeasureTemplate;
using LegacyMeasureTemplates = AuswertungPro.Next.Domain.Models.Costs.MeasureTemplates;
using LegacyPriceItem = AuswertungPro.Next.Domain.Models.Costs.PriceItem;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class MeasureTemplateEditorViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions LegacyJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IMeasureTemplateStore _templateStore;
    private readonly ICostCatalogStore _catalogStore;
    private readonly IDialogService _dialogs;
    private readonly string? _projectPath;
    private readonly string _legacyTemplatePath;
    private readonly string _activeUserTemplatePath;
    private MeasureTemplateCatalog _templates;
    private CostCatalog _catalog;

    public ObservableCollection<TemplateRow> Templates { get; } = new();
    public ObservableCollection<CatalogItemRow> AvailablePrices { get; } = new();
    public ObservableCollection<TemplateLineRow> CurrentLines { get; } = new();

    [ObservableProperty] private TemplateRow? _selectedTemplate;
    [ObservableProperty] private TemplateLineRow? _selectedLine;
    [ObservableProperty] private CatalogItemRow? _selectedAvailablePrice;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private string _templateId = string.Empty;
    [ObservableProperty] private string _templateName = string.Empty;
    [ObservableProperty] private string _templateDescription = string.Empty;

    public IRelayCommand AddCatalogItemCommand { get; }
    public IRelayCommand RemoveCatalogItemCommand { get; }
    public IRelayCommand SaveCatalogCommand { get; }

    [Obsolete("Uebergangskonstruktor. Neue Aufrufer sollen die Vorlagen-Speicher injizieren.")]
    public MeasureTemplateEditorViewModel(
        string? projectPath = null,
        IDialogService? dialogs = null,
        MeasureTemplateStore? templateStore = null,
        CostCatalogStore? catalogStore = null,
        string? legacyTemplatePath = null,
        string? activeUserTemplatePath = null)
        : this(
            projectPath,
            templateStore ?? CostStoreCompatibility.Factory.CreateMeasureTemplateStore(),
            catalogStore ?? CostStoreCompatibility.Factory.CreateCostCatalogStore(),
            dialogs,
            legacyTemplatePath,
            activeUserTemplatePath)
    {
    }

    public MeasureTemplateEditorViewModel(
        string? projectPath,
        IMeasureTemplateStore templateStore,
        ICostCatalogStore catalogStore,
        IDialogService? dialogs = null,
        string? legacyTemplatePath = null,
        string? activeUserTemplatePath = null)
    {
        _projectPath = projectPath;
        _dialogs = dialogs ?? new DialogService();
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        _legacyTemplatePath = legacyTemplatePath ?? ResolveLegacyTemplatePath();
        _activeUserTemplatePath = activeUserTemplatePath ?? ResolveActiveUserTemplatePath();

        TryOfferLegacyMigration();

        _templates = _templateStore.LoadMerged(projectPath);
        _catalog = _catalogStore.LoadMerged(projectPath);
        WarnDuplicateNpkCodes();

        LoadTemplates();
        LoadAvailablePrices();

        AddCatalogItemCommand = new RelayCommand(AddCatalogItem);
        RemoveCatalogItemCommand = new RelayCommand(RemoveCatalogItem, () => SelectedAvailablePrice is not null);
        SaveCatalogCommand = new RelayCommand(SaveCatalog);

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedTemplate))
                LoadTemplateLines();
            if (e.PropertyName == nameof(SearchText))
                FilterPrices();
            if (e.PropertyName == nameof(SelectedAvailablePrice))
                RemoveCatalogItemCommand.NotifyCanExecuteChanged();
        };
    }

    [Obsolete("Nur fuer alten Test-/Aufrufer-Code. Der Editor nutzt intern Application-Vertraege.")]
    public MeasureTemplateEditorViewModel(
        Infrastructure.Costs.CostCalculationService _,
        IDialogService? dialogs = null)
        : this(
            projectPath: null,
            dialogs: dialogs)
    {
    }

    private void LoadTemplates()
    {
        Templates.Clear();
        foreach (var t in _templates.Measures)
            Templates.Add(new TemplateRow(t));
    }

    private void LoadAvailablePrices()
    {
        AvailablePrices.Clear();
        foreach (var item in _catalog.Items)
            AvailablePrices.Add(new CatalogItemRow(item));
    }

    private void FilterPrices()
    {
        foreach (var row in AvailablePrices)
        {
            var match = string.IsNullOrWhiteSpace(SearchText) ||
                        row.Label.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        row.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        row.Group.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            row.IsVisible = match;
        }
    }

    private void LoadTemplateLines()
    {
        CurrentLines.Clear();
        if (SelectedTemplate == null) return;

        TemplateId = SelectedTemplate.Template.Id;
        TemplateName = SelectedTemplate.Template.Name;
        TemplateDescription = "";

        foreach (var line in SelectedTemplate.Template.Lines)
        {
            var priceItem = _catalog.Items.FirstOrDefault(i =>
                string.Equals(i.Key, line.ItemKey, StringComparison.OrdinalIgnoreCase));
            var label = priceItem?.Name ?? line.ItemKey;
            var unit = priceItem?.Unit ?? "Stk";
            var qtyStr = line.DefaultQty.ToString("0.###", CultureInfo.InvariantCulture);

            CurrentLines.Add(new TemplateLineRow(
                line.Group,
                label,
                unit,
                qtyStr,
                line.ItemKey));
        }
    }

    [RelayCommand]
    private void NewTemplate()
    {
        TemplateId = MeasureEditorIdPolicy.NewTemplateId(Templates.Count);
        TemplateName = "Neue Massnahme";
        TemplateDescription = string.Empty;
        CurrentLines.Clear();
        SelectedTemplate = null;
    }

    [RelayCommand]
    private void SaveTemplate()
    {
        if (string.IsNullOrWhiteSpace(TemplateId) || string.IsNullOrWhiteSpace(TemplateName))
        {
            _dialogs.Warn("ID und Name muessen ausgefuellt sein.", "Hinweis");
            return;
        }

        var template = new MeasureTemplate
        {
            Id = TemplateId,
            Name = TemplateName,
            Lines = CurrentLines.Select(r => new MeasureLineTemplate
            {
                Group = r.Group,
                ItemKey = r.ItemRef,
                Enabled = true,
                DefaultQty = ParseQtyOrDefault(r.Qty)
            }).ToList()
        };

        if (!_templateStore.UpsertUserTemplate(template, out var error))
        {
            _dialogs.Error($"Template konnte nicht gespeichert werden: {error}", "Vorlagen");
            return;
        }

        ReloadTemplatesFromStore();
        SelectedTemplate = Templates.FirstOrDefault(t =>
            string.Equals(t.Id, template.Id, StringComparison.OrdinalIgnoreCase));

        _dialogs.Info("Template gespeichert. Die Sanierungs-Matrix liest diese Vorlage.", "OK");
    }

    [RelayCommand]
    private void DeleteTemplate()
    {
        if (SelectedTemplate == null) return;

        var confirmed = _dialogs.Confirm(
            $"Template '{SelectedTemplate.Name}' wirklich loeschen/deaktivieren?",
            "Bestaetigen");

        if (!confirmed) return;

        var disabled = CloneTemplate(SelectedTemplate.Template);
        disabled.Disabled = true;
        if (!_templateStore.UpsertUserTemplate(disabled, out var error))
        {
            _dialogs.Error($"Template konnte nicht deaktiviert werden: {error}", "Vorlagen");
            return;
        }

        ReloadTemplatesFromStore();
        NewTemplate();
    }

    [RelayCommand]
    private void AddLine(CatalogItemRow? priceRow)
    {
        if (priceRow == null) return;

        var line = new TemplateLineRow(
            priceRow.Group,
            priceRow.Label,
            priceRow.Unit,
            "1",
            priceRow.Id);

        CurrentLines.Add(line);
    }

    [RelayCommand]
    private void DeleteLine()
    {
        if (SelectedLine != null)
            CurrentLines.Remove(SelectedLine);
    }

    [RelayCommand]
    private void MoveLineUp()
    {
        if (SelectedLine == null) return;
        var idx = CurrentLines.IndexOf(SelectedLine);
        if (idx > 0)
            CurrentLines.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveLineDown()
    {
        if (SelectedLine == null) return;
        var idx = CurrentLines.IndexOf(SelectedLine);
        if (idx < CurrentLines.Count - 1)
            CurrentLines.Move(idx, idx + 1);
    }

    private void AddCatalogItem()
    {
        var id = CreateNewCatalogId();
        var item = new CostCatalogItem
        {
            Key = id,
            Name = "Neue Position",
            Unit = "m",
            Type = "Fixed",
            Price = 0m,
            Active = true
        };

        var row = new CatalogItemRow(item);
        AvailablePrices.Add(row);
        SelectedAvailablePrice = row;
        FilterPrices();
    }

    private void RemoveCatalogItem()
    {
        if (SelectedAvailablePrice is null)
            return;

        var label = string.IsNullOrWhiteSpace(SelectedAvailablePrice.Label)
            ? SelectedAvailablePrice.Id
            : SelectedAvailablePrice.Label;

        var confirmed = _dialogs.Confirm(
            $"Position '{label}' wirklich loeschen?",
            "Position loeschen");

        if (!confirmed)
            return;

        AvailablePrices.Remove(SelectedAvailablePrice);
        SelectedAvailablePrice = null;
    }

    private void SaveCatalog()
    {
        _catalog.Items = AvailablePrices
            .Where(r => !string.IsNullOrWhiteSpace(r.Id))
            .Select(r => r.Item)
            .ToList();
        WarnDuplicateNpkCodes();

        if (!_catalogStore.SaveUserOverrides(_catalog, _projectPath, out var error))
        {
            _dialogs.Error($"Positionen konnten nicht gespeichert werden: {error}", "Positionen");
            return;
        }

        _dialogs.Info("Positionen gespeichert. Die Sanierungs-Matrix nutzt denselben Katalog.", "OK");
    }

    private void WarnDuplicateNpkCodes()
    {
        var warnings = CostCatalogStore.FindDuplicateNpkCodesWithDifferentUnits(_catalog);
        if (warnings.Count == 0)
            return;

        var lines = warnings.Select(w =>
            $"{w.NpkCode}: Einheiten {string.Join(", ", w.Units)} ({string.Join(", ", w.ItemKeys)})");
        _dialogs.Warn(
            "Der Katalog enthaelt gleiche NPK-Nummern mit unterschiedlichen Einheiten:\n\n" +
            string.Join("\n", lines) +
            "\n\nBitte fachlich pruefen; Speichern wird nicht blockiert.",
            "NPK-Katalog");
    }

    private void ReloadTemplatesFromStore()
    {
        _templates = _templateStore.LoadMerged(_projectPath);
        LoadTemplates();
    }

    private string CreateNewCatalogId()
    {
        var existingIds = AvailablePrices.Select(r => r.Id).ToList();
        return MeasureEditorIdPolicy.NewCatalogItemId(existingIds);
    }

    private void TryOfferLegacyMigration()
    {
        if (!File.Exists(_legacyTemplatePath))
            return;

        var legacyWrite = File.GetLastWriteTimeUtc(_legacyTemplatePath);
        var activeWrite = File.Exists(_activeUserTemplatePath)
            ? File.GetLastWriteTimeUtc(_activeUserTemplatePath)
            : DateTime.MinValue;

        if (activeWrite >= legacyWrite)
            return;

        var migrate = _dialogs.Confirm(
            "Alte Vorlagen-Bearbeitungen wurden gefunden.\n\n" +
            "Diese alte Datei wurde bisher vom Editor beschrieben, aber von Matrix und Kostenrechner nicht gelesen.\n" +
            "Jetzt in die aktive Vorlagen-Datei uebernehmen?",
            "Vorlagen uebernehmen");

        if (!migrate)
            return;

        try
        {
            var json = File.ReadAllText(_legacyTemplatePath);
            var legacy = JsonSerializer.Deserialize<LegacyMeasureTemplates>(json, LegacyJsonOptions)
                         ?? new LegacyMeasureTemplates();
            var converted = ConvertLegacyTemplates(legacy);
            var overrides = _templateStore.LoadUserOverrides();

            foreach (var template in converted.Measures)
            {
                var existing = overrides.Measures.FirstOrDefault(t =>
                    string.Equals(t.Id, template.Id, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                    overrides.Measures.Remove(existing);
                overrides.Measures.Add(template);
            }

            if (!_templateStore.SaveUserOverrides(overrides, out var error))
            {
                _dialogs.Error($"Alte Vorlagen konnten nicht uebernommen werden: {error}", "Vorlagen");
                return;
            }

            _dialogs.Info("Alte Vorlagen wurden in die aktive Vorlagen-Datei uebernommen.", "Vorlagen");
        }
        catch (Exception ex)
        {
            _dialogs.Error(
                $"Alte Vorlagen konnten nicht gelesen werden:\n{UserError.DescribeAndReport(ex, "Alte Vorlagen lesen")}",
                "Vorlagen");
        }
    }

    private static MeasureTemplateCatalog ConvertLegacyTemplates(LegacyMeasureTemplates legacy)
    {
        var catalog = new MeasureTemplateCatalog { Version = Math.Max(1, legacy.SchemaVersion) };
        foreach (var legacyTemplate in legacy.Templates ?? new())
        {
            var template = ConvertLegacyTemplate(legacyTemplate);
            if (!string.IsNullOrWhiteSpace(template.Id))
                catalog.Measures.Add(template);
        }

        return catalog;
    }

    private static MeasureTemplate ConvertLegacyTemplate(LegacyMeasureTemplate legacy)
    {
        var template = new MeasureTemplate
        {
            Id = legacy.Id?.Trim() ?? "",
            Name = string.IsNullOrWhiteSpace(legacy.Name) ? legacy.Id?.Trim() ?? "" : legacy.Name.Trim()
        };

        foreach (var line in legacy.Lines ?? new())
        {
            if (string.IsNullOrWhiteSpace(line.ItemRef))
                continue;

            template.Lines.Add(new MeasureLineTemplate
            {
                Group = line.Group?.Trim() ?? "",
                ItemKey = line.ItemRef.Trim(),
                Enabled = true,
                DefaultQty = ParseLegacyQtyOrDefault(line.Qty)
            });
        }

        return template;
    }

    private static decimal ParseLegacyQtyOrDefault(JsonElement qty)
    {
        if (qty.ValueKind == JsonValueKind.Number && qty.TryGetDecimal(out var number))
            return number;
        if (qty.ValueKind == JsonValueKind.String)
            return ParseQtyOrDefault(qty.GetString());
        return 1m;
    }

    private static decimal ParseQtyOrDefault(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 1m;

        var text = raw.Trim().Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var qty)
            ? qty
            : 1m;
    }

    private static MeasureTemplate CloneTemplate(MeasureTemplate template)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Disabled = template.Disabled,
            Lines = template.Lines.Select(line => new MeasureLineTemplate
            {
                Group = line.Group,
                ItemKey = line.ItemKey,
                Enabled = line.Enabled,
                DefaultQty = line.DefaultQty
            }).ToList()
        };

    private static string ResolveLegacyTemplatePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AuswertungPro", "legacy_costs", "measure_templates.json");
    }

    private static string ResolveActiveUserTemplatePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AuswertungPro", "measure_templates.user.json");
    }
}

public sealed partial class TemplateRow : ObservableObject
{
    public MeasureTemplate Template { get; }
    public string Id => Template.Id;
    public string Name => Template.Disabled ? $"{Template.Name} (deaktiviert)" : Template.Name;

    public TemplateRow(MeasureTemplate template)
    {
        Template = template;
    }
}

public sealed class CatalogItemRow : ObservableObject
{
    public CostCatalogItem Item { get; }

    public string Id
    {
        get => Item.Key;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            if (string.Equals(Item.Key, next, StringComparison.Ordinal)) return;
            Item.Key = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Group));
        }
    }

    public string Group
    {
        get => CatalogItemGrouping.DeriveGroupFromKey(Item.Key ?? "");
        set
        {
            // Die aktive Katalogstruktur speichert keine freie Gruppe mehr.
            OnPropertyChanged();
        }
    }

    public string Label
    {
        get => Item.Name;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            if (string.Equals(Item.Name, next, StringComparison.Ordinal)) return;
            Item.Name = next;
            OnPropertyChanged();
        }
    }

    public string Unit
    {
        get => Item.Unit;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            if (string.Equals(Item.Unit, next, StringComparison.Ordinal)) return;
            Item.Unit = next;
            OnPropertyChanged();
        }
    }

    public decimal Price
    {
        get => Item.Price ?? 0m;
        set
        {
            if (Item.Price == value) return;
            Item.Price = value;
            if (string.IsNullOrWhiteSpace(Item.Type))
                Item.Type = "Fixed";
            OnPropertyChanged();
        }
    }

    public bool IsVisible { get; set; } = true;

    public CatalogItemRow(CostCatalogItem item)
    {
        Item = item;
    }
}

public sealed partial class TemplateLineRow : ObservableObject
{
    [ObservableProperty] private string _group;
    [ObservableProperty] private string _label;
    [ObservableProperty] private string _unit;
    [ObservableProperty] private string _qty;
    [ObservableProperty] private string _itemRef;

    public TemplateLineRow(string group, string label, string unit, string qty, string itemRef)
    {
        _group = group;
        _label = label;
        _unit = unit;
        _qty = qty;
        _itemRef = itemRef;
    }
}
