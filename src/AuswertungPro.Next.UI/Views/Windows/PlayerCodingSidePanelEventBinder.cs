using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public sealed record PlayerCodingSidePanelEventHandlers(
    RoutedEventHandler CodingTakePhoto,
    MouseButtonEventHandler CodingEventsPreviewMouseRightButtonDown,
    MouseButtonEventHandler CodingEventsDoubleClick,
    SelectionChangedEventHandler CodingEventsSelectionChanged,
    RoutedEventHandler CodingEventEdit,
    RoutedEventHandler CodingEventShowPhotos,
    RoutedEventHandler CodingEventCloseStretch,
    RoutedEventHandler CodingEventSeek,
    RoutedEventHandler CodingEventDelete,
    RoutedEventHandler CodingAcceptDefect,
    RoutedEventHandler CodingEditDefect,
    RoutedEventHandler CodingRejectDefect,
    MouseButtonEventHandler ImportEventsDoubleClick,
    RoutedEventHandler ImportConfirm,
    RoutedEventHandler ImportSeek,
    RoutedEventHandler CodingSelectCode,
    RoutedEventHandler CodingCreateEvent,
    RoutedEventHandler CodingProtocolMatch,
    RoutedEventHandler CodingAcceptGreenMatches,
    RoutedEventHandler ImportShowPhotos,
    RoutedEventHandler ImportEdit,
    RoutedEventHandler ImportConfirmToBrain);

public static class PlayerCodingSidePanelEventBinder
{
    public static void Bind(
        PlayerCodingSidePanel sidePanel,
        PlayerCodingSidePanelEventHandlers handlers)
    {
        ArgumentNullException.ThrowIfNull(sidePanel);
        ArgumentNullException.ThrowIfNull(handlers);

        sidePanel.CodingTakePhotoRequested += handlers.CodingTakePhoto;
        sidePanel.CodingEventsPreviewMouseRightButtonDownRequested += handlers.CodingEventsPreviewMouseRightButtonDown;
        sidePanel.CodingEventsDoubleClickRequested += handlers.CodingEventsDoubleClick;
        sidePanel.CodingEventsSelectionChangedRequested += handlers.CodingEventsSelectionChanged;
        sidePanel.CodingEventEditRequested += handlers.CodingEventEdit;
        sidePanel.CodingEventShowPhotosRequested += handlers.CodingEventShowPhotos;
        sidePanel.CodingEventCloseStretchRequested += handlers.CodingEventCloseStretch;
        sidePanel.CodingEventSeekRequested += handlers.CodingEventSeek;
        sidePanel.CodingEventDeleteRequested += handlers.CodingEventDelete;
        sidePanel.CodingAcceptDefectRequested += handlers.CodingAcceptDefect;
        sidePanel.CodingEditDefectRequested += handlers.CodingEditDefect;
        sidePanel.CodingRejectDefectRequested += handlers.CodingRejectDefect;
        sidePanel.ImportEventsDoubleClickRequested += handlers.ImportEventsDoubleClick;
        sidePanel.ImportConfirmRequested += handlers.ImportConfirm;
        sidePanel.ImportSeekRequested += handlers.ImportSeek;
        sidePanel.CodingSelectCodeRequested += handlers.CodingSelectCode;
        sidePanel.CodingCreateEventRequested += handlers.CodingCreateEvent;
        sidePanel.CodingProtocolMatchRequested += handlers.CodingProtocolMatch;
        sidePanel.CodingAcceptGreenMatchesRequested += handlers.CodingAcceptGreenMatches;
        sidePanel.ImportShowPhotosRequested += handlers.ImportShowPhotos;
        sidePanel.ImportEditRequested += handlers.ImportEdit;
        sidePanel.ImportConfirmToBrainRequested += handlers.ImportConfirmToBrain;
    }
}
