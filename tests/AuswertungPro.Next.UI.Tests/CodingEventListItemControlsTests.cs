using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventListItemControlsTests
{
    [Fact]
    public void Apply_writes_zone_confidence_and_status_for_ai_event()
    {
        RunOnStaThread(() =>
        {
            var zoneDot = new Ellipse();
            var confidence = new TextBlock();
            var statusIcon = new TextBlock();
            var ev = new CodingEvent
            {
                Entry = new ProtocolEntry { Code = "BAB", Beschreibung = "Riss" },
                AiContext = new CodingEventAiContext
                {
                    Confidence = 0.876,
                    Decision = CodingUserDecision.AcceptedWithEdit
                }
            };

            CodingEventListItemControls.Apply(zoneDot, confidence, statusIcon, ev);

            Assert.Equal(Color.FromRgb(0x3B, 0x82, 0xF6), BrushColor(zoneDot.Fill));
            Assert.Equal("88%", confidence.Text);
            Assert.Equal(BrushColor(CodingSessionViewModel.GetConfidenceBrush(0.876)), BrushColor(confidence.Foreground));
            Assert.Equal("\uE70F", statusIcon.Text);
            Assert.Equal(
                BrushColor(CodingSessionViewModel.GetStatusBrush(DefectStatus.AcceptedWithEdit)),
                BrushColor(statusIcon.Foreground));
        });
    }

    [Fact]
    public void Apply_clears_confidence_and_keeps_status_for_manual_event()
    {
        RunOnStaThread(() =>
        {
            var zoneDot = new Ellipse();
            var confidence = new TextBlock { Text = "alt" };
            var statusIcon = new TextBlock();
            var ev = new CodingEvent
            {
                Entry = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" }
            };

            CodingEventListItemControls.Apply(zoneDot, confidence, statusIcon, ev);

            Assert.Equal(Color.FromRgb(0x94, 0xA3, 0xB8), BrushColor(zoneDot.Fill));
            Assert.Equal("", confidence.Text);
            Assert.Equal("\uE823", statusIcon.Text);
            Assert.Equal(
                BrushColor(CodingSessionViewModel.GetStatusBrush(DefectStatus.Pending)),
                BrushColor(statusIcon.Foreground));
        });
    }

    [Fact]
    public void Apply_allows_missing_template_parts()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" }
        };

        CodingEventListItemControls.Apply(null, null, null, ev);
    }

    private static Color BrushColor(Brush brush)
        => Assert.IsType<SolidColorBrush>(brush).Color;

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
