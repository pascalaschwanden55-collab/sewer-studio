using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

/// <summary>
/// Die Content-Signatur ist die Grundlage der U4-Konflikterkennung: Sie muss echte
/// Datenaenderungen erfassen, aber gegen instabile Meta-Felder (ModifiedAtUtc/Dirty/
/// LastCommittedImportTxId) unempfindlich sein — sonst gaebe es Fehl-Positive.
/// </summary>
public sealed class JsonProjectContentSignatureTests
{
    [Fact]
    public void Compute_ist_deterministisch()
    {
        var sig = new JsonProjectContentSignature();
        var project = new Project { Name = "P" };

        var s1 = sig.Compute(project);
        var s2 = sig.Compute(project);

        Assert.Equal(s1, s2);
        Assert.NotEmpty(s1);
    }

    [Fact]
    public void Compute_ist_stabil_gegen_Metafelder_aber_sensitiv_fuer_Inhalt()
    {
        var sig = new JsonProjectContentSignature();
        var project = new Project { Name = "P" };
        var rec = project.CreateNewRecord();
        rec.SetFieldValue("Haltungsname", "100-200", FieldSource.Legacy, userEdited: false);
        project.Data.Add(rec);

        var baseline = sig.Compute(project);

        // Nur Meta-Felder aendern -> gleiche Signatur.
        project.ModifiedAtUtc = project.ModifiedAtUtc.AddHours(1);
        project.Dirty = !project.Dirty;
        Assert.Equal(baseline, sig.Compute(project));

        // Echten Feldwert aendern -> andere Signatur.
        rec.SetFieldValue("Haltungsname", "300-400", FieldSource.Legacy, userEdited: false);
        Assert.NotEqual(baseline, sig.Compute(project));
    }
}
