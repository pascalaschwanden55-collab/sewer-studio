using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewPendingGeometryControllerTests
{
    [Fact]
    public void Clear_setzt_pending_box_und_sam_mask_zurueck()
    {
        BoundingBox? box = new BoundingBox(0.5, 0.5, 0.2, 0.2);
        TrainingSegmentationMask? mask = new(
            MaskRle: "1,2,3",
            ImageWidth: 720,
            ImageHeight: 576,
            MaskAreaPixels: 42,
            Confidence: 0.91,
            Label: "BAB");

        TrainingReviewPendingGeometryController.Clear(
            value => box = value,
            value => mask = value);

        Assert.Null(box);
        Assert.Null(mask);
    }
}
