using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Controls;

public partial class ProjektPruefungView : UserControl
{
    private ProjektPruefungViewModel? _viewModel;

    public ProjektPruefungView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => Verbinde(DataContext as ProjektPruefungViewModel);
        Unloaded += (_, _) => Verbinde(null);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        => Verbinde(e.NewValue as ProjektPruefungViewModel);

    private void Verbinde(ProjektPruefungViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = viewModel;
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        if (_viewModel.FokusPunkt is { } punkt) FokussierePunkt(punkt);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjektPruefungViewModel.FokusPunkt)
            && _viewModel?.FokusPunkt is { } punkt)
            FokussierePunkt(punkt);
    }

    private void FokussierePunkt(ProjektPruefpunkt punkt)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || !ReferenceEquals(_viewModel?.FokusPunkt, punkt)) return;
            PruefpunkteGrid.SelectedItem = punkt;
            PruefpunkteGrid.ScrollIntoView(punkt);
            PruefpunkteGrid.Focus();
            _viewModel.FokusPunkt = null;
        }, DispatcherPriority.Input);
    }
}
