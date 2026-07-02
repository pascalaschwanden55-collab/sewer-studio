using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Attached Behavior fuer die Hover-Foto-Vorschau in Beobachtungslisten (ListBox/DataGrid mit
/// <see cref="ProtocolEntry"/>-Items). Verweilt die Maus ~350 ms auf einem Eintrag MIT hinterlegtem
/// Foto, blendet ein wiederverwendbares Vorschau-Popup weich ein. Mausrad blaettert bei mehreren
/// Fotos. Bestehende Klick-/Doppelklick-Handler bleiben unberuehrt.
/// </summary>
public static class PhotoHoverPreviewBehavior
{
    /// <summary>Verweildauer bis zum Einblenden.</summary>
    public const int HoverDelayMs = 350;

    // ── IsEnabled (in XAML setzbar) ──
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PhotoHoverPreviewBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    // ── ProjectRootProvider (nur per Code-Behind setzbar; Funcs gehen nicht in XAML) ──
    public static readonly DependencyProperty ProjectRootProviderProperty =
        DependencyProperty.RegisterAttached(
            "ProjectRootProvider",
            typeof(Func<string?>),
            typeof(PhotoHoverPreviewBehavior),
            new PropertyMetadata(null));

    public static void SetProjectRootProvider(DependencyObject element, Func<string?>? value)
        => element.SetValue(ProjectRootProviderProperty, value);

    public static Func<string?>? GetProjectRootProvider(DependencyObject element)
        => (Func<string?>?)element.GetValue(ProjectRootProviderProperty);

    // ── PhotoPathsSelector (nur per Code-Behind setzbar; Funcs gehen nicht in XAML) ──
    // Ordnet einen beliebigen Listeneintrag seinen Roh-Fotopfaden zu. Ohne Selektor greift
    // der ProtocolEntry-Fallback in PhotoHoverPreviewSelectors.ExtractPhotoPaths.
    public static readonly DependencyProperty PhotoPathsSelectorProperty =
        DependencyProperty.RegisterAttached(
            "PhotoPathsSelector",
            typeof(Func<object, IEnumerable<string>?>),
            typeof(PhotoHoverPreviewBehavior),
            new PropertyMetadata(null));

    public static void SetPhotoPathsSelector(DependencyObject element, Func<object, IEnumerable<string>?>? value)
        => element.SetValue(PhotoPathsSelectorProperty, value);

    public static Func<object, IEnumerable<string>?>? GetPhotoPathsSelector(DependencyObject element)
        => (Func<object, IEnumerable<string>?>?)element.GetValue(PhotoPathsSelectorProperty);

    // ── State (privat, pro Control eine Instanz) ──
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(HoverState),
            typeof(PhotoHoverPreviewBehavior),
            new PropertyMetadata(null));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl control)
            return;

        if (e.NewValue is true)
            Attach(control);
        else
            Detach(control);
    }

    private static void Attach(ItemsControl control)
    {
        if (control.GetValue(StateProperty) is HoverState)
            return; // schon verdrahtet

        control.SetValue(StateProperty, new HoverState(control));
    }

    private static void Detach(ItemsControl control)
    {
        if (control.GetValue(StateProperty) is not HoverState state)
            return;

        state.Detach();
        control.ClearValue(StateProperty);
    }

    /// <summary>
    /// Arbeitsflaeche des Monitors, auf dem das Host-Fenster liegt, in DIP. Faellt bei Fehler auf
    /// <see cref="SystemParameters.WorkArea"/> zurueck (bereits DIP).
    /// </summary>
    private static (double Width, double Height) WorkAreaDip(ItemsControl owner)
    {
        var window = Window.GetWindow(owner);
        try
        {
            if (window is not null)
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
                    var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(monitor, ref info))
                    {
                        var dpi = VisualTreeHelper.GetDpi(window);
                        var widthPx = info.rcWork.right - info.rcWork.left;
                        var heightPx = info.rcWork.bottom - info.rcWork.top;
                        var scaleX = dpi.DpiScaleX <= 0 ? 1d : dpi.DpiScaleX;
                        var scaleY = dpi.DpiScaleY <= 0 ? 1d : dpi.DpiScaleY;
                        return (widthPx / scaleX, heightPx / scaleY);
                    }
                }
            }
        }
        catch
        {
            // Fallback unten
        }

        var fallback = SystemParameters.WorkArea; // bereits in DIP
        return (fallback.Width, fallback.Height);
    }

    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    /// <summary>Kapselt den kompletten Zustand pro Listen-Control und verdrahtet dessen Maus-Events.</summary>
    private sealed class HoverState
    {
        private readonly ItemsControl _owner;
        private readonly DispatcherTimer _timer;
        private PhotoHoverPreviewPopup? _popup;
        private object? _hoverItem;
        private IReadOnlyList<string> _photos = Array.Empty<string>();
        private int _index;

        public HoverState(ItemsControl owner)
        {
            _owner = owner;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoverDelayMs) };
            _timer.Tick += OnTick;

            _owner.MouseMove += OnMouseMove;
            _owner.MouseLeave += OnMouseLeave;
            _owner.PreviewMouseWheel += OnPreviewMouseWheel;
            _owner.Unloaded += OnUnloaded;
        }

        public void Detach()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _owner.MouseMove -= OnMouseMove;
            _owner.MouseLeave -= OnMouseLeave;
            _owner.PreviewMouseWheel -= OnPreviewMouseWheel;
            _owner.Unloaded -= OnUnloaded;
            DisposePopup();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Ressourcen (Popup-HWND) freigeben; Verdrahtung bleibt, damit die Vorschau nach
            // erneutem Laden (z. B. Tab-Wechsel) weiterhin funktioniert. Popup wird lazy neu erzeugt.
            _timer.Stop();
            _hoverItem = null;
            _photos = Array.Empty<string>();
            DisposePopup();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var item = ItemUnderCursor(e);
            if (ReferenceEquals(item, _hoverItem))
                return; // gleicher Eintrag -> Timer NICHT neu starten (sonst oeffnet die Vorschau nie)

            _hoverItem = item;
            _popup?.CloseAnimated();
            _timer.Stop();

            if (item is null)
                return; // Header/Scrollbar/Zwischenraum -> nur schliessen

            _timer.Start();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _timer.Stop();
            _popup?.CloseAnimated();
            _hoverItem = null;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _timer.Stop();

            var item = _hoverItem;
            if (item is null)
                return;

            var root = GetProjectRootProvider(_owner)?.Invoke();
            var rawPaths = PhotoHoverPreviewSelectors.ExtractPhotoPaths(item, GetPhotoPathsSelector(_owner));
            _photos = PhotoHoverPreviewLogic.ResolveExistingPhotos(rawPaths, root, File.Exists);
            if (_photos.Count == 0)
                return; // Eintrag ohne (existierendes) Foto -> nichts anzeigen

            _index = 0;
            var (maxWidth, maxHeight) = MaxBoxForOwner();

            EnsurePopup();
            _popup!.ShowPhoto(_photos[_index], _index, _photos.Count, maxWidth, maxHeight);
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Nur blaettern, wenn das Popup offen ist; sonst scrollt die Liste normal weiter.
            if (_popup is not { IsOpen: true } || _photos.Count == 0)
                return;

            _index = PhotoHoverPreviewLogic.NextIndex(_index, _photos.Count, e.Delta < 0 ? +1 : -1);
            var (maxWidth, maxHeight) = MaxBoxForOwner();
            _popup.ShowPhoto(_photos[_index], _index, _photos.Count, maxWidth, maxHeight);
            e.Handled = true;
        }

        private object? ItemUnderCursor(MouseEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return null;

            // Beliebiger Listeneintrag (nicht mehr auf ProtocolEntry beschraenkt); die
            // konkrete Foto-Zuordnung uebernimmt der Selektor bzw. der Fallback in OnTick.
            var container = _owner.ContainerFromElement(source);
            return container is FrameworkElement element ? element.DataContext : null;
        }

        private void EnsurePopup()
        {
            _popup ??= new PhotoHoverPreviewPopup { PlacementTarget = _owner };
        }

        private void DisposePopup()
        {
            if (_popup is null)
                return;

            _popup.CloseImmediate();
            _popup.PlacementTarget = null;
            _popup = null;
        }

        private (double MaxWidth, double MaxHeight) MaxBoxForOwner()
        {
            var (screenWidth, screenHeight) = WorkAreaDip(_owner);
            return PhotoHoverPreviewLogic.MaxBoxFromScreen(screenWidth, screenHeight);
        }
    }
}
