using System;
using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 6 des Uebergabeplans vom 2026-09-05: Beide Videos in allen Importwegen
/// uebernehmen.
///
/// Vorher (Stand c1021e76e) lief nur das Feld <c>Link</c> durch den Rueckfall-Kopierweg.
/// Das Gegeninspektionsvideo blieb als absoluter Pfad auf die Kundenquelle stehen — der
/// Bericht meldete trotzdem "1 Video, 0 Fehler". In einer Kopie des fertigen Projekts
/// war es damit nicht mehr abspielbar.
/// </summary>
public sealed class GegenvideoVerteilungTests
{
    [Fact]
    public void HauptUndGegenvideo_LandenBeideImProjektUndSindRelativVerlinkt()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            var haupt = Schreibe(quelle, "100-200.mpg", "Hinfahrt");
            var gegen = Schreibe(quelle, "100-200_G.mpg", "Rueckfahrt");
            var record = Haltung(projekt, "100-200", haupt, gegen);

            var ergebnis = Verteile(projekt, projektOrdner, quelle);

            Assert.Equal(0, ergebnis.Errors);
            Assert.Equal(2, ergebnis.VideosDistributed);

            var linkHaupt = record.GetFieldValue(FieldKeys.Link);
            var linkGegen = record.GetFieldValue("Link_G");
            Assert.False(Path.IsPathRooted(linkHaupt), $"Hauptvideo muss relativ sein: {linkHaupt}");
            Assert.False(Path.IsPathRooted(linkGegen), $"Gegenvideo muss relativ sein: {linkGegen}");
            Assert.True(File.Exists(Path.Combine(projektOrdner, linkHaupt)));
            Assert.True(File.Exists(Path.Combine(projektOrdner, linkGegen)));

            // Die bestehende Zielnamenkonvention bleibt: "-g" kennzeichnet die Gegenfahrt.
            Assert.EndsWith("-g.mpg", linkGegen, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("-g.mpg", linkHaupt, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void NurGegenvideo_WirdEbenfallsUebernommen()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            var gegen = Schreibe(quelle, "100-200_G.mpg", "Rueckfahrt");
            var record = Haltung(projekt, "100-200", haupt: null, gegen: gegen);

            var ergebnis = Verteile(projekt, projektOrdner, quelle);

            Assert.Equal(0, ergebnis.Errors);
            Assert.Equal(1, ergebnis.VideosDistributed);
            Assert.True(File.Exists(Path.Combine(projektOrdner, record.GetFieldValue("Link_G"))));
        });
    }

    [Fact]
    public void HauptvideoErfolgreich_GegenkopieFehlerhaft_ReisstDasHauptvideoNichtMit()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            var haupt = Schreibe(quelle, "100-200.mpg", "Hinfahrt");
            var record = Haltung(projekt, "100-200", haupt, Path.Combine(quelle, "gibt-es-nicht.mpg"));

            var ergebnis = Verteile(projekt, projektOrdner, quelle);

            // Das Hauptvideo ist verteilt und relativ verlinkt …
            Assert.Equal(1, ergebnis.VideosDistributed);
            Assert.True(File.Exists(Path.Combine(projektOrdner, record.GetFieldValue(FieldKeys.Link))));
            // … und die fehlende Gegenkopie ist sichtbar, nicht still.
            Assert.Contains(ergebnis.Messages, m => m.Contains("100-200-g", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void BereitsRelativeLinks_BleibenUnangetastet()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            var record = Haltung(projekt, "100-200", null, null);
            record.SetFieldValue(FieldKeys.Link, @"Haltungen_Verteilt\100-200\a.mpg", FieldSource.Legacy, userEdited: false);
            record.SetFieldValue("Link_G", @"Haltungen_Verteilt\100-200\a-g.mpg", FieldSource.Legacy, userEdited: false);

            var ergebnis = Verteile(projekt, projektOrdner, quelle);

            Assert.Equal(0, ergebnis.VideosDistributed);
            Assert.Equal(@"Haltungen_Verteilt\100-200\a.mpg", record.GetFieldValue(FieldKeys.Link));
            Assert.Equal(@"Haltungen_Verteilt\100-200\a-g.mpg", record.GetFieldValue("Link_G"));
        });
    }

    [Fact]
    public void ZweiterIdentischerLauf_ErzeugtKeineZweiteKopie()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            var haupt = Schreibe(quelle, "100-200.mpg", "Hinfahrt");
            var gegen = Schreibe(quelle, "100-200_G.mpg", "Rueckfahrt");
            Haltung(projekt, "100-200", haupt, gegen);

            Verteile(projekt, projektOrdner, quelle);
            var nachErstemLauf = Directory
                .EnumerateFiles(projektOrdner, "*.mpg", SearchOption.AllDirectories)
                .Count();

            // Zweiter Lauf: die Links sind jetzt relativ, es gibt nichts mehr zu kopieren.
            Verteile(projekt, projektOrdner, quelle);
            var nachZweitemLauf = Directory
                .EnumerateFiles(projektOrdner, "*.mpg", SearchOption.AllDirectories)
                .Count();

            Assert.Equal(2, nachErstemLauf);
            Assert.Equal(nachErstemLauf, nachZweitemLauf);
        });
    }

    [Fact]
    public void KopiertesProjekt_BehaeltBeideLinks()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            var haupt = Schreibe(quelle, "100-200.mpg", "Hinfahrt");
            var gegen = Schreibe(quelle, "100-200_G.mpg", "Rueckfahrt");
            var record = Haltung(projekt, "100-200", haupt, gegen);

            Verteile(projekt, projektOrdner, quelle);

            // Das fertige Projekt an einen anderen Ort kopieren — beide Links muessen
            // dort weiterhin aufloesbar sein, weil sie relativ sind.
            var kopie = projektOrdner + "_kopie";
            KopiereBaum(projektOrdner, kopie);

            Assert.True(File.Exists(Path.Combine(kopie, record.GetFieldValue(FieldKeys.Link))));
            Assert.True(File.Exists(Path.Combine(kopie, record.GetFieldValue("Link_G"))));
        });
    }

    // ---------------------------------------------------------------------

    private static KanalImportDistributor.Result Verteile(
        Project projekt, string projektOrdner, string quelle)
        => new KanalImportDistributionService().Distribute(
            projekt,
            projektOrdner,
            archivedPdfDir: Path.Combine(projektOrdner, "Importdateien", "PDF"),
            sourceVideoDir: quelle,
            splitPdf: false,
            primaryProtocolPdf: null,
            fileStaging: null);

    private static HaltungRecord Haltung(Project projekt, string name, string? haupt, string? gegen)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("Datum_Jahr", "02.07.2020", FieldSource.Legacy, userEdited: false);
        if (haupt is not null)
            record.SetFieldValue(FieldKeys.Link, haupt, FieldSource.Legacy, userEdited: false);
        if (gegen is not null)
            record.SetFieldValue("Link_G", gegen, FieldSource.Legacy, userEdited: false);
        projekt.Data.Add(record);
        return record;
    }

    private static void MitProjekt(Action<string, Project, string> pruefung)
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"ap6-video-{Guid.NewGuid():N}");
        var quelle = Path.Combine(wurzel, "quelle");
        var projektOrdner = Path.Combine(wurzel, "projekt");
        Directory.CreateDirectory(quelle);
        Directory.CreateDirectory(projektOrdner);

        try { pruefung(quelle, new Project(), projektOrdner); }
        finally { try { Directory.Delete(wurzel, recursive: true); } catch { } }
    }

    private static string Schreibe(string ordner, string name, string inhalt)
    {
        var pfad = Path.Combine(ordner, name);
        File.WriteAllText(pfad, inhalt, new UTF8Encoding(false));
        return pfad;
    }

    private static void KopiereBaum(string quelle, string ziel)
    {
        Directory.CreateDirectory(ziel);
        foreach (var ordner in Directory.EnumerateDirectories(quelle, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(ordner.Replace(quelle, ziel, StringComparison.Ordinal));
        foreach (var datei in Directory.EnumerateFiles(quelle, "*", SearchOption.AllDirectories))
            File.Copy(datei, datei.Replace(quelle, ziel, StringComparison.Ordinal), overwrite: true);
    }
}
