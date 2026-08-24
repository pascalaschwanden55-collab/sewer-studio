using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Erzeugt mehrere Eigentuemerdossiers auf einmal. Das Fenster zeigt nur an und
/// haekelt ab; die Regeln liegen in den Anwendungsfaellen.
/// </summary>
public partial class DossierBatchWindow : Window
{
    private readonly DossierBatchViewModel _viewModel = new();
    private readonly IParcelLookup _parcels;
    private readonly DossierBatchProposalUseCase _proposal;
    private readonly IReadOnlyList<string> _projectHoldingNames;
    private readonly IReadOnlyDictionary<string, Guid> _holdingIdsByName;
    private readonly IReadOnlyList<string> _projectShaftNumbers;
    private readonly IReadOnlyList<string> _parcelsWithDossier;

    private CancellationTokenSource? _laufendeSuche;

    private DossierBatchWindow(
        IParcelLookup parcels,
        DossierBatchProposalUseCase proposal,
        IReadOnlyList<string> projectHoldingNames,
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers,
        IReadOnlyList<string> parcelsWithDossier)
    {
        InitializeComponent();

        _parcels = parcels;
        _proposal = proposal;
        _projectHoldingNames = projectHoldingNames;
        _holdingIdsByName = holdingIdsByName;
        _projectShaftNumbers = projectShaftNumbers;
        _parcelsWithDossier = parcelsWithDossier;

        ProposalGrid.ItemsSource = _viewModel.Rows;
        Loaded += async (_, _) => await LadeGemeinden().ConfigureAwait(true);

        // Eine laufende Abfrage soll nicht sinnlos weiterlaufen, und ihre spaete
        // Antwort darf nicht in ein geschlossenes Fenster schreiben.
        Closing += (_, _) => _laufendeSuche?.Cancel();
    }

    /// <summary>Die erzeugten Dossiers. Leer, wenn abgebrochen wurde.</summary>
    public IReadOnlyList<DossierDefinition> Created { get; private set; } = Array.Empty<DossierDefinition>();

    public static IReadOnlyList<DossierDefinition> ShowFor(
        IParcelLookup parcels,
        DossierBatchProposalUseCase proposal,
        IReadOnlyList<string> projectHoldingNames,
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers,
        IReadOnlyList<string> parcelsWithDossier)
    {
        ArgumentNullException.ThrowIfNull(parcels);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(projectShaftNumbers);

        var fenster = new DossierBatchWindow(
            parcels, proposal, projectHoldingNames, holdingIdsByName,
            projectShaftNumbers, parcelsWithDossier)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return fenster.ShowDialog() == true ? fenster.Created : Array.Empty<DossierDefinition>();
    }

    private async Task LadeGemeinden()
    {
        try
        {
            StatusText.Text = "Gemeindeliste wird geladen…";
            var gemeinden = await _parcels.ListMunicipalitiesAsync().ConfigureAwait(true);

            if (!IsLoaded)
                return;

            MunicipalityBox.ItemsSource = gemeinden;
            StatusText.Text = gemeinden.Count == 0
                ? "Die Gemeindeliste konnte nicht geladen werden. Ohne Netzverbindung geht diese Funktion nicht."
                : string.Empty;
        }
        catch (Exception ex)
        {
            if (!IsLoaded)
                return;

            StatusText.Text = "Die Gemeindeliste konnte nicht geladen werden: " + ex.Message;
        }
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (MunicipalityBox.SelectedItem is not Municipality gemeinde)
        {
            StatusText.Text = "Bitte zuerst die Gemeinde wählen.";
            return;
        }

        _laufendeSuche?.Cancel();
        _laufendeSuche = new CancellationTokenSource();

        StartButton.IsEnabled = false;
        CancelSearchButton.IsEnabled = true;
        CreateButton.IsEnabled = false;

        var fortschritt = new Progress<string>(text => StatusText.Text = text);

        try
        {
            var ergebnis = await _proposal.RunAsync(
                new DossierBatchProposalRequest(
                    gemeinde.BfsNr, _projectHoldingNames, _parcelsWithDossier),
                fortschritt,
                _laufendeSuche.Token).ConfigureAwait(true);

            if (!IsLoaded)
                return;

            _viewModel.Uebernehmen(ergebnis);
            CreateButton.IsEnabled = _viewModel.SelectedCount > 0;

            var kopf = _viewModel.Rows.Count == 0
                ? "Keine Parzellen gefunden."
                : $"{_viewModel.Rows.Count} Parzellen gefunden.";

            StatusText.Text = _viewModel.WarningText.Length == 0
                ? kopf
                : kopf + " Nicht alles konnte abgefragt werden: " + _viewModel.WarningText;
        }
        catch (OperationCanceledException)
        {
            if (!IsLoaded)
                return;

            StatusText.Text = "Abgebrochen. Es wurde nichts erzeugt.";
        }
        catch (Exception ex)
        {
            if (!IsLoaded)
                return;

            StatusText.Text = "Die Suche ist fehlgeschlagen: " + ex.Message;
        }
        finally
        {
            if (IsLoaded)
            {
                StartButton.IsEnabled = true;
                CancelSearchButton.IsEnabled = false;
            }
        }
    }

    private void OnCancelSearch(object sender, RoutedEventArgs e) => _laufendeSuche?.Cancel();

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        Created = DossierBatchCreationUseCase.Build(
            _viewModel.BaueAuswahl(), _holdingIdsByName, _projectShaftNumbers);
        DialogResult = true;
    }

    private void OnClose(object sender, RoutedEventArgs e) => DialogResult = false;
}
