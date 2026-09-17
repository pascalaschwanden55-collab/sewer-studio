using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

/// <summary>
/// F1 (Fehleranalyse 17.09.2026): Eine nur voruebergehend gesperrte Projektdatei ist
/// NICHT beschaedigt. Frueher landete jeder Lesefehler im selben APP-LOAD, und die
/// Wiederherstellung spielte darauf eine alte Sicherung ueber den aktuellen Stand.
/// </summary>
public sealed class ProjektLadefehlerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "projekt-ladefehler", Guid.NewGuid().ToString("N"));

    public ProjektLadefehlerTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Load_GesperrteDatei_MeldetZugriffsfehlerStattBeschaedigung()
    {
        var path = Path.Combine(_root, "projekt.json");
        Assert.True(new JsonProjectRepository().Save(new Project { Name = "Aktueller Stand" }, path).Ok);

        using var sperre = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = new JsonProjectRepository().Load(path);

        Assert.False(result.Ok);
        Assert.Equal(ProjektLadefehler.Zugriff, result.ErrorCode);
    }

    [Fact]
    public void Load_BeschaedigtesJson_BleibtReparierbar()
    {
        var path = Path.Combine(_root, "kaputt.json");
        File.WriteAllText(path, "{ kaputte projektdatei");

        var result = new JsonProjectRepository().Load(path);

        Assert.Equal(ProjektLadefehler.Inhalt, result.ErrorCode);
        Assert.True(ProjektLadefehler.DarfSicherungEinspielen(result.ErrorCode));
    }

    [Theory]
    [InlineData(ProjektLadefehler.Inhalt, true)]        // belegt beschaedigt
    [InlineData(ProjektLadefehler.NichtGefunden, true)] // Datei weg
    [InlineData(ProjektLadefehler.Zugriff, false)]      // nur gesperrt
    [InlineData(ProjektLadefehler.Version, false)]      // Programm zu alt
    [InlineData("APP-UNBEKANNT", false)]                // fail-closed
    [InlineData(null, false)]
    public void DarfSicherungEinspielen_NurBeiBelegterBeschaedigung(string? code, bool erwartet)
        => Assert.Equal(erwartet, ProjektLadefehler.DarfSicherungEinspielen(code));

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
}
