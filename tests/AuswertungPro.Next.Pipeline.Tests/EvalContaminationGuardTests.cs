using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalContaminationGuardTests
{
    [Fact]
    public void IsEvalContaminated_TrueForByteIdenticalImage_FalseOtherwise()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-guard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var evalBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var evalImg = Path.Combine(root, "eval_frame.png");
            File.WriteAllBytes(evalImg, evalBytes);

            var evalHash = Convert.ToHexString(SHA256.HashData(evalBytes)).ToLowerInvariant();
            var evalHashes = new HashSet<string>(new[] { evalHash }, StringComparer.OrdinalIgnoreCase);

            // Inhaltsgleiches Bild unter ANDEREM Dateinamen -> kontaminiert (Inhalt zaehlt, nicht Name).
            var copyDifferentName = Path.Combine(root, "training_candidate.png");
            File.WriteAllBytes(copyDifferentName, evalBytes);
            Assert.True(EvalContaminationGuard.IsEvalContaminated(evalHashes, copyDifferentName));

            // Anderes Bild -> nicht kontaminiert.
            var other = Path.Combine(root, "other.png");
            File.WriteAllBytes(other, new byte[] { 99, 98, 97 });
            Assert.False(EvalContaminationGuard.IsEvalContaminated(evalHashes, other));

            // Leerer Hash-Satz -> kein Alarm.
            Assert.False(EvalContaminationGuard.IsEvalContaminated(new HashSet<string>(), copyDifferentName));

            // Fehlende Datei -> kein Alarm (statt Crash).
            Assert.False(EvalContaminationGuard.IsEvalContaminated(evalHashes, Path.Combine(root, "missing.png")));

            // Leerer Pfad -> kein Alarm.
            Assert.False(EvalContaminationGuard.IsEvalContaminated(evalHashes, ""));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ComputeFileHash_verwendet_kleingeschriebenes_SHA256_Hexformat()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-guard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var bytes = new byte[] { 10, 20, 30, 40 };
            var path = Path.Combine(root, "img.png");
            File.WriteAllBytes(path, bytes);

            var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Equal(expected, EvalContaminationGuard.ComputeFileHash(path));
            Assert.Null(EvalContaminationGuard.ComputeFileHash(null));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    // ── Haltungs-/CaseId-Sperrliste ────────────────────────────────────────

    [Theory]
    [InlineData("287425-81162", "287425-81162")]   // bereits kanonisch
    [InlineData("07.638910-1367", "638910-1367")]  // Praefix mit Punkt (eine Seite)
    [InlineData("06.24379-06.24377", "24379-24377")] // Bereichs-Praefix auf BEIDEN Seiten
    [InlineData("07.1026776-10.1064901", "1026776-1064901")] // unterschiedliche Bereichs-Praefixe
    [InlineData("634-581/20250625_634-581_Saniert", "634-581")] // verschachtelte CaseId
    [InlineData("  80945-81176  ", "80945-81176")] // getrimmt
    public void NormalizeHaltungKey_ExtractsManholePair(string input, string expected)
        => Assert.Equal(expected, EvalContaminationGuard.NormalizeHaltungKey(input));

    [Fact]
    public void NormalizeHaltungKey_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(EvalContaminationGuard.NormalizeHaltungKey(null));
        Assert.Null(EvalContaminationGuard.NormalizeHaltungKey("   "));
    }

    [Fact]
    public void IsEvalHaltung_BlocksMatchingHaltung_RegardlessOfPrefixSuffix()
    {
        var keys = new HashSet<string>(new[] { "287425-81162", "80945-81176" },
            StringComparer.OrdinalIgnoreCase);

        Assert.True(EvalContaminationGuard.IsEvalHaltung(keys, "287425-81162"));            // exakt
        Assert.True(EvalContaminationGuard.IsEvalHaltung(keys, "80945-81176/2025_Saniert")); // mit Suffix
        Assert.True(EvalContaminationGuard.IsEvalHaltung(keys, "81162-287425"));             // umgekehrte Richtung, gleiche Leitung
        Assert.False(EvalContaminationGuard.IsEvalHaltung(keys, "999999-888888"));           // andere Haltung
        Assert.False(EvalContaminationGuard.IsEvalHaltung(keys, ""));                        // leere CaseId
        Assert.False(EvalContaminationGuard.IsEvalHaltung(new HashSet<string>(), "287425-81162")); // leerer Satz
    }

    [Fact]
    public void LoadEvalHaltungKeys_ReadsCandidatesJson_Distinct()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-haltung", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "_candidates.json"), """
                [
                  {"id":"a","haltung_key":"287425-81162","code_main":"BCD"},
                  {"id":"b","haltung_key":"80945-81176","code_main":"BDDC"},
                  {"id":"c","haltung_key":"287425-81162","code_main":"BCE"}
                ]
                """);

            var keys = EvalContaminationGuard.LoadEvalHaltungKeys(root);
            Assert.Equal(2, keys.Count);
            Assert.Contains("287425-81162", keys);
            Assert.Contains("80945-81176", keys);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadEvalHaltungKeys_ToleratesNonStringEntry_KeepsValidKeys()
    {
        // Robustheit: ein nicht-string haltung_key (Zahl) darf NICHT die ganze Liste killen
        // (sonst faellt der Schutz still inaktiv).
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-haltung", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "_candidates.json"), """
                [
                  {"id":"a","haltung_key":"287425-81162"},
                  {"id":"b","haltung_key":12345},
                  {"id":"c","haltung_key":"80945-81176"}
                ]
                """);

            var keys = EvalContaminationGuard.LoadEvalHaltungKeys(root);
            Assert.Contains("287425-81162", keys);
            Assert.Contains("80945-81176", keys);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadEvalHaltungKeys_FallsBackToImageFilenames_WhenNoCandidates()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-haltung", Guid.NewGuid().ToString("N"));
        var images = Path.Combine(root, "images");
        Directory.CreateDirectory(images);
        try
        {
            File.WriteAllBytes(Path.Combine(images, "287425-81162_0.0s_BCD_t+0.png"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(images, "80945-81176_8.8s_BDDC_t+0.png"), new byte[] { 2 });

            var keys = EvalContaminationGuard.LoadEvalHaltungKeys(root);
            Assert.Contains("287425-81162", keys);
            Assert.Contains("80945-81176", keys);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadEvalHaltungKeys_MissingRoot_ReturnsEmpty()
        => Assert.Empty(EvalContaminationGuard.LoadEvalHaltungKeys(
            Path.Combine(Path.GetTempPath(), "no-such-eval-" + Guid.NewGuid().ToString("N"))));

    [Fact]
    public void ClassifyForExport_FlagsEvalHash_EvalHaltung_AndPassesClean()
    {
        // Audit R4: der YOLO-Export muss Eval-Bilder ausschliessen — per Inhalts-Hash UND per Haltung.
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-export", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var evalBytes = new byte[] { 5, 6, 7, 8, 9 };
            var evalHash = Convert.ToHexString(SHA256.HashData(evalBytes)).ToLowerInvariant();
            var hashes = new HashSet<string>(new[] { evalHash }, StringComparer.OrdinalIgnoreCase);
            var haltungen = new HashSet<string>(new[] { "287425-81162" }, StringComparer.OrdinalIgnoreCase);

            // inhaltsgleich zu einem Eval-Bild (anderer Name) -> EvalImageHash
            var copy = Path.Combine(root, "candidate.png");
            File.WriteAllBytes(copy, evalBytes);
            Assert.Equal(EvalContaminationGuard.ExportContaminationResult.EvalImageHash,
                EvalContaminationGuard.ClassifyForExport(hashes, haltungen, copy, "999999-888888"));

            // sauberer Inhalt, aber reservierte Eval-Haltung (mit Suffix) -> EvalHaltung
            var clean = Path.Combine(root, "clean.png");
            File.WriteAllBytes(clean, new byte[] { 42, 43, 44 });
            Assert.Equal(EvalContaminationGuard.ExportContaminationResult.EvalHaltung,
                EvalContaminationGuard.ClassifyForExport(hashes, haltungen, clean, "287425-81162/2025_Saniert"));

            // sauberer Inhalt + andere Haltung -> Clean
            Assert.Equal(EvalContaminationGuard.ExportContaminationResult.Clean,
                EvalContaminationGuard.ClassifyForExport(hashes, haltungen, clean, "111-222"));

            // leere Saetze -> kein falscher Alarm
            Assert.Equal(EvalContaminationGuard.ExportContaminationResult.Clean,
                EvalContaminationGuard.ClassifyForExport(new HashSet<string>(), new HashSet<string>(), copy, "287425-81162"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
