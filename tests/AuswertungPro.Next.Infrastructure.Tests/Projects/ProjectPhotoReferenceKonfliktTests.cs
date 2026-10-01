using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

/// <summary>
/// F3 (Fehleranalyse 17.09.2026): Ein Dateiname ist kein Identitaetsnachweis. Zwei
/// gleichnamige Fotos mit verschiedenem Inhalt duerfen nicht gegeneinander getauscht
/// werden - sonst zeigt ein Befund plötzlich ein anderes Bild.
/// </summary>
public sealed class ProjectPhotoReferenceKonfliktTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "foto-konflikt", Guid.NewGuid().ToString("N"));

    public ProjectPhotoReferenceKonfliktTests() => Directory.CreateDirectory(_root);

    private string ProjektDatei => Path.Combine(_root, "Projektdateien", "projekt.json");

    private string LegeAn(string relativerPfad, string inhalt)
    {
        var voll = Path.Combine(_root, relativerPfad.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
        File.WriteAllText(voll, inhalt);
        return voll;
    }

    private static (Project Projekt, ProtocolEntry Eintrag, VsaFinding Befund) ProjektMitFoto(string fotoPfad)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "06-001", FieldSource.Manual, userEdited: true);
        var eintrag = new ProtocolEntry { Code = "BAB", FotoPaths = [fotoPfad] };
        record.Protocol = new ProtocolDocument { Current = new ProtocolRevision() };
        record.Protocol.Current.Entries.Add(eintrag);
        var befund = new VsaFinding { KanalSchadencode = "BAB", FotoPath = fotoPfad };
        record.VsaFindings.Add(befund);
        var projekt = new Project();
        projekt.Data.Add(record);
        return (projekt, eintrag, befund);
    }

    [Fact]
    public void Normalize_GleicherNameAndererInhalt_BehaeltDenBestehendenVerweis()
    {
        var bestehend = LegeAn("extern/foto.jpg", "BEABSICHTIGTES FOTO");
        LegeAn("Fotos/Haltungen/06-001/foto.jpg", "ANDERES FOTO");
        var (projekt, eintrag, befund) = ProjektMitFoto(bestehend);

        var geaendert = new ProjectPhotoReferenceNormalizationService().Normalize(projekt, ProjektDatei);

        Assert.Equal(0, geaendert);
        Assert.Equal(bestehend, Assert.Single(eintrag.FotoPaths));
        Assert.Equal(bestehend, befund.FotoPath);
        Assert.False(projekt.Dirty);
    }

    [Fact]
    public void Normalize_GleicherNameGleicherInhalt_StelltAufDieProjektablageUm()
    {
        var bestehend = LegeAn("extern/foto.jpg", "DASSELBE BILD");
        LegeAn("Fotos/Haltungen/06-001/foto.jpg", "DASSELBE BILD");
        var (projekt, eintrag, _) = ProjektMitFoto(bestehend);

        new ProjectPhotoReferenceNormalizationService().Normalize(projekt, ProjektDatei);

        Assert.Equal("Fotos/Haltungen/06-001/foto.jpg", Assert.Single(eintrag.FotoPaths));
    }

    [Fact]
    public void Normalize_BestehenderVerweisFehlt_WirdWeiterhinRepariert()
    {
        var verschwunden = Path.Combine(_root, "Importdateien", "XTF", "Foto", "foto.jpg");
        LegeAn("Fotos/Haltungen/06-001/foto.jpg", "BILD");
        var (projekt, eintrag, _) = ProjektMitFoto(verschwunden);

        new ProjectPhotoReferenceNormalizationService().Normalize(projekt, ProjektDatei);

        Assert.Equal("Fotos/Haltungen/06-001/foto.jpg", Assert.Single(eintrag.FotoPaths));
    }

    [Fact]
    public void Normalize_KonfliktInOriginalUndHistorie_BleibtEbenfallsErhalten()
    {
        var bestehend = LegeAn("extern/foto.jpg", "BEABSICHTIGTES FOTO");
        LegeAn("Fotos/Haltungen/06-001/foto.jpg", "ANDERES FOTO");
        var (projekt, _, _) = ProjektMitFoto(bestehend);
        var protokoll = projekt.Data[0].Protocol!;
        var original = new ProtocolEntry { Code = "BAB", FotoPaths = [bestehend] };
        var historisch = new ProtocolEntry { Code = "BAB", FotoPaths = [bestehend] };
        protokoll.Original = new ProtocolRevision();
        protokoll.Original.Entries.Add(original);
        var alteFassung = new ProtocolRevision();
        alteFassung.Entries.Add(historisch);
        protokoll.History.Add(alteFassung);

        new ProjectPhotoReferenceNormalizationService().Normalize(projekt, ProjektDatei);

        Assert.Equal(bestehend, Assert.Single(original.FotoPaths));
        Assert.Equal(bestehend, Assert.Single(historisch.FotoPaths));
    }

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
}
