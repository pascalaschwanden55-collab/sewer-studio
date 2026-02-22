using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.UI.Dialogs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class CostCalculatorViewModel : ObservableObject
{
    private readonly CostCatalogStore _catalogStore = new();
    private readonly MeasureTemplateStore _templateStore = new();
    private readonly ProjectCostStoreRepository _costRepo = new();
    private readonly Action<HoldingCost>? _applyTotal;
    private readonly string? _projectPath;
    private readonly Dictionary<string, CostCatalogItem> _catalogItems;
    private readonly Dictionary<string, MeasureTemplate> _templateItems;
    private readonly Dictionary<string, string> _ownerByHolding = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedMeasureIds = new(StringComparer.OrdinalIgnoreCase);
    private ProjectCostStore _store = new();
    private readonly decimal _vatRate;

    public string Holding { get; }
    public DateTime? Date { get; }
    public string Header => string.IsNullOrWhiteSpace(Holding) ? "Kostenberechnung" : $"Kostenberechnung - {Holding}";

    public ObservableCollection<MeasureTemplateListItem> Measures { get; }
    public ObservableCollection<MeasureBlockVm> SelectedMeasures { get; } = new();
    public IReadOnlyList<string> InitialMeasureIds { get; }

    [ObservableProperty] private decimal _total;
    [ObservableProperty] private decimal _mwstAmount;
    [ObservableProperty] private decimal _totalInclMwst;
    [ObservableProperty] private string _catalogSearchText = "";
    public string MwstLabel => $"MWST {_vatRate * 100:0.0}%:";

    /// <summary>All active catalog items for the drag-source panel.</summary>
    public List<CatalogItemOption> AllCatalogItems { get; }
    public ObservableCollection<CatalogItemOption> FilteredCatalogItems { get; } = new();

    public IRelayCommand ApplyMeasuresCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand ApplyTotalCommand { get; }
    public IRelayCommand EditPositionTemplatesCommand { get; }
    public IRelayCommand<MeasureBlockVm> RemoveMeasureCommand { get; }
    public IRelayCommand<MeasureBlockVm> MoveMeasureUpCommand { get; }
    public IRelayCommand<MeasureBlockVm> MoveMeasureDownCommand { get; }
    public IRelayCommand<MeasureBlockVm> SaveTemplateCommand { get; }
    public IAsyncRelayCommand<Window?> ExportPdfCommand { get; }

    public event Action? Saved;

    public CostCalculatorViewModel(
        string holding,
        DateTime? date,
        IReadOnlyList<string> recommendedTokens,
        string? projectPath,
        Action<HoldingCost>? applyTotal = null,
        HaltungRecord? haltungRecord = null,
        IReadOnlyList<HaltungRecord>? projectRecords = null)
    {
        Holding = holding;
        Date = date;
        _projectPath = projectPath;
        _applyTotal = applyTotal;

        var catalog = _catalogStore.LoadMerged(projectPath);
        _catalogItems = catalog.Items.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        _vatRate = catalog.VatRate > 0 ? catalog.VatRate : 0.081m;

        var templates = _templateStore.LoadMerged(projectPath);
        _templateItems = templates.Measures.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        Measures = new ObservableCollection<MeasureTemplateListItem>(
            templates.Measures.Select(t => new MeasureTemplateListItem(t)));

        // Build itemKey â†’ group lookup from all template lines
        var keyToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in templates.Measures)
            foreach (var line in t.Lines)
                if (!keyToGroup.ContainsKey(line.ItemKey))
                    keyToGroup[line.ItemKey] = line.Group;

        AllCatalogItems = _catalogItems.Values
            .Where(c => c.Active)
            .Select(c =>
            {
                var group = keyToGroup.TryGetValue(c.Key, out var g) ? g : DeriveGroupFromKey(c.Key);
                return new CatalogItemOption(c.Key, group, $"[{group}]  {c.Name}  ({c.Unit})");
            })
            .OrderBy(c => GetCatalogGroupOrder(c.Group))
            .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var ci in AllCatalogItems)
            FilteredCatalogItems.Add(ci);

        _store = _costRepo.Load(projectPath);
        InitializeOwnerLookup(projectRecords, haltungRecord);

        var existing = GetExistingCost();
        var recommendedIds = ResolveMeasureIds(recommendedTokens, templates.Measures, _catalogItems);
        var initialIds = existing is null ? recommendedIds : existing.Measures.Select(m => m.MeasureId).ToList();
        InitialMeasureIds = initialIds;

        // Initialize DN and Length from HaltungRecord if provided
        if (haltungRecord != null)
        {
            InitializeFromHaltungRecord(haltungRecord);
        }

        if (existing is not null)
        {
            LoadExisting(existing);
        }
        else if (initialIds.Count > 0)
        {
            foreach (var id in initialIds)
                TryAddMeasure(id, applyPrices: true);
        }

        ApplyMeasuresCommand = new RelayCommand(ApplySelectedMeasures);
        SaveCommand = new RelayCommand(Save);
        ApplyTotalCommand = new RelayCommand(ApplyTotal);
        EditPositionTemplatesCommand = new RelayCommand(EditPositionTemplates);
        RemoveMeasureCommand = new RelayCommand<MeasureBlockVm>(RemoveMeasure);
        MoveMeasureUpCommand = new RelayCommand<MeasureBlockVm>(MoveMeasureUp);
        MoveMeasureDownCommand = new RelayCommand<MeasureBlockVm>(MoveMeasureDown);
        SaveTemplateCommand = new RelayCommand<MeasureBlockVm>(SaveTemplate);
        ExportPdfCommand = new AsyncRelayCommand<Window?>(ExportPdfAsync);
        UpdateTotal();
    }

    public void SetSelectedMeasures(IEnumerable<MeasureTemplateListItem> measures)
    {
        _selectedMeasureIds.Clear();
        foreach (var m in measures)
        {
            if (m.Disabled)
                continue;
            _selectedMeasureIds.Add(m.Id);
        }
    }

    private void ApplySelectedMeasures()
    {
        if (_selectedMeasureIds.Count == 0)
            return;

        foreach (var id in _selectedMeasureIds)
            TryAddMeasure(id, applyPrices: true);

        UpdateTotal();
    }

    private bool TryAddMeasure(string id, bool applyPrices)
    {
        if (SelectedMeasures.Any(m => string.Equals(m.MeasureId, id, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!_templateItems.TryGetValue(id, out var template))
            return false;
        if (template.Disabled)
            return false;

        var block = new MeasureBlockVm(template, _catalogItems);
        block.BlockChanged += UpdateTotal;
        SelectedMeasures.Add(block);

        // Apply default DN and Length from import if available
        if (!string.IsNullOrWhiteSpace(DefaultDn))
            block.SetDnFromImport(DefaultDn);
        if (!string.IsNullOrWhiteSpace(DefaultLength))
            block.SetLengthFromImport(DefaultLength);
        if (!string.IsNullOrWhiteSpace(DefaultConnections))
            block.SetConnectionsFromImport(DefaultConnections);

        if (applyPrices)
            block.ApplyCatalogPrices();

        return true;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_projectPath))
        {
            MessageBox.Show("Projekt bitte speichern, um Kosten abzulegen.", "Kosten",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var key = Holding?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Haltungsname fehlt.", "Kosten",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var holdingCost = BuildHoldingCost(key);
        _store.ByHolding[key] = holdingCost;

        if (!_costRepo.Save(_projectPath, _store, out var error))
        {
            MessageBox.Show($"Speichern fehlgeschlagen: {error}", "Kosten",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Keep record fields in sync when user saves from the calculator window.
        _applyTotal?.Invoke(BuildHoldingCost(key));
        Saved?.Invoke();
    }

    private void ApplyTotal()
    {
        if (_applyTotal is null)
        {
            MessageBox.Show("Kosten/Massnahmen koennen hier nicht in die Zeile uebernommen werden.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _applyTotal(BuildHoldingCost(Holding));
    }

    private async Task ExportPdfAsync(Window? owner)
    {
        if (SelectedMeasures.Count == 0)
        {
            MessageBox.Show("Bitte zuerst Massnahmen hinzufuegen.", "PDF-Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sp = (ServiceProvider)App.Services;

        var safeName = SanitizeFilePart(Holding);
        var defaultName = $"Kostenzusammenstellung_{safeName}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = sp.Dialogs.SaveFile(
            "Kostenzusammenstellung als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            owner ??= System.Windows.Application.Current?.MainWindow;
            if (owner is not null) owner.Cursor = System.Windows.Input.Cursors.Wait;

            var holdingCost = BuildHoldingCost(Holding);
            var entries = BuildCostSummaryEntries(holdingCost);
            if (entries.Count == 0)
            {
                MessageBox.Show(
                    "Keine passenden Kostenpositionen gefunden.",
                    "PDF-Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int? dn = null;
            decimal? lengthM = null;
            foreach (var m in SelectedMeasures)
            {
                if (dn is null && int.TryParse(m.DnText?.Trim(), out var d))
                    dn = d;
                if (lengthM is null && decimal.TryParse(m.LengthText?.Trim(), out var l))
                    lengthM = l;
                if (dn.HasValue && lengthM.HasValue)
                    break;
            }

            var ctx = new OfferPdfContext
            {
                ProjectTitle = "Abwasser Uri - Kostenzusammenstellung",
                VariantTitle = $"Auswertung ({entries.Count} Haltung(en))",
                CustomerBlock = "",
                ObjectBlock = OfferPdfModelFactory.BuildObjectBlock(Holding, dn, lengthM, Date),
                FilterSummaryText = "Eigentuemer: Alle",
                Currency = "CHF",
                OfferNo = "",
                TextBlocks = new List<string>
                {
                    "Kosten je Massnahme: Nettobetraege fuer die aktuell ausgewaehlte Haltung.",
                    "Kostenzusammenstellung nach Eigentuemer und Gesamtpositionen fuer diese Haltung.",
                    "Diese Ausgabe ersetzt eine Offerte und dient als Kostenuebersicht."
                }
            };

            var model = OfferPdfModelFactory.CreateCostSummary(
                entries,
                ctx,
                DateTimeOffset.Now,
                includeOwnerSummary: true,
                includePositionSummary: true);

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "cost_summary.sbnhtml");
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");

            var renderer = new OfferHtmlToPdfRenderer();
            await renderer.RenderAsync(model, templatePath, output, logoPath);

            MessageBox.Show($"PDF-Kostenzusammenstellung wurde erstellt:\n{output}", "PDF-Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF konnte nicht erstellt werden:\n{ex.Message}", "PDF-Export",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (owner is not null) owner.Cursor = null;
        }
    }

    private static string SanitizeFilePart(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Unbekannt";

        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
        return clean.Trim();
    }

    private void InitializeOwnerLookup(
        IReadOnlyList<HaltungRecord>? projectRecords,
        HaltungRecord? haltungRecord)
    {
        _ownerByHolding.Clear();

        if (projectRecords is not null)
        {
            foreach (var record in projectRecords)
            {
                var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
                if (string.IsNullOrWhiteSpace(holding))
                    continue;

                var owner = (record.GetFieldValue("Eigentuemer") ?? "").Trim();
                if (string.IsNullOrWhiteSpace(owner))
                    continue;

                _ownerByHolding[holding] = owner;
            }
        }

        if (haltungRecord is not null)
        {
            var holding = (haltungRecord.GetFieldValue("Haltungsname") ?? "").Trim();
            var owner = (haltungRecord.GetFieldValue("Eigentuemer") ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(holding) && !string.IsNullOrWhiteSpace(owner))
                _ownerByHolding[holding] = owner;
        }
    }

    private List<CostSummaryEntry> BuildCostSummaryEntries(HoldingCost currentHoldingCost)
    {
        var currentHolding = (currentHoldingCost.Holding ?? "").Trim();
        if (string.IsNullOrWhiteSpace(currentHolding) || !HasSelectedLines(currentHoldingCost))
            return new List<CostSummaryEntry>();

        return new List<CostSummaryEntry>
        {
            new()
            {
                Holding = currentHolding,
                Owner = ResolveOwnerForHolding(currentHoldingCost.Holding),
                Cost = currentHoldingCost
            }
        };
    }

    private string ResolveOwnerForHolding(string? holding)
    {
        var key = (holding ?? "").Trim();
        if (key.Length == 0)
            return "Unbekannt";
        return _ownerByHolding.TryGetValue(key, out var owner) && !string.IsNullOrWhiteSpace(owner)
            ? owner.Trim()
            : "Unbekannt";
    }

    private static bool HasSelectedLines(HoldingCost cost)
    {
        if (cost is null)
            return false;
        return cost.Measures.Any(m => m.Lines.Any(l => l.Selected));
    }

    private HoldingCost BuildHoldingCost(string holding)
    {
        var measures = SelectedMeasures.Select(m => m.ToModel()).ToList();
        var total = measures.Sum(m => m.Total);
        var mwst = Math.Round(total * _vatRate, 2);

        return new HoldingCost
        {
            Holding = holding,
            Date = Date,
            Measures = measures,
            Total = total,
            MwstRate = _vatRate,
            MwstAmount = mwst,
            TotalInclMwst = Math.Round(total + mwst, 2)
        };
    }

    private void LoadExisting(HoldingCost cost)
    {
        SelectedMeasures.Clear();
        foreach (var measure in cost.Measures)
        {
            _templateItems.TryGetValue(measure.MeasureId, out var template);

            var block = new MeasureBlockVm(template, _catalogItems);
            block.LoadFrom(measure);
            block.BlockChanged += UpdateTotal;
            SelectedMeasures.Add(block);
        }
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        Total = SelectedMeasures.Sum(m => m.Total);
        MwstAmount = Math.Round(Total * _vatRate, 2);
        TotalInclMwst = Math.Round(Total + MwstAmount, 2);
    }

    partial void OnCatalogSearchTextChanged(string value)
    {
        FilteredCatalogItems.Clear();
        var filter = value?.Trim() ?? "";
        foreach (var item in AllCatalogItems)
        {
            if (filter.Length == 0 || item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                FilteredCatalogItems.Add(item);
        }
    }

    private static readonly string[] CatalogGroupOrder =
    {
        "Installation", "Vorarbeiten", "Hauptarbeit",
        "Qualitaetskontrolle", "Qualitaet",
        "Sonstiges"
    };

    private static int GetCatalogGroupOrder(string? group)
    {
        if (string.IsNullOrWhiteSpace(group)) return CatalogGroupOrder.Length + 1;
        var idx = Array.FindIndex(CatalogGroupOrder, g => string.Equals(g, group.Trim(), StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx : CatalogGroupOrder.Length;
    }

    internal static string DeriveGroupFromKey(string key)
    {
        if (key.StartsWith("INSTALL", StringComparison.OrdinalIgnoreCase)) return "Installation";
        if (key.StartsWith("VORARBEIT", StringComparison.OrdinalIgnoreCase)) return "Vorarbeiten";
        if (key.StartsWith("QK_", StringComparison.OrdinalIgnoreCase)) return "Qualitaetskontrolle";
        if (key.StartsWith("HAUPTARBEIT", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        // All Hauptarbeit items: Schlauchliner, LEM, Kurzliner, Manschette, Anschluss
        if (key.StartsWith("SCHLAUCHLINER", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        if (key.StartsWith("LINERENDMANSCHETTE", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        if (key.StartsWith("KURZLINER", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        if (key.StartsWith("MANSCHETTE", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        if (key.StartsWith("ANSCHLUSS", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        return "Sonstiges";
    }

    private void RemoveMeasure(MeasureBlockVm? measure)
    {
        if (measure == null)
            return;

        measure.BlockChanged -= UpdateTotal;
        SelectedMeasures.Remove(measure);
        UpdateTotal();
    }

    private void MoveMeasureUp(MeasureBlockVm? measure)
    {
        if (measure is null)
            return;

        var idx = SelectedMeasures.IndexOf(measure);
        if (idx <= 0)
            return;

        SelectedMeasures.Move(idx, idx - 1);
    }

    private void MoveMeasureDown(MeasureBlockVm? measure)
    {
        if (measure is null)
            return;

        var idx = SelectedMeasures.IndexOf(measure);
        if (idx < 0 || idx >= SelectedMeasures.Count - 1)
            return;

        SelectedMeasures.Move(idx, idx + 1);
    }

    private void SaveTemplate(MeasureBlockVm? measure)
    {
        if (measure is null)
            return;

        if (string.IsNullOrWhiteSpace(measure.MeasureId))
        {
            MessageBox.Show("Vorlagen-ID fehlt.", "Vorlage",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var template = new MeasureTemplate
        {
            Id = measure.MeasureId,
            Name = string.IsNullOrWhiteSpace(measure.MeasureName) ? measure.MeasureId : measure.MeasureName,
            Lines = measure.Lines.Select(l => new MeasureLineTemplate
            {
                Group = l.Group ?? "",
                ItemKey = l.ItemKey ?? "",
                Enabled = l.Selected,
                DefaultQty = l.Qty
            }).ToList()
        };

        if (!_templateStore.UpsertUserTemplate(template, out var error))
        {
            MessageBox.Show($"Speichern fehlgeschlagen: {error}", "Vorlage",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show("Vorlage gespeichert. Gilt fuer neue Projekte.", "Vorlage",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private HoldingCost? GetExistingCost()
    {
        if (string.IsNullOrWhiteSpace(Holding))
            return null;

        foreach (var kvp in _store.ByHolding)
        {
            if (string.Equals(kvp.Key, Holding, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }

    private static List<string> ResolveMeasureIds(
        IReadOnlyList<string> tokens,
        IReadOnlyList<MeasureTemplate> templates,
        IReadOnlyDictionary<string, CostCatalogItem> catalogItems)
    {
        if (tokens.Count == 0 || templates.Count == 0)
            return new List<string>();

        var normalizedTokens = tokens
            .Select(NormalizeToken)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedTokens.Count == 0)
            return new List<string>();

        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            if (template.Disabled)
                continue;

            var templateId = template.Id?.Trim() ?? "";
            if (templateId.Length == 0)
                continue;

            var templateIdNorm = NormalizeToken(templateId);
            var templateNameNorm = NormalizeToken(template.Name);
            var templateScore = 0;

            foreach (var token in normalizedTokens)
            {
                if (templateIdNorm.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                    templateNameNorm.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    templateScore += 100;
                    continue;
                }

                if (ContainsToken(templateIdNorm, token) || ContainsToken(templateNameNorm, token))
                    templateScore += 25;

                foreach (var line in template.Lines)
                {
                    var keyNorm = NormalizeToken(line.ItemKey);
                    if (keyNorm.Length > 0)
                    {
                        if (keyNorm.Equals(token, StringComparison.OrdinalIgnoreCase))
                            templateScore += 40;
                        else if (ContainsToken(keyNorm, token))
                            templateScore += 12;
                    }

                    if (!catalogItems.TryGetValue(line.ItemKey, out var item))
                        continue;

                    var itemNameNorm = NormalizeToken(item.Name);
                    if (itemNameNorm.Length > 0)
                    {
                        if (itemNameNorm.Equals(token, StringComparison.OrdinalIgnoreCase))
                            templateScore += 60;
                        else if (ContainsToken(itemNameNorm, token))
                            templateScore += 18;
                    }

                    if (item.Aliases is null)
                        continue;

                    foreach (var alias in item.Aliases)
                    {
                        var aliasNorm = NormalizeToken(alias);
                        if (aliasNorm.Length == 0)
                            continue;

                        if (aliasNorm.Equals(token, StringComparison.OrdinalIgnoreCase))
                            templateScore += 45;
                        else if (ContainsToken(aliasNorm, token))
                            templateScore += 12;
                    }
                }
            }

            if (templateScore > 0)
                scores[templateId] = templateScore;
        }

        var ranked = scores
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ranked.Count == 0)
            return new List<string>();

        var maxScore = ranked[0].Value;
        var minScore = Math.Max(25, (int)Math.Ceiling(maxScore * 0.4m));
        return ranked
            .Where(x => x.Value >= minScore)
            .Select(x => x.Key)
            .ToList();
    }

    private static bool ContainsToken(string text, string token)
    {
        if (text.Length == 0 || token.Length == 0)
            return false;
        if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            return true;

        // Only allow reverse contains for longer values to avoid noisy matches.
        return text.Length >= 5 && token.Length >= 5 &&
               token.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToken(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        while (text.Length > 0 && (text[0] == '-' || text[0] == '*'))
            text = text[1..].TrimStart();
        return text;
    }

    private void InitializeFromHaltungRecord(HaltungRecord haltungRecord)
    {
        // Set DN from import data
        var dnValue = haltungRecord.GetFieldValue("DN_mm");
        if (!string.IsNullOrWhiteSpace(dnValue) && int.TryParse(dnValue, out var dn))
        {
            foreach (var measure in SelectedMeasures)
            {
                measure.SetDnFromImport(dn.ToString());
            }
            DefaultDn = dn.ToString();
        }

        // Set Length from import data
        var lengthValue = haltungRecord.GetFieldValue("Haltungslaenge_m");
        if (!string.IsNullOrWhiteSpace(lengthValue) && decimal.TryParse(lengthValue, out var length))
        {
            foreach (var measure in SelectedMeasures)
            {
                measure.SetLengthFromImport(length.ToString("0.00"));
            }
            DefaultLength = length.ToString("0.00");
        }

        // Set connection count from explicit field (PDF/manual) or derive from damage coding.
        var connections = ConnectionCountEstimator.EstimateFromRecord(haltungRecord);
        if (connections is not null)
        {
            var connectionText = connections.Value.ToString(CultureInfo.InvariantCulture);
            foreach (var measure in SelectedMeasures)
            {
                measure.SetConnectionsFromImport(connectionText);
            }
            DefaultConnections = connectionText;
        }
    }

    // Store defaults for new measures added later
    public string? DefaultDn { get; private set; }
    public string? DefaultLength { get; private set; }
    public string? DefaultConnections { get; private set; }

    private void EditPositionTemplates()
    {
        var dialog = new CostCatalogEditorDialog(_projectPath);
        dialog.ShowDialog();
        // Always reload â€“ user may have saved changes
        ReloadCatalog();
    }

    private void RefreshMeasures()
    {
        var templates = _templateStore.LoadMerged(_projectPath);
        _templateItems.Clear();
        foreach (var template in templates.Measures)
        {
            _templateItems[template.Id] = template;
        }

        Measures.Clear();
        foreach (var template in templates.Measures.Select(t => new MeasureTemplateListItem(t)))
        {
            Measures.Add(template);
        }
    }

    private void ReloadCatalog()
    {
        var catalog = _catalogStore.LoadMerged(_projectPath);
        _catalogItems.Clear();
        foreach (var item in catalog.Items)
            _catalogItems[item.Key] = item;

        // Rebuild keyâ†’group lookup
        var keyToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _templateItems.Values)
            foreach (var line in t.Lines)
                if (!keyToGroup.ContainsKey(line.ItemKey))
                    keyToGroup[line.ItemKey] = line.Group;

        // Rebuild AllCatalogItems + FilteredCatalogItems
        AllCatalogItems.Clear();
        AllCatalogItems.AddRange(
            _catalogItems.Values
                .Where(c => c.Active)
                .Select(c =>
                {
                    var group = keyToGroup.TryGetValue(c.Key, out var g) ? g : DeriveGroupFromKey(c.Key);
                    return new CatalogItemOption(c.Key, group, $"[{group}]  {c.Name}  ({c.Unit})");
                })
                .OrderBy(c => GetCatalogGroupOrder(c.Group))
                .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase));

        FilteredCatalogItems.Clear();
        var filter = CatalogSearchText?.Trim() ?? "";
        foreach (var item in AllCatalogItems)
        {
            if (filter.Length == 0 || item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                FilteredCatalogItems.Add(item);
        }

        // Update each measure block
        foreach (var block in SelectedMeasures)
        {
            block.RefreshCatalog(_catalogItems);
            block.ApplyCatalogPrices();
        }
    }
}

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
    private bool _suppressDnUpdate;
    private bool _suppressLengthUpdate;
    private bool _suppressConnectionsUpdate;
    private bool _applyingPrices;

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

    private void OnLineChanged()
    {
        if (_applyingPrices)
            return;

        ApplyCatalogPricesInternal(onlyQtyBased: true);
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

public sealed partial class CostLineVm : ObservableObject
{
    private bool _suppressOverride;
    private bool _suppressQtyOverride;

    [ObservableProperty] private string _group = "";
    [ObservableProperty] private string _itemKey = "";
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _unit = "";
    [ObservableProperty] private decimal _qty;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private bool _selected;
    [ObservableProperty] private bool _transferMarked;
    [ObservableProperty] private bool _isPriceOverridden;
    [ObservableProperty] private bool _isQtyOverridden;
    [ObservableProperty] private bool _priceMissing;

    public decimal LineTotal => Selected ? Qty * UnitPrice : 0m;

    public event Action? LineChanged;

    public void SetSuggestedPrice(decimal? price, bool hasPrice)
    {
        _suppressOverride = true;
        UnitPrice = price ?? 0m;
        _suppressOverride = false;
        PriceMissing = !hasPrice;
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    public void SetSuggestedQty(decimal qty)
    {
        _suppressQtyOverride = true;
        Qty = qty;
        _suppressQtyOverride = false;
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    partial void OnQtyChanged(decimal value)
    {
        if (!_suppressQtyOverride)
            IsQtyOverridden = true;
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        if (!_suppressOverride)
        {
            IsPriceOverridden = true;
            PriceMissing = false;
        }
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }

    partial void OnSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(LineTotal));
        LineChanged?.Invoke();
    }
}
