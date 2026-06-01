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
    public void ComputeFileHash_MatchesStageAExporterFormat_LowercaseHex()
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
}
