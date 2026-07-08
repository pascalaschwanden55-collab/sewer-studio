using AuswertungPro.Next.Infrastructure.Import.Protocols;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ProtocolNameResolverTests
{
    [Theory]
    [InlineData(@"D:\P\Importdateien\PDF\H_33390-36268.pdf", ProtocolKind.Haltung, "33390-36268")]
    [InlineData(@"D:\P\Importdateien\PDF\L_1273.01-7.34854.pdf", ProtocolKind.Haltung, "1273.01-7.34854")]
    [InlineData(@"D:\X\Schächte\27581\20260427_27581.pdf", ProtocolKind.Schacht, "27581")]
    [InlineData(@"D:\X\Haltungen\33390-36268\20260424_33390-36268.pdf", ProtocolKind.Haltung, "33390-36268")]
    [InlineData(@"D:\X\S_952.06.pdf", ProtocolKind.Schacht, "952.06")]
    [InlineData(@"D:\X\36051.pdf", ProtocolKind.Schacht, "36051")]
    // Kategorie-Ordner zaehlt auch als Vorfahr weiter oben (nicht nur direkter Elternordner):
    [InlineData(@"D:\Root\Schächte\27581\sub\20260427_27581.pdf", ProtocolKind.Schacht, "27581")]
    public void Resolve_erkennt_art_und_name(string path, ProtocolKind kind, string name)
    {
        var t = ProtocolNameResolver.Resolve(path);
        Assert.NotNull(t);
        Assert.Equal(kind, t!.Value.Kind);
        Assert.Equal(name, t.Value.Name);
    }

    [Theory]
    [InlineData(@"D:\X\A3_Übersichtsplan.pdf")]
    [InlineData(@"D:\X\Haltungsliste.pdf")]
    [InlineData(@"D:\X\Haltungs-Statistik.pdf")]
    [InlineData(@"D:\X\30x105_Jagdmatt_200_orto.pdf")]
    [InlineData(@"D:\X\30x105_Jagdmatt_200_AV.pdf")]
    public void Resolve_ueberspringt_nicht_protokolle(string path)
        => Assert.Null(ProtocolNameResolver.Resolve(path));
}
