using System;
using System.IO;
using System.Collections.ObjectModel;
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

public sealed partial class CostCalculationViewModel : ObservableObject
{
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
    private readonly CostCalculationService _costService;
    private readonly MeasureTemplate _template;
    private PriceCatalog _catalog;
    private CalculatedOffer? _lastOffer;

    [ObservableProperty] private string _measureName = string.Empty;
    [ObservableProperty] private int _dn;
    [ObservableProperty] private decimal _lengthM;
    [ObservableProperty] private int _connections;
    [ObservableProperty] private int _endCuffs = 2;
    [ObservableProperty] private bool _waterholding;
    
    [ObservableProperty] private string _warningText = string.Empty;
    [ObservableProperty] private string _subTotalText = string.Empty;
    [ObservableProperty] private string _mwstText = string.Empty;
    [ObservableProperty] private string _totalText = string.Empty;

    [ObservableProperty] private bool _isInstallingChromium;
    
    public ObservableCollection<OfferLine> Lines { get; } = new();

    public CostCalculationViewModel(CostCalculationService costService, MeasureTemplate template, PriceCatalog catalog)
    {
        _costService = costService;
        _template = template;
        _catalog = catalog;
        MeasureName = template.Name;
    }

    [RelayCommand]
    private void Calculate()
    {
        var inputs = new MeasureInputs
        {
            Dn = Dn,
            LengthM = LengthM,
            Connections = Connections,
            EndCuffs = EndCuffs,
            Waterholding = Waterholding,
            RabattPct = 0,
            SkontoPct = 0,
            MwstPct = 8.1m
        };

        var offer = _costService.CalculateOffer(_template, _catalog, inputs);
        _lastOffer = offer;

        Lines.Clear();
        foreach (var line in offer.Lines)
            Lines.Add(line);

        WarningText = offer.Warnings.Any() ? string.Join("\n", offer.Warnings) : string.Empty;

        var cur = offer.Totals.Currency;
        SubTotalText = $"Zwischensumme: {offer.Totals.SubTotal:N2} {cur}";
        MwstText = $"MWST {offer.Totals.MwstPct:N1}%: {offer.Totals.Mwst:N2} {cur}";
        TotalText = $"TOTAL: {offer.Totals.TotalInclMwst:N2} {cur}";
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

        // Reload catalog and recalculate
        _catalog = _costService.LoadCatalog();
        Calculate();
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (_lastOffer == null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Offerte - {_template.Name}");
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
        sb.AppendLine($"MWST {_lastOffer.Totals.MwstPct:N1}%: {_lastOffer.Totals.Mwst:N2} {_lastOffer.Totals.Currency}");
        sb.AppendLine($"TOTAL: {_lastOffer.Totals.TotalInclMwst:N2} {_lastOffer.Totals.Currency}");

        Clipboard.SetText(sb.ToString());
        MessageBox.Show("Offerte wurde in die Zwischenablage kopiert.", "OK", 
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

        var defaultName = $"Offerte_{SanitizeFilePart(MeasureName)}_{DateTime.Now:yyyyMMdd}.pdf";
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

            var ctx = new OfferPdfContext
            {
                ProjectTitle = "Abwasser Uri – Offerte",
                VariantTitle = MeasureName,
                CustomerBlock = "",
                ObjectBlock = $"DN {Dn}, Länge {LengthM:0.00} m\nAnschlüsse {Connections}, Endmanschetten {EndCuffs}" + (Waterholding ? "\nWasserhaltung: Ja" : ""),
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

    private static string SanitizeFilePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Offerte";
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Length > 80 ? value.Substring(0, 80) : value;
    }
}
