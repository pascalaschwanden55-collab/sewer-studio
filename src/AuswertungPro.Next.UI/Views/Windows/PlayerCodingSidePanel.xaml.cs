using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerCodingSidePanel : UserControl
{
    public PlayerCodingSidePanel()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? CodingTakePhotoRequested;
    public event MouseButtonEventHandler? CodingEventsPreviewMouseRightButtonDownRequested;
    public event MouseButtonEventHandler? CodingEventsDoubleClickRequested;
    public event SelectionChangedEventHandler? CodingEventsSelectionChangedRequested;
    public event RoutedEventHandler? CodingEventEditRequested;
    public event RoutedEventHandler? CodingEventShowPhotosRequested;
    public event RoutedEventHandler? CodingEventCloseStretchRequested;
    public event RoutedEventHandler? CodingEventSeekRequested;
    public event RoutedEventHandler? CodingEventDeleteRequested;
    public event RoutedEventHandler? CodingAcceptDefectRequested;
    public event RoutedEventHandler? CodingEditDefectRequested;
    public event RoutedEventHandler? CodingRejectDefectRequested;
    public event MouseButtonEventHandler? ImportEventsDoubleClickRequested;
    public event RoutedEventHandler? ImportConfirmRequested;
    public event RoutedEventHandler? ImportSeekRequested;
    public event RoutedEventHandler? CodingSelectCodeRequested;
    public event RoutedEventHandler? CodingCreateEventRequested;

    private void CodingTakePhoto_Click(object sender, RoutedEventArgs e) => CodingTakePhotoRequested?.Invoke(sender, e);
    private void CodingEvents_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => CodingEventsPreviewMouseRightButtonDownRequested?.Invoke(sender, e);
    private void CodingEvents_DoubleClick(object sender, MouseButtonEventArgs e) => CodingEventsDoubleClickRequested?.Invoke(sender, e);
    private void CodingEvents_SelectionChanged(object sender, SelectionChangedEventArgs e) => CodingEventsSelectionChangedRequested?.Invoke(sender, e);
    private void CodingEventEdit_Click(object sender, RoutedEventArgs e) => CodingEventEditRequested?.Invoke(sender, e);
    private void CodingEventShowPhotos_Click(object sender, RoutedEventArgs e) => CodingEventShowPhotosRequested?.Invoke(sender, e);
    private void CodingEventCloseStretch_Click(object sender, RoutedEventArgs e) => CodingEventCloseStretchRequested?.Invoke(sender, e);
    private void CodingEventSeek_Click(object sender, RoutedEventArgs e) => CodingEventSeekRequested?.Invoke(sender, e);
    private void CodingEventDelete_Click(object sender, RoutedEventArgs e) => CodingEventDeleteRequested?.Invoke(sender, e);
    private void CodingAcceptDefect_Click(object sender, RoutedEventArgs e) => CodingAcceptDefectRequested?.Invoke(sender, e);
    private void CodingEditDefect_Click(object sender, RoutedEventArgs e) => CodingEditDefectRequested?.Invoke(sender, e);
    private void CodingRejectDefect_Click(object sender, RoutedEventArgs e) => CodingRejectDefectRequested?.Invoke(sender, e);
    private void ImportEvents_DoubleClick(object sender, MouseButtonEventArgs e) => ImportEventsDoubleClickRequested?.Invoke(sender, e);
    private void ImportConfirm_Click(object sender, RoutedEventArgs e) => ImportConfirmRequested?.Invoke(sender, e);
    private void ImportSeek_Click(object sender, RoutedEventArgs e) => ImportSeekRequested?.Invoke(sender, e);
    private void CodingSelectCode_Click(object sender, RoutedEventArgs e) => CodingSelectCodeRequested?.Invoke(sender, e);
    private void CodingCreateEvent_Click(object sender, RoutedEventArgs e) => CodingCreateEventRequested?.Invoke(sender, e);
}
