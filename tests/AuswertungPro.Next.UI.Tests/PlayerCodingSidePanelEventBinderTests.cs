using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerCodingSidePanelEventBinderTests
{
    [Fact]
    public void Bind_connects_protocol_match_requests()
    {
        RunOnStaThread(() =>
        {
            var sidePanel = new PlayerCodingSidePanel();
            var protocolCalls = 0;
            var acceptGreenCalls = 0;

            PlayerCodingSidePanelEventBinder.Bind(
                sidePanel,
                new PlayerCodingSidePanelEventHandlers(
                    CodingTakePhoto: (_, _) => { },
                    CodingEventsPreviewMouseRightButtonDown: (_, _) => { },
                    CodingEventsDoubleClick: (_, _) => { },
                    CodingEventsSelectionChanged: (_, _) => { },
                    CodingEventEdit: (_, _) => { },
                    CodingEventShowPhotos: (_, _) => { },
                    CodingEventCloseStretch: (_, _) => { },
                    CodingEventSeek: (_, _) => { },
                    CodingEventDelete: (_, _) => { },
                    CodingAcceptDefect: (_, _) => { },
                    CodingEditDefect: (_, _) => { },
                    CodingRejectDefect: (_, _) => { },
                    ImportEventsDoubleClick: (_, _) => { },
                    ImportConfirm: (_, _) => { },
                    ImportSeek: (_, _) => { },
                    CodingSelectCode: (_, _) => { },
                    CodingCreateEvent: (_, _) => { },
                    CodingProtocolMatch: (_, _) => protocolCalls++,
                    CodingAcceptGreenMatches: (_, _) => acceptGreenCalls++,
                    ImportShowPhotos: (_, _) => { },
                    ImportEdit: (_, _) => { },
                    ImportConfirmToBrain: (_, _) => { },
                    SuggestionsDoubleClick: (_, _) => { },
                    SuggestionSeek: (_, _) => { },
                    SuggestionConfirm: (_, _) => { },
                    SuggestionReject: (_, _) => { }));

            InvokePrivateClick(sidePanel, "RunCodingProtocolMatch_Click");
            InvokePrivateClick(sidePanel, "CodingAcceptGreenMatches_Click");

            Assert.Equal(1, protocolCalls);
            Assert.Equal(1, acceptGreenCalls);
        });
    }

    private static void InvokePrivateClick(PlayerCodingSidePanel sidePanel, string methodName)
        => typeof(PlayerCodingSidePanel)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(sidePanel, [sidePanel, new RoutedEventArgs()]);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
