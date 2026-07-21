using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Teacher;

public sealed class VsaYoloClassMapFileStoreTests : IDisposable
{
    private const string ManifestHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "VsaYoloClassMapFileStoreTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetClassId_ist_strikt_lesend_und_schreibt_keine_fehlende_Datei()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        IVsaYoloClassMapStore store = new VsaYoloClassMapFileStore(mapPath);

        Assert.Equal(15, store.GetClassId("BAA"));
        Assert.Throws<KeyNotFoundException>(() => store.GetClassId("BZZ-extra"));
        Assert.Throws<ArgumentException>(() => store.GetClassId("  "));

        Assert.False(File.Exists(mapPath));
        Assert.False(File.Exists(Path.Combine(_root, "classes.txt")));
    }

    [Fact]
    public async Task GetOrAdd_bewahrt_Legacyformat_und_neue_ID_ueber_Dateirundlauf()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        IVsaYoloClassMapStore first = new VsaYoloClassMapFileStore(mapPath);

        Assert.Equal(15, first.GetOrAddClassId("BAA"));
        var newId = first.GetOrAddClassId("BZZ-extra");
        Assert.Equal(16, newId);

        IVsaYoloClassMapStore reloaded = new VsaYoloClassMapFileStore(mapPath);
        Assert.Equal(newId, reloaded.GetClassId("BZZ"));

        using var stored = JsonDocument.Parse(File.ReadAllText(mapPath));
        Assert.False(stored.RootElement.TryGetProperty("version", out _));
        Assert.Equal(newId, stored.RootElement.GetProperty("BZZ").GetInt32());
        Assert.Contains("BZZ", File.ReadAllLines(Path.Combine(_root, "classes.txt")));

        var exportPath = Path.Combine(_root, "export", "classes.txt");
        await reloaded.ExportClassesTxtAsync(exportPath);
        Assert.Contains("BAA", File.ReadAllLines(exportPath));
    }

    [Fact]
    public void GetOrAdd_ist_bei_deaktivierter_Konstruktoroption_gesperrt()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        File.WriteAllText(mapPath, "{\"BAA\":0}");
        var before = File.ReadAllBytes(mapPath);
        IVsaYoloClassMapStore store = new VsaYoloClassMapFileStore(
            mapPath,
            allowAutomaticClassCreation: false);

        Assert.Equal(0, store.GetClassId("BAA"));
        var error = Assert.Throws<InvalidOperationException>(
            () => store.GetOrAddClassId("BAA"));

        Assert.Contains("deaktiviert", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllBytes(mapPath));
    }

    [Fact]
    public void Kaputte_vorhandene_Datei_ist_ein_harter_Fehler_ohne_Ueberschreiben()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        File.WriteAllText(mapPath, "{ kaputt");
        var before = File.ReadAllBytes(mapPath);
        var store = new VsaYoloClassMapFileStore(mapPath);

        var error = Assert.Throws<InvalidDataException>(() => store.GetFullMap());

        Assert.Contains(mapPath, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllBytes(mapPath));
        Assert.False(File.Exists(Path.Combine(_root, "classes.txt")));
    }

    [Theory]
    [InlineData("{\"A\":0,\"B\":0}", "mehrfach")]
    [InlineData("{\"A\":-1}", "negative")]
    [InlineData("{\"A\":0,\"B\":2}", "lueckenlos")]
    [InlineData("{\"BAB\":0,\"bab\":1}", "mehrfach")]
    public void Ungueltige_Legacy_IDs_werden_abgelehnt(string json, string expectedMessage)
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        File.WriteAllText(mapPath, json);
        var store = new VsaYoloClassMapFileStore(mapPath);

        var error = Assert.Throws<InvalidDataException>(() => store.GetFullMap());

        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Versionierte_Karte_loest_exakten_Key_und_VSA_Praefix_auf()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        WriteVersionedMap(mapPath, new Dictionary<string, int>
        {
            ["BAB_riss"] = 0,
            ["SONST_schaden"] = 1
        });
        IVsaYoloClassMapStore store = new VsaYoloClassMapFileStore(mapPath);

        Assert.Equal(0, store.GetClassId("BAB_riss"));
        Assert.Equal(0, store.GetClassId("BABBA"));
        Assert.Equal(1, store.GetClassId("SONST_schaden"));
    }

    [Fact]
    public void Exakter_v2_Key_hat_Vorrang_vor_VSA_Praefixnormalisierung()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        WriteVersionedMap(mapPath, new Dictionary<string, int>
        {
            ["BAB"] = 0,
            ["BAB_riss"] = 1
        });
        IVsaYoloClassMapStore store = new VsaYoloClassMapFileStore(mapPath);

        Assert.Equal(1, store.GetClassId("BAB_riss"));
    }

    [Fact]
    public void GetOrAdd_erhaelt_v2_Version_und_Manifesthash()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        WriteVersionedMap(mapPath, new Dictionary<string, int>
        {
            ["BAB_riss"] = 0,
            ["SONST_schaden"] = 1
        });
        IVsaYoloClassMapStore store = new VsaYoloClassMapFileStore(mapPath);

        Assert.Equal(2, store.GetOrAddClassId("BZZ_spezial"));

        using var stored = JsonDocument.Parse(File.ReadAllText(mapPath));
        Assert.Equal(2, stored.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(ManifestHash, stored.RootElement.GetProperty("vsa_manifest_hash").GetString());
        var classes = stored.RootElement.GetProperty("classes");
        Assert.Equal(2, classes.GetProperty("BZZ_spezial").GetInt32());
        Assert.Equal(
            new[] { "BAB_riss", "SONST_schaden", "BZZ_spezial" },
            File.ReadAllLines(Path.Combine(_root, "classes.txt")));
    }

    [Theory]
    [InlineData("{\"version\":3,\"vsa_manifest_hash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"classes\":{\"BAB_riss\":0}}", "Version")]
    [InlineData("{\"version\":2,\"vsa_manifest_hash\":\"zu-kurz\",\"classes\":{\"BAB_riss\":0}}", "SHA-256")]
    [InlineData("{\"version\":2,\"classes\":{\"BAB_riss\":0}}", "vsa_manifest_hash")]
    [InlineData("{\"version\":2,\"vsa_manifest_hash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"classes\":{\"BAB_riss\":0},\"extra\":true}", "Unbekannte")]
    public void Ungueltige_v2_Metadaten_werden_abgelehnt(string json, string expectedMessage)
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        File.WriteAllText(mapPath, json);
        var store = new VsaYoloClassMapFileStore(mapPath);

        var error = Assert.Throws<InvalidDataException>(() => store.GetFullMap());

        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Schreibfehler_wird_gemeldet_und_alte_Karte_bleibt_unveraendert()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        File.WriteAllText(mapPath, "{\"BAA\":0}");
        var before = File.ReadAllBytes(mapPath);
        Directory.CreateDirectory(Path.Combine(_root, "classes.txt"));
        var store = new VsaYoloClassMapFileStore(mapPath);

        Assert.ThrowsAny<IOException>(() => store.GetOrAddClassId("BZZ"));

        Assert.Equal(before, File.ReadAllBytes(mapPath));
        Assert.Throws<KeyNotFoundException>(() => store.GetClassId("BZZ"));
    }

    [Fact]
    public void Kartenfehler_setzt_zuvor_geschriebene_classes_Datei_zurueck()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        Directory.CreateDirectory(mapPath);
        var classesPath = Path.Combine(_root, "classes.txt");
        File.WriteAllText(classesPath, "ALTE_KLASSE" + Environment.NewLine);
        var before = File.ReadAllBytes(classesPath);
        var store = new VsaYoloClassMapFileStore(mapPath);

        Assert.ThrowsAny<IOException>(() => store.GetOrAddClassId("BZZ"));

        Assert.Equal(before, File.ReadAllBytes(classesPath));
        Assert.True(Directory.Exists(mapPath));
        Assert.Throws<KeyNotFoundException>(() => store.GetClassId("BZZ"));
    }

    [Fact]
    public void Kartenfehler_entfernt_neu_angelegte_classes_Datei()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        Directory.CreateDirectory(mapPath);
        var classesPath = Path.Combine(_root, "classes.txt");
        var store = new VsaYoloClassMapFileStore(mapPath);

        Assert.ThrowsAny<IOException>(() => store.GetOrAddClassId("BZZ"));

        Assert.False(File.Exists(classesPath));
        Assert.True(Directory.Exists(mapPath));
    }

    [Fact]
    public void Unbekannte_strikte_Abfrage_aendert_vorhandene_Datei_nicht()
    {
        Directory.CreateDirectory(_root);
        var mapPath = MapPath();
        File.WriteAllText(mapPath, "{\"BAA\":0}");
        var before = File.ReadAllBytes(mapPath);
        var store = new VsaYoloClassMapFileStore(mapPath);

        Assert.Throws<KeyNotFoundException>(() => store.GetClassId("BZZ"));

        Assert.Equal(before, File.ReadAllBytes(mapPath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }

    private string MapPath()
        => Path.Combine(_root, "yolo_class_map.json");

    private static void WriteVersionedMap(
        string path,
        IReadOnlyDictionary<string, int> classes)
    {
        var json = JsonSerializer.Serialize(new
        {
            version = 2,
            vsa_manifest_hash = ManifestHash,
            classes
        });
        File.WriteAllText(path, json);
    }
}
