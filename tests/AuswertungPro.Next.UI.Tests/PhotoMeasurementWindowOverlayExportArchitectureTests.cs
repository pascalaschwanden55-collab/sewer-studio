using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PhotoMeasurementWindowOverlayExportArchitectureTests
{
    [Fact]
    public void Photo_measurement_window_delegates_overlay_rendering_and_file_output_to_exporter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PhotoMeasurementWindow.xaml.cs");
        var exporterPath = Path.Combine(
            uiRoot,
            "PhotoMeasurement",
            "PhotoMeasurementOverlayExporter.cs");
        var workflowPath = Path.Combine(
            uiRoot,
            "PhotoMeasurement",
            "PhotoMeasurementCompletionWorkflow.cs");

        Assert.True(File.Exists(exporterPath), "Der WPF-/Datei-Export muss ausserhalb des Fensters liegen.");
        Assert.True(File.Exists(workflowPath), "Fehlerbehandlung und Messergebnis sollen testbar ausserhalb des Fensters liegen.");

        var windowPartials = string.Join(
            Environment.NewLine,
            Directory.GetFiles(windowsRoot, "PhotoMeasurementWindow*.cs").Select(File.ReadAllText));
        var windowRoot = File.ReadAllText(windowRootPath);
        var exporter = File.ReadAllText(exporterPath);
        var workflow = File.ReadAllText(workflowPath);
        var okHandler = ExtractMethodBody(windowRoot, "private void BtnOk_Click(");

        Assert.DoesNotContain("BurnOverlayToPhoto", windowPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderTargetBitmap", windowPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawingVisual", windowPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("VisualBrush", windowPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("PngBitmapEncoder", windowPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Create", windowPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.ChangeExtension", windowPartials, StringComparison.Ordinal);
        Assert.DoesNotContain("_overlay.png", windowPartials, StringComparison.Ordinal);

        Assert.Contains("private readonly IPhotoMeasurementOverlayExporter _overlayExporter;", windowRoot);
        Assert.Equal(
            1,
            CountOccurrences(
                windowPartials,
                "_overlayExporter = new PhotoMeasurementOverlayExporter();"));
        Assert.Contains("PhotoMeasurementCompletionWorkflow.Execute", okHandler);
        Assert.Equal(1, CountOccurrences(okHandler, "_overlayExporter.Export("));
        Assert.Contains("PhotoImage.Source as BitmapSource", okHandler);
        Assert.Contains("OverlayCanvas", okHandler);
        Assert.Contains("GetImageRenderedRect(PhotoImage)", okHandler);
        Assert.Contains("_photoPath", okHandler);
        Assert.Contains("UserError.DescribeAndReport", okHandler);
        Assert.Contains("\"Messfoto-Overlay exportieren\"", okHandler);
        Assert.Contains("DialogResult = true", okHandler);
        Assert.DoesNotContain("catch (Exception", okHandler, StringComparison.Ordinal);

        Assert.Contains("internal interface IPhotoMeasurementOverlayExporter", exporter);
        Assert.Contains(
            "internal sealed class PhotoMeasurementOverlayExporter : IPhotoMeasurementOverlayExporter",
            exporter);
        Assert.DoesNotContain("partial class PhotoMeasurementWindow", exporter, StringComparison.Ordinal);
        Assert.Contains("internal static class PhotoMeasurementCompletionWorkflow", workflow);
        Assert.DoesNotContain("partial class PhotoMeasurementWindow", workflow, StringComparison.Ordinal);
        Assert.Contains("catch (Exception ex)", workflow);
        Assert.Contains("Confirmed = true", workflow);
    }

    private static int CountOccurrences(string source, string token)
        => source.Split(token, StringSplitOptions.None).Length - 1;

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Methode nicht gefunden: {signature}");

        var openBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openBraceIndex > signatureIndex, $"Methoden-Anfang nicht gefunden: {signature}");

        var depth = 0;
        for (var index = openBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}')
                depth--;

            if (depth == 0)
                return source[signatureIndex..(index + 1)];
        }

        throw new InvalidOperationException($"Methoden-Ende nicht gefunden: {signature}");
    }
}
