using System;
using System.IO;
using System.Text;

using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierPlanWorkSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "dossier_plan_session_" + Guid.NewGuid().ToString("N"));
    private readonly DossierPlanPublicationService _publications = new();

    public DossierPlanWorkSessionTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Ein Aufraeumfehler darf den Testlauf nicht rot machen.
        }
    }

    [Fact]
    public void Jede_Vorschau_hat_einen_eigenen_Arbeitsordner()
    {
        var sibling = Path.Combine(_root, "fremd.txt");
        File.WriteAllText(sibling, "bleibt");

        using var second = new DossierPlanWorkSession(_root);
        var first = new DossierPlanWorkSession(_root);

        Assert.NotEqual(first.WorkFolder, second.WorkFolder);
        Assert.True(Directory.Exists(first.WorkFolder));
        Assert.True(Directory.Exists(second.WorkFolder));

        first.Dispose();

        Assert.False(Directory.Exists(first.WorkFolder));
        Assert.True(Directory.Exists(second.WorkFolder));
        Assert.True(File.Exists(sibling));
    }

    [Fact]
    public void Uebernehmen_veroeffentlicht_ohne_bestehende_Datei_zu_ersetzen()
    {
        var session = new DossierPlanWorkSession(_root);
        var arbeitsdatei = Path.Combine(session.WorkFolder, "plan.png");
        var neuerInhalt = Encoding.UTF8.GetBytes("neuer Plan");
        File.WriteAllBytes(arbeitsdatei, neuerInhalt);

        var zielordner = Path.Combine(_root, "dossier");
        Directory.CreateDirectory(zielordner);
        var vorhandenerPlan = Path.Combine(zielordner, "plan.png");
        var alterInhalt = Encoding.UTF8.GetBytes("bestehender Plan");
        File.WriteAllBytes(vorhandenerPlan, alterInhalt);

        var result = session.Publish(_publications, _root, arbeitsdatei, zielordner);

        Assert.True(result.Success, result.Error);
        Assert.Equal("plan (2).png", Path.GetFileName(result.ImagePath));
        Assert.Equal(alterInhalt, File.ReadAllBytes(vorhandenerPlan));
        Assert.Equal(neuerInhalt, File.ReadAllBytes(result.ImagePath!));
        Assert.True(File.Exists(arbeitsdatei));

        result.Publication!.Accept();
        result.Publication.Dispose();

        session.Dispose();

        Assert.False(Directory.Exists(session.WorkFolder));
        Assert.True(File.Exists(vorhandenerPlan));
        Assert.True(File.Exists(result.ImagePath));
    }

    [Fact]
    public void Verwerfen_laesst_das_Kundenoriginal_unveraendert()
    {
        var original = Path.Combine(_root, "kunde", "original.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        var originalInhalt = Encoding.UTF8.GetBytes("Kundenoriginal");
        File.WriteAllBytes(original, originalInhalt);

        var session = new DossierPlanWorkSession(_root);
        var zielordner = Path.Combine(_root, "dossier");

        var result = session.Publish(_publications, _root, original, zielordner);

        Assert.True(result.Success, result.Error);
        Assert.Equal(original, result.ImagePath);
        Assert.False(Directory.Exists(zielordner));

        session.Dispose();

        Assert.Equal(originalInhalt, File.ReadAllBytes(original));
    }

    [Fact]
    public void Fehler_beim_Uebernehmen_laesst_die_Arbeitsdatei_bestehen()
    {
        using var session = new DossierPlanWorkSession(_root);
        var arbeitsdatei = Path.Combine(session.WorkFolder, "plan.png");
        File.WriteAllText(arbeitsdatei, "Plan");

        var ungueltigesZiel = Path.Combine(session.WorkFolder, "ausgabe");
        var result = session.Publish(_publications, _root, arbeitsdatei, ungueltigesZiel);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.True(File.Exists(arbeitsdatei));
        Assert.False(Directory.Exists(ungueltigesZiel));
    }

    [Fact]
    public void Fehlende_Arbeitsdatei_wird_nicht_als_uebernommen_gemeldet()
    {
        using var session = new DossierPlanWorkSession(_root);
        var fehlend = Path.Combine(session.WorkFolder, "fehlt.png");

        var result = session.Publish(
            _publications,
            _root,
            fehlend,
            Path.Combine(_root, "dossier"));

        Assert.False(result.Success);
        Assert.Contains("nicht gefunden", result.Error);
    }
}
