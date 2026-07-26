using System.IO;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    public void FileToImageConverter_Original_behaelt_die_echten_Bildpixel()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"sewerstudio-training-preview-{Guid.NewGuid():N}.png");
        try
        {
            SaveTestImage(path, width: 720, height: 576);
            var converter = new FileToImageConverter();

            var result = Assert.IsAssignableFrom<BitmapSource>(
                converter.Convert(
                    path,
                    typeof(BitmapSource),
                    "Original",
                    CultureInfo.InvariantCulture));

            Assert.Equal(720, result.PixelWidth);
            Assert.Equal(576, result.PixelHeight);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void TrainingStudio_verwendet_fuer_Modellboxen_die_Originalpixel()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml"));

        Assert.Contains(
            "Converter={StaticResource FileToImage}, ConverterParameter=Original",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingStudio_verdrahtet_die_allgemeine_Foto_KI_statt_des_Anschluss_Sonderwegs()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml"));

        Assert.Contains("Foto allgemein mit KI prüfen", source, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding FotoMitKiPruefenCommand}\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BestimmeBauartCommand", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingStudio_allgemeine_Foto_KI_nutzt_zentralen_Protokoll_KI_Dienst_und_Vsa_Katalog()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "TrainingStudioWindowDependencyFactory.cs"));

        Assert.Contains("protocolAi: services?.ProtocolAi", source, StringComparison.Ordinal);
        Assert.Contains("services.CodeCatalog.AllowedCodes()", source, StringComparison.Ordinal);
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
    public void PersonalGoldMainCodeToTextConverter_benennt_BBD_fachlich_korrekt()
    {
        var converter = new PersonalGoldMainCodeToTextConverter();

        var result = converter.Convert(
            "BBD",
            typeof(string),
            null,
            CultureInfo.InvariantCulture);

        Assert.Equal("BBD — Eindringender Boden", result);
    }

    [Fact]
    public void PersonalGoldMainCodeToTextConverter_laesst_Alle_Codes_unveraendert()
    {
        var converter = new PersonalGoldMainCodeToTextConverter();

        var result = converter.Convert(
            "Alle Codes",
            typeof(string),
            null,
            CultureInfo.InvariantCulture);

        Assert.Equal("Alle Codes", result);
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

    private static void SaveTestImage(string path, int width, int height)
    {
        var bitmap = new WriteableBitmap(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
