using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayCleanupControllerTests
{
    [Fact]
    public void ClearAiOverlays_removes_only_ai_overlay_elements()
    {
        var result = RunOnSta(() =>
        {
            var canvas = new Canvas();
            canvas.Children.Add(new Border { Tag = OverlayTags.AiOverlay });
            canvas.Children.Add(new Border { Tag = OverlayTags.AiPrefix + "label" });
            canvas.Children.Add(new Border { Tag = OverlayTags.Manual });
            canvas.Children.Add(new Border { Tag = OverlayTags.Measure });

            CodingOverlayCleanupController.ClearAiOverlays(canvas);

            return (
                Count: canvas.Children.Count,
                FirstTag: ((Border)canvas.Children[0]).Tag,
                SecondTag: ((Border)canvas.Children[1]).Tag);
        });

        Assert.Equal(2, result.Count);
        Assert.Equal(OverlayTags.Manual, result.FirstTag);
        Assert.Equal(OverlayTags.Measure, result.SecondTag);
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
