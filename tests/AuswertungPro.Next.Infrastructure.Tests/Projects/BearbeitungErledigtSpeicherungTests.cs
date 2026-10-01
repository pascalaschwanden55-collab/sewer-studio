using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class BearbeitungErledigtSpeicherungTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "erledigt-" + Guid.NewGuid().ToString("N"));

    public BearbeitungErledigtSpeicherungTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Markierung_ueberlebt_Speichern_Kopieren_und_Laden_ohne_Sanierungsstatus_zu_aendern()
    {
        var p = new Project();
        var h = new HaltungRecord();
        var s = new SchachtRecord();
        h.SetFieldValue(FieldKeys.WorkflowStatus, "offen", FieldSource.Manual, true);
        s.SetFieldValue("Status\noffen/abgeschlossen", "abgeschlossen", FieldSource.Manual, true);
        p.Data.Add(h); p.SchaechteData.Add(s);
        var signatur = new JsonProjectContentSignature();
        var vorher = signatur.Compute(p);
        h.BearbeitungErledigt = true;
        Assert.NotEqual(vorher, signatur.Compute(p));
        var nurHaltung = signatur.Compute(p);
        s.BearbeitungErledigt = true;
        Assert.NotEqual(nurHaltung, signatur.Compute(p));

        var repo = new JsonProjectRepository();
        var pfad = Path.Combine(_root, "projekt.json");
        Assert.True(repo.Save(p, pfad).Ok);
        var geladen = repo.Load(pfad);
        Assert.True(geladen.Ok, geladen.ErrorMessage);
        var kopie = repo.DeepCopy(geladen.Value!);
        Assert.True(kopie.Data.Single().BearbeitungErledigt);
        Assert.True(kopie.SchaechteData.Single().BearbeitungErledigt);
        Assert.Equal("offen", kopie.Data.Single().GetFieldValue(FieldKeys.WorkflowStatus));
        Assert.Equal("abgeschlossen", kopie.SchaechteData.Single().GetFieldValue("Status\noffen/abgeschlossen"));
        Assert.DoesNotContain("BearbeitungErledigt", kopie.Data.Single().Fields.Keys);
        Assert.DoesNotContain("BearbeitungErledigt", kopie.SchaechteData.Single().Fields.Keys);

        kopie.Data.Single().BearbeitungErledigt = false;
        kopie.SchaechteData.Single().BearbeitungErledigt = false;
        Assert.True(repo.Save(kopie, pfad).Ok);
        var wiederOffen = repo.Load(pfad).Value!;
        Assert.False(wiederOffen.Data.Single().BearbeitungErledigt);
        Assert.False(wiederOffen.SchaechteData.Single().BearbeitungErledigt);
        Assert.True(h.BearbeitungErledigt);
        Assert.True(s.BearbeitungErledigt);
    }

    [Fact]
    public void Altprojekt_mit_abgeschlossener_Sanierung_erhaelt_keine_Erledigtmarke()
    {
        var pfad = Path.Combine(_root, "projekt.json");
        File.WriteAllText(pfad, """
            { "Version": 2,
              "Data": [{ "Fields": { "Offen_abgeschlossen": "abgeschlossen" } }],
              "SchaechteData": [{ "Fields": { "Status\noffen/abgeschlossen": "abgeschlossen" } }]
            }
            """);
        var result = new JsonProjectRepository().Load(pfad);
        Assert.True(result.Ok, result.ErrorMessage);
        Assert.False(result.Value!.Data.Single().BearbeitungErledigt);
        Assert.False(result.Value.SchaechteData.Single().BearbeitungErledigt);
    }
}
