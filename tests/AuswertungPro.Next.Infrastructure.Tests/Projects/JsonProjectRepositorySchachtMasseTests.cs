using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

/// <summary>
/// Ein Bestandsprojekt mit dem alten Textfeld "Dimension" wird beim Laden auf die zwei
/// Zahlenfelder umgestellt. Im Bestand vom 2026-09-03 betraf das 61 von 392 Schaechten.
/// </summary>
public sealed class JsonProjectRepositorySchachtMasseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"schachtmasse-{Guid.NewGuid():N}");

    public JsonProjectRepositorySchachtMasseTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* Aufraeumen ist Nebensache */ }
    }

    [Fact]
    public void Das_alte_Textfeld_wird_beim_Laden_in_die_zwei_Zahlenfelder_umgestellt()
    {
        var repo = new JsonProjectRepository();
        var pfad = Path.Combine(_dir, "projekt.json");

        var projekt = new Project { Name = "Alt" };
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "78998", FieldSource.Pdf, userEdited: false);
        schacht.SetFieldValue("Dimension", "1100 x 900 mm", FieldSource.Pdf, userEdited: false);
        projekt.SchaechteData.Add(schacht);
        Assert.True(repo.Save(projekt, pfad).Ok);

        var geladen = repo.Load(pfad);
        Assert.True(geladen.Ok, geladen.ErrorMessage);
        var s = Assert.Single(geladen.Value!.SchaechteData);

        Assert.Equal("1100", s.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("900", s.GetFieldValue(FieldKeys.ShaftDimension2Mm));
        Assert.Equal(FieldSource.Pdf, s.FieldMeta[FieldKeys.ShaftDimension1Mm].Source);
        Assert.False(s.Fields.ContainsKey("Dimension"));
        // Eine echte Aenderung: Sie muss beim naechsten Speichern festgehalten werden.
        Assert.True(geladen.Value.Dirty);
    }

    [Fact]
    public void Ein_Projekt_ohne_altes_Textfeld_bleibt_unveraendert()
    {
        var repo = new JsonProjectRepository();
        var pfad = Path.Combine(_dir, "projekt.json");

        var projekt = new Project { Name = "Neu" };
        var schacht = new SchachtRecord();
        schacht.SetFieldValue(FieldKeys.ShaftDimension1Mm, "600", FieldSource.Manual, userEdited: true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension2Mm, "600", FieldSource.Manual, userEdited: true);
        projekt.SchaechteData.Add(schacht);
        Assert.True(repo.Save(projekt, pfad).Ok);

        var geladen = repo.Load(pfad);
        Assert.True(geladen.Ok, geladen.ErrorMessage);
        Assert.False(geladen.Value!.Dirty);
        Assert.Equal("600", Assert.Single(geladen.Value.SchaechteData).GetFieldValue(FieldKeys.ShaftDimension2Mm));
    }
}
