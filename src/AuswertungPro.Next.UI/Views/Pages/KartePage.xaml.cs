using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class KartePage : UserControl
{
    private KarteViewModel? _vm;
    private bool _selectionSubscribed;
    private bool _mapInitialized;
    private bool _mapBuildInProgress;

    public KartePage()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => _vm = DataContext as KarteViewModel;
        Loaded += KartePage_Loaded;
        Unloaded += KartePage_Unloaded;
    }

    private async void KartePage_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        SubscribeBridgeSelection();
        _vm = DataContext as KarteViewModel;
        if (_vm is null)
            return;

        if (!_mapInitialized && !_mapBuildInProgress)
        {
            _mapBuildInProgress = true;
            var vm = _vm;

            try
            {
                var map = await vm.BuildMapAsync();
                if (!ReferenceEquals(vm, _vm))
                    return;

                MapControl.Map = map;
                _mapInitialized = true;
                await RefreshWhenSizedAsync(centerInitial: true);
            }
            finally
            {
                _mapBuildInProgress = false;
            }

            return;
        }

        await RefreshWhenSizedAsync(centerInitial: false);
    }

    private void KartePage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        UnsubscribeBridgeSelection();
    }

    private void SubscribeBridgeSelection()
    {
        if (_selectionSubscribed)
            return;

        QgisBridge.QgisBridgeSelection.SelectionChanged += OnBridgeSelectionChanged;
        _selectionSubscribed = true;
    }

    private void UnsubscribeBridgeSelection()
    {
        if (!_selectionSubscribed)
            return;

        QgisBridge.QgisBridgeSelection.SelectionChanged -= OnBridgeSelectionChanged;
        _selectionSubscribed = false;
    }

    private Task RefreshWhenSizedAsync(bool centerInitial)
        => Dispatcher.InvokeAsync(
            () => ApplyViewportRefresh(centerInitial),
            DispatcherPriority.Loaded).Task;

    private void ApplyViewportRefresh(bool centerInitial)
    {
        if (MapControl.ActualWidth > 0 && MapControl.ActualHeight > 0)
        {
            if (centerInitial)
                _vm?.CenterOnUriAndRefresh();
            else
                _vm?.RefreshVisibleNetworkLayer(force: true);

            MapControl.ForceUpdate();
            return;
        }

        void OnSizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            _ = sender;
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
                return;

            MapControl.SizeChanged -= OnSizeChanged;

            if (centerInitial)
                _vm?.CenterOnUriAndRefresh();
            else
                _vm?.RefreshVisibleNetworkLayer(force: true);

            MapControl.ForceUpdate();
        }

        MapControl.SizeChanged += OnSizeChanged;
    }

    private void ZoomIn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        MapControl.Map?.Navigator.ZoomIn(250);
        _vm?.RefreshVisibleNetworkLayer(force: true);
        MapControl.ForceUpdate();
    }

    private void ZoomOut_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        MapControl.Map?.Navigator.ZoomOut(250);
        _vm?.RefreshVisibleNetworkLayer(force: true);
        MapControl.ForceUpdate();
    }

    private void ZoomToNetwork_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _vm?.ZoomToNetworkAndRefresh();
        MapControl.ForceUpdate();
    }

    private void ToggleBasemap_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _vm?.ToggleBasemap();
        MapControl.ForceUpdate();
    }

    private void ToggleSchaechte_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _vm?.ToggleSchaechte();
        MapControl.ForceUpdate();
    }

    private void OnBridgeSelectionChanged()
        => Dispatcher.InvokeAsync(() =>
        {
            _vm?.ZoomToSelectedHaltung();
            MapControl.ForceUpdate();
        });
}
