using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTimelineControlsTests
{
    [Fact]
    public void Configure_wires_timeline_accessors_markers_commands_and_panel_visibility()
    {
        RunOnStaThread(() =>
        {
            var timeline = new PipeGraphTimeline();
            var panel = new FrameworkElement { Visibility = Visibility.Collapsed };
            var markers = new List<CodingEvent>
            {
                new()
                {
                    MeterAtCapture = 4.2,
                    Entry = new ProtocolEntry { Code = "BBA" },
                    AiContext = new CodingEventAiContext { Confidence = 0.7 }
                }
            };
            var navigate = new RecordingCommand();
            var markerClicked = new RecordingCommand();
            var configure = FindConfigureMethod();
            Assert.NotNull(configure);

            configure.Invoke(null, [timeline, panel, 12.3, markers, navigate, markerClicked]);

            Assert.Equal(12.3, timeline.TotalLength);
            Assert.Same(markers, timeline.Markers);
            Assert.Same(navigate, timeline.NavigateToMeterCommand);
            Assert.Same(markerClicked, timeline.MarkerClickedCommand);
            Assert.Equal(Visibility.Visible, panel.Visibility);

            var marker = Assert.Single(markers);
            Assert.Equal(4.2, timeline.MeterAccessor!(marker));
            Assert.Equal("BBA", timeline.CodeAccessor!(marker));
            Assert.Equal(0.7, timeline.ConfidenceAccessor!(marker));
            Assert.False(timeline.IsRejectedAccessor!(marker));
        });
    }

    private static MethodInfo? FindConfigureMethod()
        => typeof(CodingTimelineMarkerAccessors).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingTimelineControls")
            ?.GetMethod(
                "Configure",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types:
                [
                    typeof(PipeGraphTimeline),
                    typeof(FrameworkElement),
                    typeof(double),
                    typeof(System.Collections.IEnumerable),
                    typeof(ICommand),
                    typeof(ICommand)
                ],
                modifiers: null);

    private sealed class RecordingCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }

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
