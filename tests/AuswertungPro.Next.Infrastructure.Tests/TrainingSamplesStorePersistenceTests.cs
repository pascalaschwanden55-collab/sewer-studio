using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class TrainingSamplesStorePersistenceTests
{
    [Fact]
    public async Task MergePaths_PreserveDedupUpdateAndAtomicBackup()
    {
        await WithTempStore(async path =>
        {
            await TrainingSamplesStore.SaveAsync([
                Sample("original", "sig-1", notes: "alt"),
                Sample("ohne-signatur-1", "")
            ]);

            await TrainingSamplesStore.MergeAndSaveAsync([
                Sample("duplikat", "sig-1"),
                Sample("neu", "sig-2"),
                Sample("ohne-signatur-2", "")
            ]);

            await TrainingSamplesStore.MergeOrUpdateAsync([
                Sample("ersatz", "sig-1", TrainingSampleStatus.Approved, "aktualisiert"),
                Sample("neu-2", "sig-3")
            ]);

            var samples = await TrainingSamplesStore.LoadAsync();

            Assert.Equal(5, samples.Count);
            var updated = Assert.Single(samples, sample => sample.Signature == "sig-1");
            Assert.Equal("original", updated.SampleId);
            Assert.Equal(TrainingSampleStatus.Approved, updated.Status);
            Assert.Equal("aktualisiert", updated.Notes);
            Assert.True(File.Exists(path + ".bak"));
            Assert.False(File.Exists(path + ".tmp"));
        });
    }

    [Fact]
    public async Task Load_PreservesLegacySignatureAndFallsBackToBackupForCorruptPrimary()
    {
        await WithTempStore(async path =>
        {
            var legacy = Sample("legacy", "BAB|1.0|2.0");
            legacy.CaseId = "H-001";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { legacy }));

            var loaded = await TrainingSamplesStore.LoadAsync();

            var loadedSample = Assert.Single(loaded);
            Assert.Equal("H-001", loadedSample.CaseId);
            Assert.Equal("BAB|1.0|2.0", loadedSample.Signature);
            await TrainingSamplesStore.SaveAsync(loaded);
            Assert.True(File.Exists(path + ".bak"));

            await File.WriteAllTextAsync(path, "{ keine gueltige JSON-Datei");
            var recovered = await TrainingSamplesStore.LoadAsync();

            Assert.Single(recovered);
            Assert.Contains(
                Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "training_samples.json.bad_*"),
                File.Exists);
        });
    }

    [Fact]
    public async Task FileStore_ParallelMergesDoNotLoseSamples()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-training-sample-file-store-tests",
            Guid.NewGuid().ToString("N"));
        var store = new TrainingSampleFileStore(Path.Combine(root, "training_samples.json"));
        store.ConfigureEvalProtection(Path.Combine(root, "empty-eval"));

        try
        {
            await Task.WhenAll(Enumerable.Range(0, 16).Select(index =>
                store.MergeAndSaveAsync([Sample($"sample-{index}", $"sig-{index}")])));

            var samples = await store.LoadAsync();

            Assert.Equal(16, samples.Count);
            Assert.Equal(16, samples.Select(sample => sample.Signature).Distinct().Count());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ZweiInstanzen_gleicheDatei_parallelesMerge_verliertKeineSamples()
    {
        // Bildet den echten Aufbau nach: die ServiceProvider-Instanz und die statische Fassade
        // sind zwei getrennte Store-Instanzen auf DERSELBEN training_samples.json. Ohne geteiltes
        // Lock koennten sich parallele Merges (Self-Training vs. Pruefplatz) gegenseitig ueberschreiben.
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-training-sample-file-store-two-instances",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "training_samples.json");
        var storeA = new TrainingSampleFileStore(path);
        var storeB = new TrainingSampleFileStore(path);
        storeA.ConfigureEvalProtection(Path.Combine(root, "empty-eval"));
        storeB.ConfigureEvalProtection(Path.Combine(root, "empty-eval"));

        try
        {
            var mergesA = Enumerable.Range(0, 30)
                .Select(i => storeA.MergeAndSaveAsync([Sample($"a-{i}", $"sig-a-{i}")]));
            var mergesB = Enumerable.Range(0, 30)
                .Select(i => storeB.MergeAndSaveAsync([Sample($"b-{i}", $"sig-b-{i}")]));
            await Task.WhenAll(mergesA.Concat(mergesB));

            var samples = await storeA.LoadAsync();

            Assert.Equal(60, samples.Count);
            Assert.Equal(60, samples.Select(sample => sample.Signature).Distinct().Count());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task MergeOrUpdate_gleicheSampleId_neueSignatur_aktualisiert_denselben_Eintrag()
    {
        // Codekorrektur-Fall: gleiche SampleId, aber neue Signatur (enthaelt den neuen Code).
        // Der Id-Match muss denselben Datensatz aktualisieren — kein Dublett.
        await WithTempStore(async _ =>
        {
            await TrainingSamplesStore.SaveAsync([
                Sample("wb_1", "H-TEST|BAB|1.0|1.0", notes: "alt")
            ]);

            await TrainingSamplesStore.MergeOrUpdateAsync([
                Sample("wb_1", "H-TEST|BBA|1.0|1.0", TrainingSampleStatus.Approved, "korrigiert")
            ]);

            var samples = await TrainingSamplesStore.LoadAsync();
            var updated = Assert.Single(samples);
            Assert.Equal("wb_1", updated.SampleId);
            Assert.Equal(TrainingSampleStatus.Approved, updated.Status);
            Assert.Equal("korrigiert", updated.Notes);
        });
    }

    [Fact]
    public async Task MergeOrUpdate_gleicheSignatur_verschiedeneIds_nutzt_Signatur_Fallback()
    {
        // Dokumentation des bisherigen Verhaltens: ohne Id-Treffer matcht die Signatur
        // (Alt-Aufrufer ohne stabile Id-Zuordnung). Der bestehende Eintrag wird
        // aktualisiert — die neue Id erzeugt KEINEN zweiten Datensatz.
        await WithTempStore(async _ =>
        {
            await TrainingSamplesStore.SaveAsync([Sample("alt-id", "sig-x", notes: "alt")]);

            await TrainingSamplesStore.MergeOrUpdateAsync([
                Sample("neu-id", "sig-x", TrainingSampleStatus.Approved, "neu")
            ]);

            var samples = await TrainingSamplesStore.LoadAsync();
            var updated = Assert.Single(samples);
            Assert.Equal("alt-id", updated.SampleId);   // Id des bestehenden Eintrags bleibt
            Assert.Equal("neu", updated.Notes);
        });
    }

    [Fact]
    public async Task MergeOrUpdate_unbekannteIdUndSignatur_haengt_an()
    {
        await WithTempStore(async _ =>
        {
            await TrainingSamplesStore.SaveAsync([Sample("a", "sig-a")]);

            await TrainingSamplesStore.MergeOrUpdateAsync([Sample("b", "sig-b")]);

            Assert.Equal(2, (await TrainingSamplesStore.LoadAsync()).Count);
        });
    }

    [Fact]
    public async Task ReplaceBySampleId_ersetzt_unter_einer_Sperre_genau_einen_Eintrag()
    {
        await WithTempStore(async _ =>
        {
            await TrainingSamplesStore.SaveAsync([
                Sample("wb_1", "H-TEST|BAB|1.0|1.0", notes: "alt"),
                Sample("wb_2", "H-TEST|BAB|2.0|2.0", notes: "unberuehrt")
            ]);

            var ersatz = Sample("wb_1", "H-TEST|BBA|1.0|1.0", TrainingSampleStatus.Approved, "korrigiert");
            ersatz.Code = "BBA";

            var replaced = await TrainingSamplesStore.Current.ReplaceBySampleIdAsync(ersatz);

            Assert.True(replaced);
            var samples = await TrainingSamplesStore.LoadAsync();
            Assert.Equal(2, samples.Count);                       // kein Eintrag verloren/verdoppelt
            var updated = Assert.Single(samples, sample => sample.SampleId == "wb_1");
            Assert.Equal("BBA", updated.Code);                    // neuer Code unter gleicher Id
            Assert.Equal("H-TEST|BBA|1.0|1.0", updated.Signature);
            Assert.Equal("unberuehrt", Assert.Single(samples, sample => sample.SampleId == "wb_2").Notes);
        });
    }

    [Fact]
    public async Task ReplaceBySampleId_unbekannteId_schreibt_nichts()
    {
        await WithTempStore(async path =>
        {
            await TrainingSamplesStore.SaveAsync([Sample("wb_1", "sig-1", notes: "alt")]);
            var before = await File.ReadAllTextAsync(path);

            var replaced = await TrainingSamplesStore.Current.ReplaceBySampleIdAsync(
                Sample("wb_fremd", "sig-fremd"));

            Assert.False(replaced);
            Assert.Equal(before, await File.ReadAllTextAsync(path));   // Datei unveraendert
        });
    }

    [Fact]
    public async Task ReplaceBySampleId_Signatur_gehoert_anderer_Id_bricht_laut_ab_und_schreibt_nichts()
    {
        await WithTempStore(async path =>
        {
            await TrainingSamplesStore.SaveAsync([
                Sample("wb_1", "H-TEST|BAB|1.0|1.0", notes: "zu ersetzen"),
                Sample("wb_2", "H-TEST|BBA|2.0|2.0", notes: "bestehende Wahrheit")
            ]);
            var before = await File.ReadAllTextAsync(path);
            var ersatz = Sample(
                "wb_1",
                "H-TEST|BBA|2.0|2.0",
                TrainingSampleStatus.Approved,
                "darf nicht geschrieben werden");
            ersatz.Code = "BBA";

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => TrainingSamplesStore.Current.ReplaceBySampleIdAsync(ersatz));

            Assert.Contains("Signatur", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("wb_2", error.Message, StringComparison.Ordinal);
            Assert.Equal(before, await File.ReadAllTextAsync(path));
        });
    }

    [Fact]
    public async Task TryAddNew_neues_Sample_wird_angehaengt_und_gemeldet()
    {
        await WithTempStore(async _ =>
        {
            var neu = Sample("wb_neu", "sig-neu");

            var added = await TrainingSamplesStore.Current.TryAddNewAsync(neu);

            Assert.True(added);
            Assert.Single(await TrainingSamplesStore.LoadAsync());
        });
    }

    [Fact]
    public async Task TryAddNew_gleiche_Signatur_meldet_false_und_laesst_Datei_unveraendert()
    {
        await WithTempStore(async path =>
        {
            await TrainingSamplesStore.SaveAsync([Sample("wb_1", "sig-1", notes: "alt")]);
            var before = await File.ReadAllTextAsync(path);

            var added = await TrainingSamplesStore.Current.TryAddNewAsync(
                Sample("wb_2", "sig-1", notes: "duplikat"));

            Assert.False(added);
            Assert.Equal(before, await File.ReadAllTextAsync(path));   // Inhalt unveraendert
            Assert.Single(await TrainingSamplesStore.LoadAsync());
        });
    }

    private static TrainingSample Sample(
        string id,
        string signature,
        TrainingSampleStatus status = TrainingSampleStatus.New,
        string notes = "") =>
        new()
        {
            SampleId = id,
            CaseId = "H-TEST",
            Code = "BAB",
            Beschreibung = "Gepruefter Schaden",
            Signature = signature,
            Status = status,
            Notes = notes
        };

    private static async Task WithTempStore(Func<string, Task> body)
    {
        var previousKnowledge = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        var previousEval = Environment.GetEnvironmentVariable("SEWERSTUDIO_EVAL_SET_ROOT");
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-training-samples-store-tests",
            Guid.NewGuid().ToString("N"));
        var knowledgeRoot = Path.Combine(root, "knowledge");
        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, knowledgeRoot);
        Environment.SetEnvironmentVariable("SEWERSTUDIO_EVAL_SET_ROOT", Path.Combine(root, "empty-eval"));
        KnowledgeBasePaths.InvalidateCache();
        TrainingSamplesStore.ConfigureEvalProtection(null);

        try
        {
            await body(KnowledgeBasePaths.GetTrainingSamplesPath());
        }
        finally
        {
            TrainingSamplesStore.ConfigureEvalProtection(null);
            Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, previousKnowledge);
            Environment.SetEnvironmentVariable("SEWERSTUDIO_EVAL_SET_ROOT", previousEval);
            KnowledgeBasePaths.InvalidateCache();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
