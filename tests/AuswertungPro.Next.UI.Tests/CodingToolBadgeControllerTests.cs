using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingToolBadgeControllerTests
{
    [Fact]
    public void Update_keeps_existing_badge_when_overlay_service_is_missing()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border
            {
                Tag = OverlayTags.ToolBadge,
                Child = new TextBlock { Text = "Alt" }
            });

            CodingToolBadgeController.Update(
                canvas,
                hasOverlayService: false,
                activeTool: OverlayToolType.Level,
                schemaType: SchemaType.FillLevel,
                activeLevelMode: LevelMode.Water);

            var badge = Assert.IsType<Border>(canvas.Children[0]);
            return Assert.IsType<TextBlock>(badge.Child).Text;
        });

        Assert.Equal("Alt", result);
    }

    [Fact]
    public void Update_builds_active_tool_text_and_renders_badge()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();

            CodingToolBadgeController.Update(
                canvas,
                hasOverlayService: true,
                activeTool: OverlayToolType.Level,
                schemaType: SchemaType.FillLevel,
                activeLevelMode: LevelMode.Water);

            var badge = Assert.IsType<Border>(canvas.Children[0]);
            return Assert.IsType<TextBlock>(badge.Child).Text;
        });

        Assert.Equal("Wasser %", result);
    }

    [Fact]
    public void Update_clears_badge_when_active_tool_has_no_badge_text()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border { Tag = OverlayTags.ToolBadge });
            canvas.Children.Add(new Border { Tag = OverlayTags.BendMarker });

            CodingToolBadgeController.Update(
                canvas,
                hasOverlayService: true,
                activeTool: OverlayToolType.None,
                schemaType: SchemaType.FillLevel,
                activeLevelMode: LevelMode.Water);

            return (canvas.Children.Count, ((Border)canvas.Children[0]).Tag);
        });

        Assert.Equal(1, result.Count);
        Assert.Equal(OverlayTags.BendMarker, result.Tag);
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? threadError = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        return result!;
    }
}
