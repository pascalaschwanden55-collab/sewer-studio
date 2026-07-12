using AuswertungPro.Next.Infrastructure.HoldingDistribution;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ShaftPdfSelectionExpanderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "SewerStudio_ShaftPdfSelection_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Expand_AddsMatchingPhotoPdf_WhenProtocolWasSelected()
    {
        Directory.CreateDirectory(_root);
        var protocol = Create("Gemeinde_Schachtprotokoll.pdf");
        var photos = Create("Gemeinde_Schachtfotos.pdf");
        var unrelated = Create("Haltungsprotokoll.pdf");

        var result = ShaftPdfSelectionExpander.Expand([protocol]);

        Assert.Contains(protocol, result, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(photos, result, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(unrelated, result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expand_AddsMatchingProtocolPdf_WhenPhotosWereSelected()
    {
        Directory.CreateDirectory(_root);
        var protocol = Create("Export_SCHACHTPROTOKOLL.pdf");
        var photos = Create("Export_SCHACHTFOTOS.pdf");

        var result = ShaftPdfSelectionExpander.Expand([photos]);

        Assert.Contains(protocol, result, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(photos, result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expand_LeavesOrdinarySelectionUnchanged()
    {
        Directory.CreateDirectory(_root);
        var selected = Create("Haltungsprotokoll.pdf");
        Create("Gemeinde_Schachtfotos.pdf");

        var result = ShaftPdfSelectionExpander.Expand([selected]);

        Assert.Equal([selected], result);
    }

    private string Create(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "test");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
