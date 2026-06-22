using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoSlotPolicyTests
{
    [Fact]
    public void Apply_appends_first_photo_and_returns_overlay_text()
    {
        var photos = new List<string>();

        var result = CodingPhotoSlotPolicy.Apply(photos, @"C:\Fotos\a.png");

        Assert.Equal([@"C:\Fotos\a.png"], photos);
        Assert.Equal(1, result.SlotNumber);
        Assert.False(result.Replaced);
        Assert.Equal("Foto 1: a.png", result.OverlayText);
    }

    [Fact]
    public void Apply_appends_second_photo()
    {
        var photos = new List<string> { @"C:\Fotos\a.png" };

        var result = CodingPhotoSlotPolicy.Apply(photos, @"C:\Fotos\b.png");

        Assert.Equal([@"C:\Fotos\a.png", @"C:\Fotos\b.png"], photos);
        Assert.Equal(2, result.SlotNumber);
        Assert.False(result.Replaced);
        Assert.Equal("Foto 2: b.png", result.OverlayText);
    }

    [Fact]
    public void Apply_replaces_second_photo_when_two_or_more_exist()
    {
        var photos = new List<string> { "first.png", "old-second.png", "third.png" };

        var result = CodingPhotoSlotPolicy.Apply(photos, @"C:\Fotos\new-second.png");

        Assert.Equal(["first.png", @"C:\Fotos\new-second.png", "third.png"], photos);
        Assert.Equal(2, result.SlotNumber);
        Assert.True(result.Replaced);
        Assert.Equal("Foto 2 ersetzt: new-second.png", result.OverlayText);
    }
}
