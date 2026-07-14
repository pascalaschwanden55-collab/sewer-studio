using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public sealed class KatasterXtfFilePathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "KatasterXtfFilePathResolverTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_bevorzugt_bekannten_Dateinamen_und_nutzt_sonst_groesste_XTF()
    {
        Directory.CreateDirectory(_root);
        var klein = LegeAn("klein.xtf", "1");
        var gross = LegeAn("gross.xtf", "12345");
        IKatasterXtfPathResolver resolver = new KatasterXtfFilePathResolver();

        Assert.Equal(gross, resolver.Resolve(explicitPath: null, directoryPath: _root));

        var bevorzugt = LegeAn("Abwasserkataster_Uri_korrigiert.xtf", "x");
        Assert.Equal(bevorzugt, resolver.Resolve(explicitPath: null, directoryPath: _root));
        Assert.NotEqual(klein, bevorzugt);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Temp-Aufraeumen darf den Testlauf nicht verdecken.
        }
    }

    private string LegeAn(string name, string inhalt)
    {
        var pfad = Path.Combine(_root, name);
        File.WriteAllText(pfad, inhalt);
        return pfad;
    }
}
