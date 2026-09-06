using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Schutztests fuer das ALTE VSA_KEK-Modell (15.07.2008), bevor am Leser gearbeitet wird
/// (Arbeitspaket 0 des Uebergabeplans vom 2026-09-05).
///
/// Diese Tests halten fest, was HEUTE schon richtig ist: Eine Haltungsuntersuchung aus
/// einer alten WinCan-VX-XTF wird mit Name, beiden Schaechten und ihren Kanalschaeden
/// eingelesen. Die spaeteren Arbeitspakete duerfen das nicht kaputt machen.
///
/// Was heute falsch ist (Schachtbegehungen landen als Haltungen), wird bewusst NICHT
/// hier festgeschrieben — der Soll-Test dazu entsteht in Arbeitspaket 3.
/// </summary>
public sealed class AlteVsaKekQuelleSchutzTests
{
    [Fact]
    public void AlteVsaKekDatei_HaltungWirdMitSchaechtenUndSchaedenGelesen()
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"vsakek-alt-{Guid.NewGuid():N}");
        var datei = Path.Combine(ordner, "Zone_Test.xtf");
        AlteVsaKekTestquelle.SchreibeDatei(
            datei,
            haltungen: [new AlteVsaKekTestquelle.Haltung("chU0000000000001", "327015-2414", "327015", "2414", AnzahlKanalschaeden: 7)]);

        try
        {
            var project = new Project();
            var stats = new LegacyXtfImportService().ImportXtfFiles([datei], project);
            var protokoll = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message}"));

            Assert.Equal(0, stats.Errors);
            Assert.True(project.Data.Count > 0, protokoll);
            var record = Assert.Single(project.Data);
            Assert.Equal("327015-2414", record.GetFieldValue("Haltungsname"));
            Assert.Equal("327015", record.GetFieldValue("Schacht_oben"));
            Assert.Equal("2414", record.GetFieldValue("Schacht_unten"));
            Assert.Equal(7, record.VsaFindings.Count);
        }
        finally
        {
            try { Directory.Delete(ordner, recursive: true); } catch { }
        }
    }

    [Fact]
    public void AlteVsaKekDatei_MehrereHaltungenBleibenGetrennt()
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"vsakek-alt-{Guid.NewGuid():N}");
        var datei = Path.Combine(ordner, "Zone_Test.xtf");
        AlteVsaKekTestquelle.SchreibeDatei(
            datei,
            haltungen:
            [
                new AlteVsaKekTestquelle.Haltung("chU0000000000001", "3164-3180", "3164", "3180", AnzahlKanalschaeden: 2),
                new AlteVsaKekTestquelle.Haltung("chU0000000000002", "3182-2200", "3182", "2200", AnzahlKanalschaeden: 3)
            ]);

        try
        {
            var project = new Project();
            var stats = new LegacyXtfImportService().ImportXtfFiles([datei], project);

            Assert.Equal(0, stats.Errors);
            Assert.Equal(2, project.Data.Count);
            Assert.Contains(project.Data, r => r.GetFieldValue("Haltungsname") == "3164-3180" && r.VsaFindings.Count == 2);
            Assert.Contains(project.Data, r => r.GetFieldValue("Haltungsname") == "3182-2200" && r.VsaFindings.Count == 3);
        }
        finally
        {
            try { Directory.Delete(ordner, recursive: true); } catch { }
        }
    }
}
