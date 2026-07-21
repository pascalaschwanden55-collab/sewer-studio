using System.IO;
using System.Globalization;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterFileToImageConverterTests
{
    [Fact]
    public void FileToImageConverter_loads_decoded_image_into_memory()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingCenterConverters.cs"));

        Assert.Contains("BitmapCacheOption.OnLoad", source);
        Assert.Contains("DecodePixelWidth = ResolveDecodePixelWidth(parameter)", source);
        Assert.Contains("return 480;", source);
    }

    [Fact]
    public void NotNullToBoolConverter_unterscheidet_null_und_Objekt()
    {
        var converter = new NotNullToBoolConverter();

        Assert.Equal(false, converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(true, converter.Convert(new object(), typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void VsaCodeToTextConverter_laesst_unbekannten_Text_unveraendert()
    {
        var converter = new VsaCodeToTextConverter();

        var result = converter.Convert(
            "nichts erkannt",
            typeof(string),
            null,
            CultureInfo.InvariantCulture);

        Assert.Equal("nichts erkannt", result);
    }

    [Fact]
    public void Converter_sind_nicht_mehr_im_Fenster_definiert()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingCenterWindow.xaml.cs"));

        Assert.DoesNotContain("class NotNullToBoolConverter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("class VsaCodeToTextConverter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("class FileToImageConverter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IValueConverter", source, StringComparison.Ordinal);
    }
}
