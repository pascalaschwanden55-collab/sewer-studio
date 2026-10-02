using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

/// <summary>
/// Deepscan 02.10.2026 (A1/R2), Entscheid E1 «sperren»: Der Eval-Schutz hatte je nach
/// Leseweg drei Bedeutungen von «Ordner fehlt». Der Gold-Speicher liess bei fehlendem,
/// leerem oder unlesbarem Pruefdaten-Ordner alle Samples ungefiltert durch, und ein
/// unlesbarer Unterordner liess weitere Pruefsaetze still wegfallen. Jetzt gilt EINE Regel
/// (<see cref="EvalProtectionSetReader"/>): Nur ein bewusst leerer Eintrag schaltet den
/// Schutz ab; fehlt, unlesbar, Verknuepfung, defekt oder leer ist an jedem Weg ein Fehler,
/// und der Speicher schreibt dann nichts.
/// </summary>
[Collection("EnvironmentVars")]
public sealed class EvalSchutzLesewegeTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "eval-schutz-lesewege-" + Guid.NewGuid().ToString("N"));

    private const string GueltigerHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    public static TheoryData<string> DefekteOrdner() => new()
    {
        "fehlt",
        "leerer_ordner",
        "kaputte_kandidatenliste",
        "leere_kandidatenliste",
        "nur_bildhashes",
        "leere_kandidatenliste_im_unterset",
        "kandidat_ohne_haltung"
    };

    [Theory]
    [MemberData(nameof(DefekteOrdner))]
    public void Gemeinsamer_Leser_meldet_jeden_defekten_Ordner_als_Fehler(string fall)
    {
        var evalRoot = Baue(fall);

        var fehler = Record.Exception(() => EvalProtectionSetReader.LoadStrict(evalRoot));

        Assert.True(
            fehler is IOException or InvalidDataException,
            $"{fall}: erwartet IOException/InvalidDataException, erhalten {fehler?.GetType().Name ?? "keine Ausnahme"}");
    }

    [Theory]
    [MemberData(nameof(DefekteOrdner))]
    public void Wissenssuche_bekommt_bei_defektem_Ordner_keine_Sperrliste(string fall)
    {
        var evalRoot = Baue(fall);

        var fehler = Record.Exception(() => GuardedRetrievalFactory.Sperrliste(evalRoot));

        Assert.True(fehler is IOException or InvalidDataException, $"{fall}: {fehler?.GetType().Name ?? "keine Ausnahme"}");
    }

    [Theory]
    [MemberData(nameof(DefekteOrdner))]
    public async Task Speicher_sperrt_das_Speichern_bei_defektem_Ordner(string fall)
    {
        var evalRoot = Baue(fall);
        var store = Speicher(evalRoot);

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.MergeAndSaveAsync([Sample("neu", "300-400")]));

        Assert.Contains(Path.GetFullPath(evalRoot), fehler.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Prüfdaten-Ordner", fehler.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(store.StoragePath), "Bei gesperrtem Eval-Schutz darf nichts geschrieben werden.");
    }

    [Theory]
    [MemberData(nameof(DefekteOrdner))]
    public async Task Speicher_sperrt_SaveAsync_und_TryAddNewAsync_bei_defektem_Ordner(string fall)
    {
        var evalRoot = Baue(fall);
        var store = Speicher(evalRoot);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync([Sample("neu", "300-400")]));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.TryAddNewAsync(Sample("neu-2", "301-401")));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.MergeOrUpdateAsync([Sample("neu-3", "302-402")]));
        Assert.False(File.Exists(store.StoragePath));
    }

    [Fact]
    public async Task Speicher_liefert_Bestand_bei_fehlendem_Ordner_nicht_ungefiltert_aus()
    {
        var gueltig = EvalSchutzTestOrdner.Anlegen(Path.Combine(_root, "eval_gueltig"));
        var store = Speicher(gueltig);
        await store.SaveAsync([Sample("bestand", "300-400")]);

        store.ConfigureEvalProtection(Path.Combine(_root, "eval_fehlt"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task Ersetzen_sperrt_bei_fehlendem_Ordner_und_laesst_den_Bestand_unveraendert()
    {
        var gueltig = EvalSchutzTestOrdner.Anlegen(Path.Combine(_root, "eval_gueltig"));
        var store = Speicher(gueltig);
        await store.SaveAsync([Sample("bestand", "300-400")]);
        var vorher = File.ReadAllBytes(store.StoragePath);

        store.ConfigureEvalProtection(Path.Combine(_root, "eval_fehlt"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReplaceBySampleIdAsync(Sample("bestand", "300-400", code: "BAC")));
        Assert.Equal(vorher, File.ReadAllBytes(store.StoragePath));
    }

    // Review PR #69: Laden migrierte alte Signaturen und rettete eine defekte Datei aus der
    // Sicherung, BEVOR der Eval-Schutz geprueft wurde. Bei gesperrtem Schutz darf der Speicher
    // gar nichts schreiben - auch keine Migration und keine Rettungskopie.
    [Theory]
    [InlineData("laden")]
    [InlineData("ersetzen")]
    public async Task Gesperrter_Schutz_schreibt_auch_keine_Signatur_Migration(string weg)
    {
        var store = Speicher(Path.Combine(_root, "eval_fehlt"));
        Directory.CreateDirectory(Path.GetDirectoryName(store.StoragePath)!);
        var altbestand = new TrainingSample
        {
            SampleId = "alt",
            CaseId = "300-400",
            Code = "BAB",
            Beschreibung = "Altbestand",
            Signature = "BAB|1.0|1.0"   // dreiteilig: wird beim Laden migriert
        };
        File.WriteAllText(store.StoragePath, System.Text.Json.JsonSerializer.Serialize(new[] { altbestand }));
        var vorher = File.ReadAllBytes(store.StoragePath);

        await Assert.ThrowsAsync<InvalidOperationException>(() => weg == "laden"
            ? store.LoadAsync()
            : store.ReplaceBySampleIdAsync(Sample("alt", "300-400")));

        Assert.Equal(vorher, File.ReadAllBytes(store.StoragePath));
        Assert.Equal(
            [Path.GetFileName(store.StoragePath)],
            Directory.EnumerateFiles(Path.GetDirectoryName(store.StoragePath)!).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("laden")]
    [InlineData("ersetzen")]
    public async Task Gesperrter_Schutz_schreibt_auch_keine_Rettungskopie(string weg)
    {
        var store = Speicher(Path.Combine(_root, "eval_fehlt"));
        var ordner = Path.GetDirectoryName(store.StoragePath)!;
        Directory.CreateDirectory(ordner);
        File.WriteAllText(store.StoragePath, "{ defekt");
        File.WriteAllText(
            store.StoragePath + ".bak",
            System.Text.Json.JsonSerializer.Serialize(new[] { Sample("alt", "300-400") }));
        var vorher = Directory.EnumerateFiles(ordner)
            .ToDictionary(pfad => Path.GetFileName(pfad), File.ReadAllBytes);

        await Assert.ThrowsAsync<InvalidOperationException>(() => weg == "laden"
            ? store.LoadAsync()
            : store.ReplaceBySampleIdAsync(Sample("alt", "300-400")));

        var nachher = Directory.EnumerateFiles(ordner)
            .ToDictionary(pfad => Path.GetFileName(pfad), File.ReadAllBytes);
        Assert.Equal(vorher.Keys.Order(), nachher.Keys.Order());
        foreach (var (name, inhalt) in vorher)
            Assert.Equal(inhalt, nachher[name]);
    }

    [Fact]
    public async Task Ersetzen_lehnt_ein_Sample_aus_einer_Pruefhaltung_ab()
    {
        var evalRoot = EvalSchutzTestOrdner.Anlegen(Path.Combine(_root, "eval"), "100-200");
        var store = Speicher(evalRoot);
        await store.SaveAsync([Sample("bestand", "300-400")]);
        var vorher = File.ReadAllBytes(store.StoragePath);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReplaceBySampleIdAsync(Sample("bestand", "100-200")));
        Assert.Equal(vorher, File.ReadAllBytes(store.StoragePath));
    }

    [Fact]
    public async Task Ohne_Konfiguration_sperrt_ein_fehlender_Standardordner()
    {
        var vorher = Environment.GetEnvironmentVariable("SEWERSTUDIO_EVAL_SET_ROOT");
        var fehlt = Path.Combine(_root, "eval_aus_umgebung_fehlt");
        Environment.SetEnvironmentVariable("SEWERSTUDIO_EVAL_SET_ROOT", fehlt);
        try
        {
            var store = new TrainingSampleFileStore(Path.Combine(_root, "knowledge", "training_samples.json"));

            var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.MergeAndSaveAsync([Sample("neu", "300-400")]));

            Assert.Contains(fehlt, fehler.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(store.StoragePath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_EVAL_SET_ROOT", vorher);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Bewusst_leerer_Eintrag_schaltet_den_Schutz_an_allen_Wegen_ab(string eintrag)
    {
        Assert.True(EvalProtectionSetReader.LoadStrict(eintrag).IsDisabled);
        Assert.Empty(GuardedRetrievalFactory.Sperrliste(eintrag));

        var store = Speicher(eintrag);
        await store.MergeAndSaveAsync([Sample("frei", "300-400")]);

        Assert.Equal(string.Empty, store.EffectiveEvalSetRoot);
        Assert.Equal(["frei"], (await store.LoadAsync()).Select(sample => sample.SampleId));
    }

    [Fact]
    public async Task Gueltiger_Ordner_mit_Unterset_schuetzt_beide_Saetze()
    {
        var evalRoot = EvalSchutzTestOrdner.Anlegen(Path.Combine(_root, "eval"), "100-200");
        var v2 = Path.Combine(evalRoot, "v2");
        Directory.CreateDirectory(v2);
        File.WriteAllText(Path.Combine(v2, "_manifest.json"), ManifestMitHash());
        File.WriteAllText(Path.Combine(v2, "_candidates.json"), """[{"haltung_key":"07.500-600"}]""");

        var sets = EvalProtectionSetReader.LoadStrict(evalRoot);

        Assert.False(sets.IsDisabled);
        Assert.Contains("100-200", sets.HaltungKeys);
        Assert.Contains("500-600", sets.HaltungKeys);
        Assert.Contains(GueltigerHash, sets.ImageHashes);

        var store = Speicher(evalRoot);
        await store.MergeAndSaveAsync([
            Sample("pruef-v1", "100-200"),
            Sample("pruef-v2", "600-500"),
            Sample("frei", "300-400")
        ]);
        Assert.Equal(["frei"], (await store.LoadAsync()).Select(sample => sample.SampleId));
    }

    [Fact]
    public async Task Unlesbarer_Unterordner_sperrt_alle_Lesewege()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var evalRoot = EvalSchutzTestOrdner.Anlegen(Path.Combine(_root, "eval"), "100-200");
        var gesperrt = Path.Combine(evalRoot, "a_gesperrt");
        Directory.CreateDirectory(gesperrt);
        var sperreGesetzt = AclSperre.Setze(gesperrt);
        try
        {
            // Umgebungen, welche die Deny-Regel nicht durchsetzen, pruefen hier nichts.
            if (!sperreGesetzt || !AclSperre.Wirkt(gesperrt))
                return;

            Assert.Throws<IOException>(() => EvalProtectionSetReader.LoadStrict(evalRoot));
            Assert.Throws<IOException>(() => GuardedRetrievalFactory.Sperrliste(evalRoot));
            var store = Speicher(evalRoot);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.MergeAndSaveAsync([Sample("neu", "300-400")]));
            Assert.False(File.Exists(store.StoragePath));
        }
        finally
        {
            AclSperre.Entferne(gesperrt);
        }
    }

    [Fact]
    public void Milder_Leser_verliert_kein_Unterset_hinter_einem_unlesbaren_Ordner()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var evalRoot = Path.Combine(_root, "eval");
        var gesperrt = Path.Combine(evalRoot, "a_gesperrt");
        var v2 = Path.Combine(evalRoot, "v2");
        Directory.CreateDirectory(gesperrt);
        Directory.CreateDirectory(v2);
        File.WriteAllText(Path.Combine(v2, "_manifest.json"), "{}");
        File.WriteAllText(Path.Combine(v2, "_candidates.json"), """[{"haltung_key":"100-200"}]""");
        var sperreGesetzt = AclSperre.Setze(gesperrt);
        try
        {
            if (!sperreGesetzt || !AclSperre.Wirkt(gesperrt))
                return;

            Assert.Contains("100-200", EvalContaminationGuard.LoadEvalHaltungKeys(evalRoot));
        }
        finally
        {
            AclSperre.Entferne(gesperrt);
        }
    }

    [JunctionFact]
    public async Task Verknuepfter_Unterordner_sperrt_alle_Lesewege()
    {
        var evalRoot = EvalSchutzTestOrdner.Anlegen(Path.Combine(_root, "eval"), "100-200");
        var fremd = EvalSchutzTestOrdner.Anlegen(Path.Combine(_root, "fremd"), "700-800");
        JunctionTestSupport.CreateDirectoryLink(Path.Combine(evalRoot, "verknuepft"), fremd);

        Assert.Throws<IOException>(() => EvalProtectionSetReader.LoadStrict(evalRoot));
        Assert.Throws<IOException>(() => GuardedRetrievalFactory.Sperrliste(evalRoot));
        var store = Speicher(evalRoot);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.MergeAndSaveAsync([Sample("neu", "300-400")]));
        Assert.False(File.Exists(store.StoragePath));
    }

    private string Baue(string fall)
    {
        var evalRoot = Path.Combine(_root, "eval_" + fall);
        switch (fall)
        {
            case "fehlt":
                break;
            case "leerer_ordner":
                Directory.CreateDirectory(evalRoot);
                break;
            case "kaputte_kandidatenliste":
                Directory.CreateDirectory(evalRoot);
                File.WriteAllText(Path.Combine(evalRoot, "_candidates.json"), "{ kaputt");
                break;
            case "leere_kandidatenliste":
                Directory.CreateDirectory(evalRoot);
                File.WriteAllText(Path.Combine(evalRoot, "_manifest.json"), ManifestMitHash());
                File.WriteAllText(Path.Combine(evalRoot, "_candidates.json"), "[]");
                break;
            case "nur_bildhashes":
                Directory.CreateDirectory(evalRoot);
                File.WriteAllText(Path.Combine(evalRoot, "_manifest.json"), ManifestMitHash());
                break;
            case "leere_kandidatenliste_im_unterset":
                EvalSchutzTestOrdner.Anlegen(evalRoot, "100-200");
                Directory.CreateDirectory(Path.Combine(evalRoot, "v2"));
                File.WriteAllText(Path.Combine(evalRoot, "v2", "_manifest.json"), "{}");
                File.WriteAllText(Path.Combine(evalRoot, "v2", "_candidates.json"), """{"candidates":[]}""");
                break;
            case "kandidat_ohne_haltung":
                Directory.CreateDirectory(evalRoot);
                File.WriteAllText(
                    Path.Combine(evalRoot, "_candidates.json"),
                    """[{"haltung_key":"100-200"},{"frame_path":"x.png"}]""");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fall), fall, null);
        }

        return evalRoot;
    }

    private static string ManifestMitHash() => $$"""
        { "hashes": { "images/frame.png": { "sha256": "{{GueltigerHash}}" } } }
        """;

    private TrainingSampleFileStore Speicher(string evalRoot)
    {
        var store = new TrainingSampleFileStore(
            Path.Combine(_root, "knowledge-" + Guid.NewGuid().ToString("N"), "training_samples.json"));
        store.ConfigureEvalProtection(evalRoot);
        return store;
    }

    private static TrainingSample Sample(string id, string caseId, string code = "BAB") => new()
    {
        SampleId = id,
        CaseId = caseId,
        Code = code,
        Beschreibung = "Gepruefter Schaden",
        FramePath = Path.Combine(Path.GetTempPath(), "gibt-es-nicht-" + id + ".png"),
        Signature = id
    };

    [SupportedOSPlatform("windows")]
    private static class AclSperre
    {
        private static readonly FileSystemRights Rechte =
            FileSystemRights.ListDirectory | FileSystemRights.ReadData | FileSystemRights.Traverse;

        private static SecurityIdentifier Benutzer() => WindowsIdentity.GetCurrent().User!;

        public static bool Setze(string ordner)
        {
            try
            {
                var info = new DirectoryInfo(ordner);
                var sicherheit = info.GetAccessControl();
                sicherheit.AddAccessRule(new FileSystemAccessRule(Benutzer(), Rechte, AccessControlType.Deny));
                info.SetAccessControl(sicherheit);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException)
            {
                return false;
            }
        }

        public static void Entferne(string ordner)
        {
            try
            {
                var info = new DirectoryInfo(ordner);
                var sicherheit = info.GetAccessControl();
                sicherheit.RemoveAccessRuleAll(new FileSystemAccessRule(Benutzer(), Rechte, AccessControlType.Deny));
                info.SetAccessControl(sicherheit);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException)
            {
                // Nur Testaufraeumen; Dispose loescht den Ordner danach.
            }
        }

        public static bool Wirkt(string ordner)
        {
            try
            {
                _ = Directory.EnumerateFileSystemEntries(ordner).ToList();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
    }
}
