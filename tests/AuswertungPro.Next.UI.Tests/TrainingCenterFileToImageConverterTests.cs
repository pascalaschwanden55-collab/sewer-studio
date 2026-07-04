using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterFileToImageConverterTests
{
    [Fact]
    public void FileToImageConverter_loads_decoded_image_into_memory()
    {
        var source = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"));

        Assert.Contains("BitmapCacheOption.OnLoad", source);
        Assert.Contains("DecodePixelWidth = ResolveDecodePixelWidth(parameter)", source);
        Assert.Contains("return 480;", source);
    }
}
