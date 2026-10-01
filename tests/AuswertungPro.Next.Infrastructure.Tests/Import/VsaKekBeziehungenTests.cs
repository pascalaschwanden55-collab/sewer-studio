using System.Xml.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf.VsaKek;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Die Beziehungsaufloesung des VSA-KEK-Imports fuer sich (AP08b): Befunde gehoeren ueber
/// die TID zu ihrer Untersuchung, nicht ueber den Namen; was sich nicht zuordnen laesst,
/// bleibt im Zwischenergebnis erkennbar. Grundlage ist die synthetische Referenzdatei.
/// </summary>
public sealed class VsaKekBeziehungenTests
{
    private static readonly VsaKekBezuege Bezuege = VsaKekBeziehungen.Loese(
        VsaKekObjektLeser.Lies(XDocument.Load(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", "vsakek-referenz.xtf"))),
        "referenz.xtf",
        new NamensPfade());

    [Fact]
    public void Befunde_gehoeren_ueber_die_TID_zu_ihrer_Untersuchung_auch_bei_gleichem_Namen()
    {
        Assert.Equal(
            new[] { "refSCHADEN1", "refSCHADEN2", "refSCHADEN3", "refSCHADEN4", "refSCHADEN4DOPPELT" },
            Bezuege.BefundeJeUntersuchung["refUNTERS1"].Select(b => b.KanalschadenTid));
        Assert.Equal(
            new[] { "refSCHADEN5", "refSCHADEN6" },
            Bezuege.BefundeJeUntersuchung["refUNTERS2"].Select(b => b.KanalschadenTid));
    }

    [Fact]
    public void Untersuchungen_werden_als_Haltung_Schacht_oder_ungeklaert_eingeordnet()
    {
        Assert.Equal(new[] { "refUNTERS1", "refUNTERS2", "refUNTERS6" }, Bezuege.Haltungsuntersuchungen.Select(u => u.Tid));
        Assert.Equal(new[] { "refUNTERS3", "refUNTERS7" }, Bezuege.Schachtbegehungen.Select(u => u.Tid));
        Assert.Equal(new[] { "400-401", "500" }, Bezuege.Offene.Select(o => o.Bezeichnung));
        Assert.Equal(8, Bezuege.Untersuchungen);
    }

    [Fact]
    public void Nicht_Zuordenbares_bleibt_im_Zwischenergebnis_sichtbar()
    {
        // Nicht uebernommen, seit 01.10.2026 gemeldet (VsaKekLueckenmeldungen): Untersuchung
        // ohne Bezeichnung, Schaeden ohne gueltigen Bezug, Foto zu einem fehlenden Kanalschaden.
        // Felder wie <KanalSchadencode> zaehlen nicht.
        Assert.Equal("refUNTERS8", Assert.Single(Bezuege.OhneBezeichnung).Tid);
        Assert.Equal(new[] { "refSCHADENVERWAIST", "refSCHADENOHNEREF" }, Bezuege.VerwaisteKanalschaeden.Select(k => k.Tid));
        Assert.Equal("refUNTERSFEHLT", Assert.Single(Bezuege.VerwaisteNormschachtschaeden).UntersuchungRef);
        Assert.Equal("foto_verwaist.jpg", Assert.Single(Bezuege.FotosOhneBefund).Bezeichnung);
    }

    [Fact]
    public void Foto_und_Video_finden_ihr_Ziel_und_das_erste_gewinnt()
    {
        var befunde = Bezuege.BefundeJeUntersuchung["refUNTERS1"];
        Assert.Equal("Fotos/foto_schaden2.jpg", befunde.Single(b => b.KanalschadenTid == "refSCHADEN2").FotoPath);
        Assert.Equal("Fotos/foto_schaden3.jpg", befunde.Single(b => b.KanalschadenTid == "refSCHADEN3").FotoPath); // ueber OBJ_ID
        Assert.Equal("Film/200-201.mpg", Bezuege.VideoJeUntersuchung["refUNTERS1"]);
        Assert.Equal("Film/200-201_gegen.mp4", Bezuege.VideoJeUntersuchung["refUNTERS2"]);
    }

    /// <summary>Loest jeden Pfad ohne Dateizugriff zu "Ordner/Name" auf.</summary>
    private sealed class NamensPfade : IVsaMediaPathResolver
    {
        public string ResolvePhoto(string xtfPath, string? relativeFolder, string? fileName) => $"{relativeFolder}/{fileName}";

        public string ResolveVideo(string xtfPath, string? relativeFolder, string? fileName) => $"{relativeFolder}/{fileName}";
    }
}
