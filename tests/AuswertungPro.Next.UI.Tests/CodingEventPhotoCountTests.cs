using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventPhotoCountTests
{
    [Fact]
    public void PhotoCount_reports_all_photos_without_embedding_a_ui_symbol()
    {
        var codingEvent = new CodingEvent();

        Assert.Equal(0, codingEvent.PhotoCount);

        codingEvent.Entry.FotoPaths.Add("foto-1.jpg");
        codingEvent.Entry.FotoPaths.Add("foto-2.jpg");
        codingEvent.Entry.FotoPaths.Add("foto-3.jpg");

        Assert.Equal(3, codingEvent.PhotoCount);
    }
}
