using System;
using System.IO;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Vergleicht zwei Dateien byteweise auf gleichen Inhalt (Laengen-Kurzschluss + 8-KiB-Puffer).
/// Gemeinsame Quelle fuer die Import-Kopier- und Verteilwege, damit die Dedup-Entscheidung
/// "gleiche Datei?" nicht mehrfach kopiert auseinanderlaeuft.
/// </summary>
public static class FileContentComparer
{
    public static bool FilesEqual(string firstPath, string secondPath)
    {
        var firstInfo = new FileInfo(firstPath);
        var secondInfo = new FileInfo(secondPath);
        if (firstInfo.Length != secondInfo.Length)
            return false;

        using var first = File.OpenRead(firstPath);
        using var second = File.OpenRead(secondPath);
        var firstBuffer = new byte[8192];
        var secondBuffer = new byte[8192];
        while (true)
        {
            var firstRead = first.Read(firstBuffer, 0, firstBuffer.Length);
            var secondRead = second.Read(secondBuffer, 0, secondBuffer.Length);
            if (firstRead != secondRead)
                return false;
            if (firstRead == 0)
                return true;
            if (!firstBuffer.AsSpan(0, firstRead).SequenceEqual(secondBuffer.AsSpan(0, secondRead)))
                return false;
        }
    }
}
