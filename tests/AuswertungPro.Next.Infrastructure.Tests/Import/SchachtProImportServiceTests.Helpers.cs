using System.IO.Compression;
using System.Text;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Test-Helfer: baut ein .spro-Archiv zur Laufzeit als echtes ZIP
/// (manifest.json, projects/&lt;exportId&gt;.json, photos/...). Kein Binaer-Fixture.
/// JSON-Feldnamen = exakte Gson-Namen der App (ProjectArchive.kt).
/// </summary>
public sealed partial class SchachtProImportServiceTests
{
    private static string Manifest(string id, string name)
        => Manifest((id, name));

    private static string Manifest(params (string Id, string Name)[] projekte)
    {
        var entries = string.Join(",", projekte.Select(p =>
            $$"""{"exportId":"{{p.Id}}","name":"{{p.Name}}","protocolCount":1,"photoCount":0,"hasLogo":false}"""));
        return $$"""{"formatVersion":1,"dbSchemaVersion":21,"appVersionName":"4.5.0","appVersionCode":45,"exportedAtMillis":1752174000000,"projectCount":{{projekte.Length}},"projects":[{{entries}}]}""";
    }

    private static string Snapshot(string exportId, string name, string protokolle, string mode = "PRO")
        => $$"""{"exportId":"{{exportId}}","project":{"name":"{{name}}","auftraggeberName":"Gemeinde Uri","mode":"{{mode}}"},"protocols":[{{protokolle}}]}""";

    /// <summary>Identitaet — macht den Aufrufstellen die Protokoll-JSON-Rohheit sichtbar.</summary>
    private static string Protokoll(string json) => json;

    private static string ErzeugeArchiv(
        TempDir temp,
        string manifest,
        Dictionary<string, string> projekte,
        Dictionary<string, byte[]>? fotos = null)
    {
        var pfad = Path.Combine(temp.Root, $"test-{Guid.NewGuid():N}.spro");
        using var fs = File.Create(pfad);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        SchreibeTextEintrag(zip, "manifest.json", manifest);
        foreach (var (exportId, json) in projekte)
            SchreibeTextEintrag(zip, $"projects/{exportId}.json", json);

        if (fotos is not null)
        {
            foreach (var (name, bytes) in fotos)
            {
                var entry = zip.CreateEntry(name);
                using var stream = entry.Open();
                stream.Write(bytes);
            }
        }

        return pfad;
    }

    private static void SchreibeTextEintrag(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    /// <summary>Happy-Path-Archiv: Projekt "Uri 2026" mit S-100 (rund) und S-200 (oval) + 3 Fotos.</summary>
    private static string ErzeugeArchivMitZweiProtokollen(TempDir temp)
    {
        var protokoll1 = """
            {
              "schachtNr":"S-100",
              "datum":"12.07.2026",
              "wetter":"schoen_trocken",
              "schachtFunktion":"Kontrollschacht",
              "dimension":"1000",
              "tiefe":"2.50",
              "materialSchacht":"Beton",
              "medium":"Mischabwasser",
              "schachtform":"rund",
              "bemerkungen":"",
              "deckelZustand":{"gerissen":true},
              "konusZustand":{"korrodiert":true},
              "leiterSteigeisen":{"steigeisen":true,"verrostet":true},
              "anschluesse":[{"nr":1,"typ":"Anschluss","medium":"Mischabwasser","dn":"150","tiefe":"1.80","material":"PVC","uhr":"3","richtung":"","rohrform":"Rund","breite":"","hoehe":"","zustand":{"gerissen":true}}],
              "lv95East":2683947.125,
              "lv95North":1192844.5,
              "photos":[{"archivePath":"photos/XP1/0_0.jpg","rotation":0,"description":""}]
            }
            """;

        var protokoll2 = """
            {
              "schachtNr":"S-200",
              "datum":"12.07.2026",
              "schachtform":"oval",
              "laenge":"800",
              "breite":"600",
              "bankettZustand":{"Ablagerungen":true},
              "tauchbogen":{"fehlt":true},
              "photos":[{"archivePath":"photos/XP1/1_0.jpg"},{"archivePath":"photos/XP1/1_1.jpg"}]
            }
            """;

        byte[] foto1 = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 };
        byte[] foto2 = { 0xFF, 0xD8, 0xFF, 0xE0, 5, 6, 7, 8 };
        byte[] foto3 = { 0xFF, 0xD8, 0xFF, 0xE0, 9, 10, 11, 12 };

        return ErzeugeArchiv(
            temp,
            Manifest(("XP1", "Uri 2026")),
            new Dictionary<string, string>
            {
                ["XP1"] = Snapshot("XP1", "Uri 2026", protokoll1 + "," + protokoll2)
            },
            new Dictionary<string, byte[]>
            {
                ["photos/XP1/0_0.jpg"] = foto1,
                ["photos/XP1/1_0.jpg"] = foto2,
                ["photos/XP1/1_1.jpg"] = foto3
            });
    }

    private static IImportFileStagingSession BeginStaging(TempDir temp)
        => new ImportFileStagingService().Begin(temp.ProjectFile)
           ?? throw new InvalidOperationException("Staging-Sitzung fehlt.");

    private static ImportRunContext Ctx(IImportFileStagingSession session)
        => new(CancellationToken.None, null, new ImportRunLog(), dryRun: false, collectionLock: null, fileStaging: session);

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "schachtpro-test-" + Guid.NewGuid().ToString("N"));
            ProjectRoot = Path.Combine(Root, "Projekt");
            Directory.CreateDirectory(ProjectRoot);
            ProjectFile = Path.Combine(ProjectRoot, "Projektdateien", "projekt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectFile)!);
            File.WriteAllText(ProjectFile, "{}");
        }

        public string Root { get; }
        public string ProjectRoot { get; }
        public string ProjectFile { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Temp-Bereinigung ist best-effort
            }
        }
    }
}
