using System.ComponentModel;
using System.Windows;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Fenster «Verteilen». Orchestriert nur: Die Wahl und die Vorschau liegen im
/// <see cref="VerteilenViewModel"/>, verteilt wird danach von der Export-Seite.
/// </summary>
public partial class VerteilenWindow : Window
{
    private readonly VerteilenViewModel _viewModel;

    public VerteilenWindow(VerteilenViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SchliessenAngefordert += OnSchliessenAngefordert;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        AktualisiereFilmSpalte();
        Loaded += (_, _) => viewModel.Starte();
        Closed += (_, _) =>
        {
            viewModel.SchliessenAngefordert -= OnSchliessenAngefordert;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    private void OnSchliessenAngefordert(object? sender, EventArgs e) => Close();

    // Eine DataGrid-Spalte liegt nicht im Elementbaum; ihre Sichtbarkeit laesst sich nicht binden.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VerteilenViewModel.ZeigeFilme) or null)
            AktualisiereFilmSpalte();
    }

    private void AktualisiereFilmSpalte()
        => FilmSpalte.Visibility = _viewModel.ZeigeFilme ? Visibility.Visible : Visibility.Collapsed;
}
