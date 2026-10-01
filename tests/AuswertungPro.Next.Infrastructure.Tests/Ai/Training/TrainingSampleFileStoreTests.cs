using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

/// <summary>
/// AP-1 (Audit 2026-08-10): Ein Lesefehler an der Golddatei darf nie zum
/// Loeschen des Bestands fuehren. Unlesbar ist nicht dasselbe wie nicht
/// vorhanden — der Erstlauf bleibt eine leere Liste, der unlesbare Bestand
/// ist ein Fehler, und es wird nichts gespeichert.
/// </summary>
public sealed class TrainingSampleFileStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsfs-" + Guid.NewGuid().ToString("N"));
    private readonly List<FileStream> _sperren = new();

    public void Dispose()
    {
        foreach (var sperre in _sperren)
            sperre.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Ein_unlesbarer_Bestand_darf_nie_ueberschrieben_werden()
    {
        // Hauptdatei und alle Sicherungskopien sind KURZ gesperrt — genau das
        // tut eine voruebergehende Sperre durch Spiegeldienst, Virenscanner oder
        // eine zweite Instanz. Nach 300 ms loest sie sich wieder: Der Lesevorgang
        // scheitert, der Schreibvorgang waere danach moeglich. Genau dann darf
        // nicht geschrieben werden.
        var pfad = GolddateiMitEintraegen(5);
        var vorher = File.ReadAllBytes(pfad);
        var bakVorher = File.ReadAllBytes(pfad + ".bak");
        Sperre(pfad);
        Sperre(pfad + ".bak");
        Sperre(pfad + ".bak.2");
        Sperre(pfad + ".bak.3");
        var freigabe = Task.Run(async () =>
        {
            await Task.Delay(300);
            foreach (var sperre in _sperren)
                sperre.Dispose();
            _sperren.Clear();
        });
        var store = new TrainingSampleFileStore(pfad);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.MergeOrUpdateAsync([NeuesSample("neu-1")]));
        await freigabe;

        // Der wichtigere Teil: Der Bestand ist byte-gleich geblieben.
        Assert.Equal(vorher, File.ReadAllBytes(pfad));
        Assert.Equal(bakVorher, File.ReadAllBytes(pfad + ".bak"));
    }

    [Fact]
    public async Task Ein_fehlender_Bestand_ist_ein_Erstlauf_kein_Fehler()
    {
        var pfad = Path.Combine(_root, "training_samples.json");
        var store = new TrainingSampleFileStore(pfad);

        await store.MergeOrUpdateAsync([NeuesSample("neu-2")]);

        var geladen = await store.LoadAsync();
        Assert.Single(geladen);
        Assert.Equal("neu-2", geladen[0].SampleId);
    }

    [Fact]
    public async Task Defekte_Hauptdatei_und_lesbares_Backup_stellt_wieder_her()
    {
        // Korrupte Hauptdatei, lesbare .bak: die Wiederherstellung bleibt
        // unveraendert — sie ist nicht der Fehlerpfad, sondern der Schutz.
        var pfad = GolddateiMitEintraegen(2);
        File.WriteAllText(pfad, "{ defekt", Encoding.UTF8);
        var store = new TrainingSampleFileStore(pfad);

        await store.MergeOrUpdateAsync([NeuesSample("neu-3")]);

        var geladen = await store.LoadAsync();
        Assert.Equal(3, geladen.Count);
        Assert.Contains(geladen, sample => sample.SampleId == "neu-3");
    }

    private string GolddateiMitEintraegen(int anzahl)
    {
        Directory.CreateDirectory(_root);
        var pfad = Path.Combine(_root, "training_samples.json");
        var eintraege = Enumerable.Range(1, anzahl)
            .Select(index => NeuesSample($"alt-{index}"))
            .ToList();
        var json = JsonSerializer.Serialize(eintraege);
        File.WriteAllText(pfad, json, Encoding.UTF8);
        File.WriteAllText(pfad + ".bak", json, Encoding.UTF8);
        File.WriteAllText(pfad + ".bak.2", json, Encoding.UTF8);
        File.WriteAllText(pfad + ".bak.3", json, Encoding.UTF8);
        return pfad;
    }

    private void Sperre(string pfad)
        => _sperren.Add(new FileStream(pfad, FileMode.Open, FileAccess.ReadWrite, FileShare.None));

    private static TrainingSample NeuesSample(string id) => new()
    {
        SampleId = id,
        CaseId = "36053-36052",
        Code = "BCC",
        Beschreibung = "Bogen - Testbestand",
        Signature = $"36053-36052|BCC|{id}|sig"
    };
}
