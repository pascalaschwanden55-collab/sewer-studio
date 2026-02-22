using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models.Costs;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class CombinedOfferViewModel : ObservableObject
{
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
    private readonly CostCalculationService _costService;
    private PriceCatalog _catalog;
    private CalculatedOffer? _lastOffer;

    [ObservableProperty] private bool _isInstallingChromium;

    [ObservableProperty] private int _dn;
    [ObservableProperty] private decimal _lengthM;
    [ObservableProperty] private int _connections;
    [ObservableProperty] private int _endCuffs = 2;
    [ObservableProperty] private bool _waterholding;
    [ObservableProperty] private decimal _rabattPct;
    [ObservableProperty] private decimal _skontoPct;

    [ObservableProperty] private string _warningText = string.Empty;
    [ObservableProperty] private string _rabattText = string.Empty;
    [ObservableProperty] private string _subTotalNettoText = string.Empty;
    [ObservableProperty] private string _skontoText = string.Empty;
    [ObservableProperty] private string _totalNettoText = string.Empty;
    [ObservableProperty] private string _mwstText = string.Empty;
    [ObservableProperty] private string _totalBruttoText = string.Empty;

    public ObservableCollection<OfferLine> Lines { get; } = new();
    public ObservableCollection<MeasureSelectionRow> SelectedMeasures { get; } = new();

    public CombinedOfferViewModel(CostCalculationService costService, PriceCatalog catalog)
    {
        _costService = costService;
        _catalog = catalog;
    }

    [RelayCommand]
    private void SelectMeasures(Window? owner)
    {
        var allTemplates = _costService.LoadTemplates();
        var selVm = new MeasureSelectionViewModel(allTemplates);
        var selWindow = new Views.Windows.MeasureSelectionWindow
        {
            DataContext = selVm,
            Owner = owner
        };

        if (selWindow.ShowDialog() == true)
        {
            SelectedMeasures.Clear();
            foreach (var row in selVm.Rows.Where(r => r.IsSelected))
                SelectedMeasures.Add(row);
        }
    }

    [RelayCommand]
    private void Calculate()
    {
        if (!SelectedMeasures.Any())
        {
            MessageBox.Show("Bitte wählen Sie mindestens eine Maßnahme aus.", "Hinweis", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Use same inputs for all measures (could be extended to per-measure inputs)
        var inputRows = new List<MeasureInputs>();
        foreach (var _ in SelectedMeasures)
        {
            inputRows.Add(new MeasureInputs
            {
                Dn = Dn,
                LengthM = LengthM,
                Connections = Connections,
                EndCuffs = EndCuffs,
                Waterholding = Waterholding,
                RabattPct = RabattPct,
                SkontoPct = SkontoPct,
                MwstPct = 8.1m
            });
        }

        var templates = SelectedMeasures.Select(m => m.Template).ToList();
        var offer = _costService.CalculateCombinedOffer(templates, _catalog, inputRows);
        _lastOffer = offer;

        Lines.Clear();
        foreach (var line in offer.Lines)
            Lines.Add(line);

        WarningText = offer.Warnings.Any() ? string.Join("\n", offer.Warnings) : string.Empty;

        var cur = offer.Totals.Currency;
        RabattText = $"Rabatt {offer.Totals.RabattPct:N1}%: -{offer.Totals.Rabatt:N2} {cur}";
        SubTotalNettoText = $"Zwischensumme: {offer.Totals.SubTotal:N2} {cur}";
        SkontoText = $"Skonto {offer.Totals.SkontoPct:N1}%: -{offer.Totals.Skonto:N2} {cur}";
        TotalNettoText = $"Total (exkl. MWST): {offer.Totals.NetExclMwst:N2} {cur}";
        MwstText = $"MWST {offer.Totals.MwstPct:N1}%: {offer.Totals.Mwst:N2} {cur}";
        TotalBruttoText = $"TOTAL (inkl. MWST): {offer.Totals.TotalInclMwst:N2} {cur}";
    }

    [RelayCommand]
    private void OpenPriceEditor(Window? owner)
    {
        var editorVm = new PriceCatalogEditorViewModel(_costService);
        var editorWindow = new Views.Windows.PriceCatalogEditorWindow
        {
            DataContext = editorVm,
            Owner = owner
        };
        editorWindow.ShowDialog();

        _catalog = _costService.LoadCatalog();
        Calculate();
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (_lastOffer == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Kombinierte Offerte");
        sb.AppendLine($"Maßnahmen: {string.Join(", ", SelectedMeasures.Select(m => m.Name))}");
        sb.AppendLine($"DN {Dn}, Länge {LengthM:0.00}m, Anschlüsse {Connections}, Endmanschetten {EndCuffs}");
        sb.AppendLine();

        foreach (var group in _lastOffer.Lines.GroupBy(l => l.Group))
        {
            sb.AppendLine($"[{group.Key}]");
            foreach (var line in group)
            {
                var ep = line.UnitPrice?.ToString("N2") ?? "?";
                var am = line.Amount?.ToString("N2") ?? "?";
                sb.AppendLine($"{line.Label} | {line.Qty} {line.Unit} | EP {ep} | {am} {_lastOffer.Totals.Currency}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Zwischensumme: {_lastOffer.Totals.SubTotal:N2} {_lastOffer.Totals.Currency}");
        sb.AppendLine($"Rabatt {_lastOffer.Totals.RabattPct:N1}%: -{_lastOffer.Totals.Rabatt:N2} {_lastOffer.Totals.Currency}");
        sb.AppendLine($"Skonto {_lastOffer.Totals.SkontoPct:N1}%: -{_lastOffer.Totals.Skonto:N2} {_lastOffer.Totals.Currency}");
        sb.AppendLine($"Total (exkl. MWST): {_lastOffer.Totals.NetExclMwst:N2} {_lastOffer.Totals.Currency}");
        sb.AppendLine($"MWST {_lastOffer.Totals.MwstPct:N1}%: {_lastOffer.Totals.Mwst:N2} {_lastOffer.Totals.Currency}");
        sb.AppendLine($"TOTAL (inkl. MWST): {_lastOffer.Totals.TotalInclMwst:N2} {_lastOffer.Totals.Currency}");

        Clipboard.SetText(sb.ToString());
        MessageBox.Show("Kombinierte Offerte wurde in die Zwischenablage kopiert.", "OK", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task ExportPdfAsync(Window? owner)
    {
        if (_lastOffer is null)
        {
            MessageBox.Show("Bitte zuerst berechnen.", "PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var defaultName = $"Offerte_Kombiniert_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Offerte als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "offer.sbnhtml");
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");

            var selected = SelectedMeasures.Any()
                ? string.Join(", ", SelectedMeasures.Select(m => m.Name))
                : "Kombiniert";

            var ctx = new OfferPdfContext
            {
                ProjectTitle = "Abwasser Uri – Offerte",
                VariantTitle = "Kombinierte Offerte: " + selected,
                CustomerBlock = "",
                ObjectBlock = $"DN {Dn}, Länge {LengthM:0.00} m\nAnschlüsse {Connections}, Endmanschetten {EndCuffs}" + (Waterholding ? "\nWasserhaltung: Ja" : "") +
                             $"\nRabatt {RabattPct:N1}%, Skonto {SkontoPct:N1}%",
                Currency = _lastOffer.Totals.Currency,
                OfferNo = ""
            };

            var model = OfferPdfModelFactory.Create(_lastOffer, ctx, DateTimeOffset.Now);
            var renderer = new OfferHtmlToPdfRenderer();

            owner ??= System.Windows.Application.Current?.MainWindow;
            if (owner is not null) owner.Cursor = System.Windows.Input.Cursors.Wait;

            await renderer.RenderAsync(model, templatePath, output, logoPath);

            MessageBox.Show("PDF wurde erstellt.", "PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF konnte nicht erstellt werden:\n{ex.Message}", "PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (owner is not null) owner.Cursor = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallChromium))]
    private async Task InstallChromiumAsync(Window? owner)
    {
        if (_sp.PlaywrightInstaller.IsChromiumInstalled())
        {
            MessageBox.Show("Chromium ist bereits installiert.", "PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        owner ??= System.Windows.Application.Current?.MainWindow;

        try
        {
            IsInstallingChromium = true;
            if (owner is not null) owner.Cursor = System.Windows.Input.Cursors.Wait;

            var result = await _sp.PlaywrightInstaller.InstallChromiumAsync();
            if (result.Success)
            {
                MessageBox.Show("Chromium wurde installiert. PDF-Export sollte jetzt funktionieren.", "PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "Chromium-Installation fehlgeschlagen.\n\n" +
                    $"ExitCode: {result.ExitCode}\nTool: {result.Tool}\nScript: {result.ScriptPath}\n\n" +
                    result.CombinedOutput,
                    "PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chromium-Installation fehlgeschlagen:\n{ex.Message}", "PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (owner is not null) owner.Cursor = null;
            IsInstallingChromium = false;
        }
    }

    private bool CanInstallChromium() => !IsInstallingChromium;

    partial void OnIsInstallingChromiumChanged(bool value)
    {
        InstallChromiumCommand.NotifyCanExecuteChanged();
    }
}
