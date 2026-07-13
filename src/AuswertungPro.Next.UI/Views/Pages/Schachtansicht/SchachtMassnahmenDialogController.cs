using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
    private const string StoreFileName = "schacht_empfehlungen.json";

    private readonly ServiceProvider _services;
    private readonly FrameworkElement _ownerElement;
    private readonly Action _markProjectDirty;
    private readonly Action _refreshPage;

    public SchachtMassnahmenDialogController(
        ServiceProvider services,
        FrameworkElement ownerElement,
        Action markProjectDirty,
        Action refreshPage)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _ownerElement = ownerElement ?? throw new ArgumentNullException(nameof(ownerElement));
        _markProjectDirty = markProjectDirty ?? throw new ArgumentNullException(nameof(markProjectDirty));
        _refreshPage = refreshPage ?? throw new ArgumentNullException(nameof(refreshPage));
    }

    public void Open(SchachtRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var schachtNummer = SchaechteColumnPolicy.GetSchachtNumber(record);
        var projectPath = _services.Settings.LastProjectPath;
        var repository = new ProjectCostStoreRepository(StoreFileName);
        var store = repository.Load(projectPath, out var loadError);

        if (loadError is not null)
        {
            _services.Dialogs.Warn(
                $"Bestehende Schacht-Empfehlungen konnten nicht gelesen werden:\n{loadError}\n\nDu kannst neu erfassen; Speichern legt die Datei neu an.",
                "Sanierungsmassnahmen");
        }

        HoldingCost? bestehend = null;
        if (!string.IsNullOrWhiteSpace(schachtNummer))
            store.ByHolding.TryGetValue(schachtNummer, out bestehend);

        var viewModel = new SchachtMassnahmenViewModel(
            record,
            _services.SchachtMassnahmenKatalog.Load(),
            bestehend,
            onUebernehmen: cost => Persist(repository, store, schachtNummer, cost, projectPath),
            onListeBearbeiten: EditKatalog);

        var window = new SchachtMassnahmenWindow(viewModel)
        {
            Owner = Window.GetWindow(_ownerElement)
        };
        window.ShowDialog();
    }

    private void Persist(
        ProjectCostStoreRepository repository,
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
                _services.Dialogs.Info(
                    "Projekt bitte zuerst speichern, damit die Auswahl dauerhaft abgelegt wird.",
                    "Sanierungsmassnahmen");
            }
            else if (!repository.Save(projectPath, store, out var error))
            {
                _services.Dialogs.Error(
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
        var viewModel = new SchachtMassnahmenKatalogEditorViewModel(
            _services.SchachtMassnahmenKatalog.Load());
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
                    ?? Window.GetWindow(_ownerElement);
        var window = new SchachtMassnahmenKatalogEditorWindow(viewModel) { Owner = owner };

        if (window.ShowDialog() != true)
            return null;

        _services.SchachtMassnahmenKatalog.Save(viewModel.Ergebnis);
        return viewModel.Ergebnis;
    }
}
