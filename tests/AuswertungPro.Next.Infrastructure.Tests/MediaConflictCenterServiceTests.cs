using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Media;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class MediaConflictCenterServiceTests
{
    // Audit R5: gleiche Dateigroesse ist KEIN Identitaetsbeweis. SameHeadAndTail muss zwei
    // gleich grosse, aber verschiedene Videos unterscheiden, damit nicht falsch verlinkt wird.

    [Fact]
    public void SameHeadAndTail_EqualSizeDifferentContent_IsFalse_IdenticalIsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "media-r5", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a.mp4");
            var b = Path.Combine(dir, "b.mp4");
            var bytesA = new byte[5000];
            var bytesB = new byte[5000];
            for (var i = 0; i < bytesA.Length; i++)
            {
                bytesA[i] = (byte)(i % 251);
                bytesB[i] = (byte)((i + 7) % 251);   // gleiche Groesse, anderer Inhalt
            }
            File.WriteAllBytes(a, bytesA);
            File.WriteAllBytes(b, bytesB);

            Assert.False(MediaConflictCenterService.SameHeadAndTail(a, b, bytesA.Length));

            var c = Path.Combine(dir, "c.mp4");
            File.WriteAllBytes(c, bytesA);   // identische Kopie
            Assert.True(MediaConflictCenterService.SameHeadAndTail(a, c, bytesA.Length));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SameHeadAndTail_LargeFilesDifferingOnlyInTail_IsFalse()
    {
        // Exerziert den Schwanz-Vergleich (> 1 MB): Kopf identisch, nur die letzten Bytes anders.
        var dir = Path.Combine(Path.GetTempPath(), "media-r5", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const int size = 1_200_000;
            var a = new byte[size];
            var b = new byte[size];
            Array.Fill(a, (byte)0xAA);
            Array.Fill(b, (byte)0xAA);
            for (var i = size - 100; i < size; i++) b[i] = 0xBB;   // nur Schwanz unterscheidet sich

            var pa = Path.Combine(dir, "a.mp4");
            var pb = Path.Combine(dir, "b.mp4");
            File.WriteAllBytes(pa, a);
            File.WriteAllBytes(pb, b);

            Assert.False(MediaConflictCenterService.SameHeadAndTail(pa, pb, size));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
