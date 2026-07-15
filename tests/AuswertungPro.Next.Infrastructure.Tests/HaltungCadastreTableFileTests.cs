using System.Text;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HaltungCadastreTableFileTests
{
    [Fact]
    public void EnsureAndLoad_baut_aus_Xtf_eine_lesbare_Tabelle_und_den_Index()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cadastre_table_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var xtfPath = Path.Combine(root, "kataster.xtf");
            var tablePath = Path.Combine(root, "cache", "haltungen.tsv");
            File.WriteAllText(
                xtfPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <TRANSFER>
                  <SIA405.Haltung>
                    <Bezeichnung>865-864</Bezeichnung>
                    <LaengeEffektiv>70.51</LaengeEffektiv>
                    <Lichte_Hoehe>250</Lichte_Hoehe>
                    <Material>PE</Material>
                  </SIA405.Haltung>
                  <SIA405.Haltung>
                    <Bezeichnung>06.24341-35625</Bezeichnung>
                  </SIA405.Haltung>
                </TRANSFER>
                """,
                new UTF8Encoding(false));

            var index = new HaltungCadastreIndexProvider().EnsureAndLoad(xtfPath, tablePath);
            var rows = HaltungCadastreExtractor.ReadTable(tablePath);

            Assert.Equal(2, index.Count);
            Assert.True(index.PairExists("865", "864"));
            Assert.True(HaltungCadastreExtractor.IsTableFresh(tablePath, xtfPath));
            Assert.Collection(
                rows,
                first => Assert.Equal(
                    new CadastreHaltung("865-864", "865", "864", "70.51", "250", "PE"),
                    first),
                second => Assert.Equal(
                    new CadastreHaltung("06.24341-35625", "06.24341", "35625", null, null, null),
                    second));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { /* Testaufraeumen ist best effort. */ }
        }
    }
}
