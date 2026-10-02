using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

/// <summary>
/// Gold-Altarchiv-Wiederherstellung: Wissensordner und Altarchiv duerfen keine Verknuepfung sein.
/// Die Pruefung laeuft ueber den gemeinsamen VerknuepfungsSchutz (Deepscan 02.10.2026, A5).
/// </summary>
public sealed class PersonalGoldArchiveRecoveryInputVerknuepfungTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gold_altarchiv_" + Guid.NewGuid().ToString("N"));

    public PersonalGoldArchiveRecoveryInputVerknuepfungTests() => Directory.CreateDirectory(_root);

    [JunctionFact]
    public void ValidateAndResolve_sperrt_ein_verknuepftes_Altarchiv()
    {
        var aktiv = Directory.CreateDirectory(Path.Combine(_root, "aktiv")).FullName;
        var fremd = Directory.CreateDirectory(Path.Combine(_root, "fremd")).FullName;
        var altarchiv = Path.Combine(_root, "altarchiv");
        JunctionTestSupport.CreateDirectoryLink(altarchiv, fremd);

        var fehler = Assert.Throws<InvalidDataException>(() => PersonalGoldArchiveRecoveryInput.ValidateAndResolve(
            new PersonalGoldArchiveRecoveryRequest(aktiv, altarchiv, "Pascal", DateTimeOffset.UtcNow, ["BAB"], DryRun: true)));

        Assert.Contains("Verknüpfung", fehler.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        try
        {
            var link = Path.Combine(_root, "altarchiv");
            if (Directory.Exists(link))
                Directory.Delete(link);
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }
}
