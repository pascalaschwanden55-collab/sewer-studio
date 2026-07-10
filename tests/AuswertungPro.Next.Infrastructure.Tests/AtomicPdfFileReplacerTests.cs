using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AtomicPdfFileReplacerTests
{
    [Fact]
    public void Fallback_StelltOriginalWiederHer_WennNeueDateiNichtEingesetztWerdenKann()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfreplace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "ziel.pdf");
        var staged = Path.Combine(dir, "neu.pdf");
        var backup = target + ".bak";
        File.WriteAllText(target, "alt");
        File.WriteAllText(staged, "neu");
        var moveCount = 0;

        try
        {
            Assert.Throws<IOException>(() =>
                AtomicPdfFileReplacer.ReplaceExistingPreservingOriginal(
                    staged,
                    target,
                    backup,
                    replace: (_, _, _) => throw new PlatformNotSupportedException(),
                    move: (source, destination, overwrite) =>
                    {
                        moveCount++;
                        if (moveCount == 2)
                            throw new IOException("simulierter Schreibfehler");
                        File.Move(source, destination, overwrite);
                    }));

            Assert.Equal("alt", File.ReadAllText(target));
            Assert.Equal("neu", File.ReadAllText(staged));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
