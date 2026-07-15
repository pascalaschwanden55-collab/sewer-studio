using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class XtfHelperTests
{
    [Fact]
    public void ParseHoldingsFromXtf_LiestKanalAttributeAuchMitNamespace()
    {
        var path = CreateTempFile(
            """
            <TRANSFER xmlns="urn:sia405:test">
              <SIA405_Abwasser.Kanal Haltung="80638-80631"
                                         SchachtOben="80638"
                                         SchachtUnten="80631" />
            </TRANSFER>
            """);

        try
        {
            var holding = Assert.Single(XtfHelper.ParseHoldingsFromXtf(path));

            Assert.Equal("80638-80631", holding.HaltungId);
            Assert.Equal("80638", holding.SchachtOben);
            Assert.Equal("80631", holding.SchachtUnten);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseHoldingsFromXtf_BeschaedigteOderFehlendeDatei_LiefertLeereListe()
    {
        var path = CreateTempFile("<TRANSFER><Kanal");

        try
        {
            Assert.Empty(XtfHelper.ParseHoldingsFromXtf(path));
            Assert.Empty(XtfHelper.ParseHoldingsFromXtf(path + ".fehlt"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FindMatchingXtf_NimmtErstenPfadMitPdfBasisnamen()
    {
        var xtfFiles = new[]
        {
            @"D:\Export\anderes.xtf",
            @"D:\Export\20260715_80638-80631_Zusatz.xtf",
            @"D:\Export\20260715_80638-80631_Zweite.xtf"
        };

        var result = XtfHelper.FindMatchingXtf(
            @"D:\PDF\20260715_80638-80631.pdf",
            xtfFiles);

        Assert.Equal(xtfFiles[1], result);
    }

    private static string CreateTempFile(string content)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"sewerstudio-xtf-helper-{Guid.NewGuid():N}.xtf");
        File.WriteAllText(path, content);
        return path;
    }
}
