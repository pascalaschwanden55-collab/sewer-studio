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

    [Fact]
    public async Task TryAddNew_erkennt_legacy_Signatur_mit_gleicher_Box_als_Dublette()
    {
        await WithTempStore(async _ =>
        {
            var legacy = Sample(
                "wb_legacy",
                TrainingSample.BuildCanonicalSignature("H-TEST", "BAB", 1.0, 1.0));
            SetBox(legacy, 0.5, 0.5, 0.2, 0.2);
            await TrainingSamplesStore.SaveAsync([legacy]);

            var duplicate = Sample(
                "wb_neu",
                TrainingSample.BuildCanonicalSignature(
                    "H-TEST", "BAB", 1.0, 1.0, 0.5, 0.5, 0.2, 0.2));
            SetBox(duplicate, 0.5, 0.5, 0.2, 0.2);

            Assert.False(await TrainingSamplesStore.Current.TryAddNewAsync(duplicate));
            Assert.Single(await TrainingSamplesStore.LoadAsync());
        });
    }

    [Fact]
    public async Task TryAddNew_erlaubt_zweite_Box_neben_legacy_Sample()
    {
        await WithTempStore(async _ =>
        {
            var legacy = Sample(
                "wb_legacy",
                TrainingSample.BuildCanonicalSignature("H-TEST", "BAB", 1.0, 1.0));
            SetBox(legacy, 0.5, 0.5, 0.2, 0.2);
            await TrainingSamplesStore.SaveAsync([legacy]);

            var secondObject = Sample(
                "wb_neu",
                TrainingSample.BuildCanonicalSignature(
                    "H-TEST", "BAB", 1.0, 1.0, 0.2, 0.2, 0.1, 0.1));
            SetBox(secondObject, 0.2, 0.2, 0.1, 0.1);

            Assert.True(await TrainingSamplesStore.Current.TryAddNewAsync(secondObject));
            Assert.Equal(2, (await TrainingSamplesStore.LoadAsync()).Count);
        });
    }

    [Fact]
    public async Task Die_Herkunft_eines_Vorschlags_ueberlebt_das_Speichern_und_Laden()
    {
        // Ein Feld, das beim Schreiben verloren geht, taeuscht Nachvollziehbarkeit
        // vor: Die Herkunft laesst sich nachtraeglich nicht rekonstruieren.
        await WithTempStore(async _ =>
        {
            var beeinflusst = Sample("mit-vorschlag", "sig-1");
            beeinflusst.Code = "BAJC";
            beeinflusst.SuggestionProvenance = new TrainingSampleSuggestionProvenance
            {
                Origin = TrainingSampleSuggestionOrigin.SuggestionShown,
                ModelId = "bcc_nc15_seed44_20260808",
                ModelSha256 = new string('a', 64),
                SuggestedCode = "BCCYB",
                SuggestedConfidence = 0.57
            };

            var eigen = Sample("ohne-vorschlag", "sig-2");
            eigen.SuggestionProvenance = new TrainingSampleSuggestionProvenance
            {
                Origin = TrainingSampleSuggestionOrigin.Independent
            };

            await TrainingSamplesStore.SaveAsync([beeinflusst, eigen]);
            var geladen = await TrainingSamplesStore.LoadAsync();

            var wieder = Assert.Single(geladen, s => s.SampleId == "mit-vorschlag");
            Assert.Equal(
                TrainingSampleSuggestionOrigin.SuggestionShown,
                wieder.SuggestionProvenance?.Origin);
            Assert.Equal("bcc_nc15_seed44_20260808", wieder.SuggestionProvenance?.ModelId);
            Assert.Equal("BCCYB", wieder.SuggestionProvenance?.SuggestedCode);
            Assert.Equal(0.57, wieder.SuggestionProvenance?.SuggestedConfidence);
            Assert.False(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(wieder));
            // Abweichender Code gegenueber dem Vorschlag: das ist eine Korrektur.
            Assert.True(SuggestionProvenancePolicy.CarriesNewInformation(wieder));

            var selbst = Assert.Single(geladen, s => s.SampleId == "ohne-vorschlag");
            Assert.True(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(selbst));
        });
    }

    [Fact]
    public async Task Eine_echte_Altdatei_ohne_Herkunftsfeld_laedt_und_gilt_als_unbekannt()
    {
        // Der gesamte bestehende Goldbestand wurde ohne dieses Feld geschrieben.
        // Er muss weiter laden — und darf dabei nie stillschweigend als
        // unabhaengig codiert gelten, sonst waere jede Messung wertlos.
        await WithTempStore(async path =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                """
                [
                  {
                    "SampleId": "alt",
                    "CaseId": "H-ALT",
                    "Code": "BAB",
                    "Beschreibung": "Riss laengs",
                    "MeterStart": 1.0,
                    "MeterEnd": 1.0,
                    "Signature": "sig-alt",
                    "Status": 1
                  }
                ]
                """);

            var geladen = Assert.Single(await TrainingSamplesStore.LoadAsync());

            Assert.Equal("alt", geladen.SampleId);
            Assert.Null(geladen.SuggestionProvenance);
            Assert.Equal(
                TrainingSampleSuggestionOrigin.Unknown,
                SuggestionProvenancePolicy.ResolveOrigin(geladen));
            Assert.False(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(geladen));
            Assert.False(SuggestionProvenancePolicy.CarriesNewInformation(geladen));
        });
    }

    [Fact]
    public async Task Ersetzung_wiederholt_sich_wenn_ein_Leser_die_Zieldatei_kurz_haelt()
    {
        // Windows verweigert eine atomare Ersetzung, solange ein anderer Leser die
        // Zieldatei offen hat — unabhaengig vom Freigabemodus. Der Spiegeldienst
        // liest die 18 MB grosse Trainingsdatei nach jedem Speichern; genau dabei
        // ist am 2026-08-07 der Codiermodus mit "Access to the path is denied"
        // abgebrochen. Ein voruebergehend gesperrtes Ziel ist kein Datenfehler.
        var versuche = 0;
        var wartezeiten = new List<int>();

        await TrainingSampleFileStore.ReplaceAtomicallyAsync(
            "quelle.tmp",
            "ziel.json",
            move: (_, _) =>
            {
                versuche++;
                if (versuche < 3)
                    throw new UnauthorizedAccessException("Access to the path is denied.");
            },
            delay: milliseconds =>
            {
                wartezeiten.Add(milliseconds);
                return Task.CompletedTask;
            });

        Assert.Equal(3, versuche);
        Assert.Equal(2, wartezeiten.Count);
        Assert.All(wartezeiten, wartezeit => Assert.True(wartezeit > 0));
    }

    [Fact]
    public async Task Ersetzung_meldet_den_Fehler_wenn_die_Sperre_bleibt()
    {
        // Eine dauerhaft gesperrte Datei bleibt ein echter Fehler: Der Speichervorgang
        // darf niemals stillschweigend gelingen, ohne dass die Datei ersetzt wurde.
        var versuche = 0;

        var fehler = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => TrainingSampleFileStore.ReplaceAtomicallyAsync(
                "quelle.tmp",
                "ziel.json",
                move: (_, _) =>
                {
                    versuche++;
                    throw new UnauthorizedAccessException("Access to the path is denied.");
                },
                delay: _ => Task.CompletedTask));

        Assert.Contains("denied", fehler.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(versuche > 3, $"Es wurde nur {versuche} Mal versucht.");
    }

    [Fact]
    public async Task SaveAsync_gelingt_waehrend_ein_Leser_die_Datei_kurz_offen_haelt()
    {
        await WithTempStore(async path =>
        {
            await TrainingSamplesStore.SaveAsync([Sample("erst", "sig-1")]);

            // Der Leser haelt die Datei so, wie es der Spiegeldienst waehrend des
            // Hashens tut, und gibt sie danach wieder frei.
            using var leser = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var freigabe = Task.Run(async () =>
            {
                await Task.Delay(250);
                leser.Dispose();
            });

            await TrainingSamplesStore.SaveAsync([Sample("erst", "sig-1"), Sample("zweit", "sig-2")]);
            await freigabe;

            Assert.Equal(2, (await TrainingSamplesStore.LoadAsync()).Count);
        });
    }

    private static void SetBox(
        TrainingSample sample,
        double x,
        double y,
        double width,
        double height)
    {
        sample.MeterStart = 1.0;
        sample.MeterEnd = 1.0;
        sample.BboxXCenter = x;
        sample.BboxYCenter = y;
        sample.BboxWidth = width;
        sample.BboxHeight = height;
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
