using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolEntryPhotoPathAppenderTests
{
    [Fact]
    public void AddIfPresent_adds_non_null_photo_path()
    {
        var entry = new ProtocolEntry();

        var added = CodingProtocolEntryPhotoPathAppender.AddIfPresent(entry, "foto.png");

        Assert.True(added);
        Assert.Equal(["foto.png"], entry.FotoPaths);
    }

    [Fact]
    public void AddIfPresent_ignores_null_photo_path()
    {
        var entry = new ProtocolEntry();

        var added = CodingProtocolEntryPhotoPathAppender.AddIfPresent(entry, null);

        Assert.False(added);
        Assert.Empty(entry.FotoPaths);
    }

    [Fact]
    public void AddDistinctNonBlank_adds_non_blank_missing_photo_path()
    {
        var entry = new ProtocolEntry { FotoPaths = ["existing.png"] };

        var added = CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank(entry, "new.png");

        Assert.True(added);
        Assert.Equal(["existing.png", "new.png"], entry.FotoPaths);
    }

    [Fact]
    public void AddDistinctNonBlank_ignores_blank_and_case_insensitive_duplicate_paths()
    {
        var entry = new ProtocolEntry { FotoPaths = ["C:\\Fotos\\A.PNG"] };

        var blankAdded = CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank(entry, " ");
        var duplicateAdded = CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank(entry, "c:\\fotos\\a.png");

        Assert.False(blankAdded);
        Assert.False(duplicateAdded);
        Assert.Equal(["C:\\Fotos\\A.PNG"], entry.FotoPaths);
    }
}
