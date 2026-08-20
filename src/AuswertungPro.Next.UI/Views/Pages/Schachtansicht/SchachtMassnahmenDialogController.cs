using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

/// <summary>
/// Oeffnet und speichert die einfachen, NPK-freien Schacht-Sanierungsmassnahmen.
/// Die Seite meldet nur noch, welcher Schacht bearbeitet werden soll.
/// </summary>
internal sealed class SchachtMassnahmenDialogController
{
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly ISchachtMassnahmenKatalogStore _katalog;
    private readonly IProjectCostStoreRepository _repository;
    private readonly ICostCatalogStore? _catalogStore;
    private readonly FrameworkElement _ownerElement;
    private readonly Action _markProjectDirty;
    private readonly Action _refreshPage;

    public SchachtMassnahmenDialogController(
        AppSettings settings,
        IDialogService dialogs,
        ISchachtMassnahmenKatalogStore katalog,
        IProjectCostStoreRepository repository,
        FrameworkElement ownerElement,
        Action markProjectDirty,
        Action refreshPage,
        ICostCatalogStore? catalogStore = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _ownerElement = ownerElement ?? throw new ArgumentNullException(nameof(ownerElement));
        _markProjectDirty = markProjectDirty ?? throw new ArgumentNullException(nameof(markProjectDirty));
        _refreshPage = refreshPage ?? throw new ArgumentNullException(nameof(refreshPage));
        _catalogStore = catalogStore;
    }

    public void Open(SchachtRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var schachtNummer = SchaechteColumnPolicy.GetSchachtNumber(record);
        var projectPath = _settings.LastProjectPath;
        var store = _repository.Load(projectPath, out var loadError);

        if (loadError is not null)
        {
            _dialogs.Error(
                $"Bestehende Schacht-Empfehlungen konnten nicht gelesen werden:\n{loadError}\n\n" +
                "Bearbeiten und Speichern sind gesperrt, damit die vorhandene Datei nicht " +
                "ueberschrieben wird. Bitte die Datei pruefen und danach erneut oeffnen.",
                "Sanierungsmassnahmen");
            return;
        }

        var katalog = _katalog.Load(out var katalogError);
        if (katalogError is not null)
        {
            _dialogs.Error(
                $"Die Schacht-Massnahmenliste konnte nicht gelesen werden:\n{katalogError}\n\n" +
                "Bearbeiten und Speichern sind gesperrt, damit die selbst gepflegte Liste " +
                "nicht durch die Standardliste ersetzt wird. Bitte die Datei pruefen und " +
                "danach erneut oeffnen.",
                "Sanierungsmassnahmen");
            return;
        }

        HoldingCost? bestehend = null;
        if (!string.IsNullOrWhiteSpace(schachtNummer))
            store.ByHolding.TryGetValue(schachtNummer, out bestehend);

        var viewModel = new SchachtMassnahmenViewModel(
            record,
            katalog,
            bestehend,
            onUebernehmen: cost => Persist(_repository, store, schachtNummer, cost, projectPath),
            onListeBearbeiten: EditKatalog,
            vatRate: ResolveVatRate(projectPath));

        var window = new SchachtMassnahmenWindow(viewModel)
        {
            Owner = Window.GetWindow(_ownerElement)
        };
        window.ShowDialog();
    }

    /// <summary>
    /// MWST-Satz des Projektkatalogs. Ohne lesbaren Katalog gilt der App-Standard —
    /// dieselbe Regel wie in der Schacht-Matrix.
    /// </summary>
    private decimal ResolveVatRate(string? projectPath)
    {
        if (_catalogStore is null)
            return CostCalculatorLogicService.DefaultVatRate;

        var catalog = _catalogStore.LoadMerged(projectPath ?? "", out var catalogError);
        if (catalogError is not null)
            return CostCalculatorLogicService.DefaultVatRate;

        return catalog.VatRate > 0m ? catalog.VatRate : CostCalculatorLogicService.DefaultVatRate;
    }

    private void Persist(
        IProjectCostStoreRepository repository,
        ProjectCostStore store,
        string schachtNummer,
        HoldingCost cost,
        string? projectPath)
    {
        if (!string.IsNullOrWhiteSpace(schachtNummer))
        {
            var hatAuswahl = cost.Measures.Any(measure => measure.Lines.Any(line => line.Selected));
            if (hatAuswahl)
                store.ByHolding[schachtNummer] = cost;
            else
                store.ByHolding.Remove(schachtNummer);

            if (string.IsNullOrWhiteSpace(projectPath))
            {
                _dialogs.Info(
                    "Projekt bitte zuerst speichern, damit die Auswahl dauerhaft abgelegt wird.",
                    "Sanierungsmassnahmen");
            }
            else if (!repository.Save(projectPath, store, out var error))
            {
                _dialogs.Error(
                    $"Speichern der Schacht-Empfehlungen fehlgeschlagen:\n{error}",
                    "Sanierungsmassnahmen");
            }
        }

        // Die Felder "Massnahmen" und "Kosten" schreibt bereits das ViewModel in den Record.
        _markProjectDirty();
        _refreshPage();
    }

    private IReadOnlyList<SchachtMassnahmeKatalogEintrag>? EditKatalog()
    {
        var bestand = _katalog.Load(out var loadError);
        if (loadError is not null)
        {
            // Ohne diese Sperre zeigt der Editor die Standardliste, der Anwender
            // bestaetigt sie, und die echte Liste ist beim Speichern weg (Audit M2).
            _dialogs.Error(
                $"Die Massnahmenliste konnte nicht gelesen werden:\n{loadError}\n\n" +
                "Das Bearbeiten ist gesperrt, damit die vorhandene Liste nicht ersetzt wird.",
                "Massnahmenliste");
            return null;
        }

        var viewModel = new SchachtMassnahmenKatalogEditorViewModel(bestand);
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
                    ?? Window.GetWindow(_ownerElement);
        var window = new SchachtMassnahmenKatalogEditorWindow(viewModel) { Owner = owner };

        if (window.ShowDialog() != true)
            return null;

        if (!_katalog.Save(viewModel.Ergebnis, out var saveError))
        {
            _dialogs.Error(
                $"Die Massnahmenliste konnte nicht gespeichert werden:\n{saveError}",
                "Massnahmenliste");
            return null;
        }

        return viewModel.Ergebnis;
    }
}
