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

            // Zentrieren + erstes Laden erst wenn der MapControl eine echte Größe hat.
            // Grund: Navigator.CenterOnAndZoomTo hat keinen Effekt bevor der Viewport
            // initialisiert ist (ActualWidth/Height > 0 UND Mapsui-Resolution > 0).
            await Dispatcher.InvokeAsync(
                () =>
                {
                    if (MapControl.ActualWidth > 0 && MapControl.ActualHeight > 0)
                    {
                        // Steuerelement bereits dimensioniert → sofort zentrieren
                        _vm?.CenterOnUriAndRefresh();
                        MapControl.ForceUpdate();
                    }
                    else
                    {
                        // Noch kein Layout → SizeChanged einmalig abonnieren
                        void OnSizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
                        {
                            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
                                return;

                            // Einmal-Handler: sofort abmelden
                            MapControl.SizeChanged -= OnSizeChanged;

                            // Auf UI-Thread bleiben (SizeChanged läuft bereits dort)
                            _vm?.CenterOnUriAndRefresh();
                            MapControl.ForceUpdate();
                        }

                        MapControl.SizeChanged += OnSizeChanged;
                    }
                },
                DispatcherPriority.Loaded);
        };
    }
}
