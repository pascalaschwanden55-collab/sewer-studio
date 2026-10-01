using System;
using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Buerglen 2026-09-09: In jedem der 19 Haltungsordner lag dasselbe Video zweimal —
/// einmal als <c>20260902_&lt;Haltung&gt;.mp4</c> (Feld <c>Link</c>, vom
/// <see cref="HoldingFolderDistributor"/> verteilt) und einmal als
/// <c>00000000_&lt;Haltung&gt;-aufnahme-&lt;Hash&gt;.mp4</c> (aus
/// <see cref="ProtocolRevision.ImportVideoPaths"/>). Beide Dateien waren bytegleich;
/// 3397 von 6803 MB des Ordners waren reine Dopplung.
///
/// Ursache: Die Sperre <c>verteiltePfade</c> merkt sich als Schluessel den WERT von
/// <c>Link</c> — und der ist zu diesem Zeitpunkt bereits der relative Projektpfad.
/// <c>ImportVideoPaths</c> traegt aber noch den absoluten Quellpfad. Zwei verschiedene
/// Zeichenketten fuer dieselbe Datei ergeben keinen Treffer, also wurde ein zweites Mal
/// kopiert. Die zweite Sperre (liegt dieselbe Datei schon am Ziel?) prueft nur den einen
/// Wunschnamen und sieht die bereits vorhandene Kopie unter ihrem Datumsnamen nicht.
/// </summary>
public sealed class VideoDoppelkopieTests
{
    [Fact]
    public void BereitsVerteiltesVideo_WirdAusImportVideoPathsNichtZweitesMalKopiert()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            // Ausgangslage wie nach dem Haltungs-Verteiler: die Kopie liegt schon im
            // Projekt und "Link" zeigt relativ darauf.
            var quellvideo = Schreibe(quelle, "H66_00010.mp4", "Kamerafahrt");
            var haltungsOrdner = Path.Combine(projektOrdner, "Haltungen_Verteilt", "100-200");
            Directory.CreateDirectory(haltungsOrdner);
            var bereitsVerteilt = Path.Combine(haltungsOrdner, "20260902_100-200.mp4");
            File.Copy(quellvideo, bereitsVerteilt);

            var record = Haltung(projekt, "100-200");
            record.SetFieldValue(
                FieldKeys.Link,
                @"Haltungen_Verteilt\100-200\20260902_100-200.mp4",
                FieldSource.Legacy,
                userEdited: false);
            SetzeImportVideos(record, quellvideo);

            var ergebnis = Verteile(projekt, projektOrdner, quelle);

            Assert.Equal(0, ergebnis.Errors);
            var videos = Directory
                .EnumerateFiles(projektOrdner, "*.mp4", SearchOption.AllDirectories)
                .ToList();
            Assert.Single(videos);

            // Der Verweis der Untersuchung zeigt auf genau diese eine Datei.
            var pfad = Assert.Single(record.Protocol!.Current.ImportVideoPaths!);
            Assert.False(Path.IsPathRooted(pfad), $"Verweis muss relativ sein: {pfad}");
            Assert.Equal(
                Path.GetFullPath(bereitsVerteilt),
                Path.GetFullPath(Path.Combine(projektOrdner, pfad)));
        });
    }

    [Fact]
    public void SelberLauf_HauptvideoUndImportVideoPath_ErgebenNurEineKopie()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            var quellvideo = Schreibe(quelle, "H66_00010.mp4", "Kamerafahrt");
            var record = Haltung(projekt, "100-200");
            record.SetFieldValue(FieldKeys.Link, quellvideo, FieldSource.Legacy, userEdited: false);
            SetzeImportVideos(record, quellvideo);

            var ergebnis = Verteile(projekt, projektOrdner, quelle);

            Assert.Equal(0, ergebnis.Errors);
            Assert.Single(Directory.EnumerateFiles(projektOrdner, "*.mp4", SearchOption.AllDirectories));
            Assert.Equal(
                record.GetFieldValue(FieldKeys.Link),
                Assert.Single(record.Protocol!.Current.ImportVideoPaths!));
        });
    }

    [Fact]
    public void EchtWeiteresVideo_WirdWeiterhinVerteilt()
    {
        MitProjekt((quelle, projekt, projektOrdner) =>
        {
            var haupt = Schreibe(quelle, "100-200.mp4", "Hinfahrt");
            var weiteres = Schreibe(quelle, "100-200_wiederholung.mp4", "Nachkontrolle");
            var record = Haltung(projekt, "100-200");
            record.SetFieldValue(FieldKeys.Link, haupt, FieldSource.Legacy, userEdited: false);
            SetzeImportVideos(record, weiteres);

            var ergebnis = Verteile(projekt, projektOrdner, quelle);

            Assert.Equal(0, ergebnis.Errors);
            Assert.Equal(2, Directory.EnumerateFiles(projektOrdner, "*.mp4", SearchOption.AllDirectories).Count());
            var pfad = Assert.Single(record.Protocol!.Current.ImportVideoPaths!);
            Assert.False(Path.IsPathRooted(pfad));
            Assert.Equal("Nachkontrolle", File.ReadAllText(Path.Combine(projektOrdner, pfad)));
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

    private static HaltungRecord Haltung(Project projekt, string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("Datum_Jahr", "02.09.2026", FieldSource.Legacy, userEdited: false);
        projekt.Data.Add(record);
        return record;
    }

    private static void SetzeImportVideos(HaltungRecord record, params string[] pfade)
    {
        record.Protocol ??= new ProtocolDocument();
        record.Protocol.Current.ImportVideoPaths = pfade.ToList();
    }

    private static void MitProjekt(Action<string, Project, string> pruefung)
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"videodoppel-{Guid.NewGuid():N}");
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
}
