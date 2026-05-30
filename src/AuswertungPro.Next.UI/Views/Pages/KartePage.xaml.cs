using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class KartePage : UserControl
{
    private KarteViewModel? _vm;

    public KartePage()
    {
        InitializeComponent();

        // ViewModel aus DataContext lesen sobald es gesetzt ist
        DataContextChanged += (_, _) => _vm = DataContext as KarteViewModel;

        Loaded += async (_, _) =>
        {
            _vm = DataContext as KarteViewModel;
            if (_vm is null)
                return;

            var map = await _vm.BuildMapAsync();
            MapControl.Map = map;
            await Dispatcher.InvokeAsync(
                () =>
                {
                    _vm.RefreshVisibleNetworkLayer(force: true);
                    MapControl.ForceUpdate();
                },
                DispatcherPriority.Loaded);
        };
    }
}
