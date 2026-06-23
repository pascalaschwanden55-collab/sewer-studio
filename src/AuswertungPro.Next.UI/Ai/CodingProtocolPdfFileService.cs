using System;
using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingProtocolPdfFileService
{
    private readonly Action<string, byte[]> _writeAllBytes;
    private readonly Func<string, bool> _tryOpen;

    public CodingProtocolPdfFileService()
        : this(
            File.WriteAllBytes,
            path => SafeShellOpen.TryOpen(path, out _))
    {
    }

    public CodingProtocolPdfFileService(
        Action<string, byte[]> writeAllBytes,
        Func<string, bool> tryOpen)
    {
        _writeAllBytes = writeAllBytes ?? throw new ArgumentNullException(nameof(writeAllBytes));
        _tryOpen = tryOpen ?? throw new ArgumentNullException(nameof(tryOpen));
    }

    public void SaveAndOpen(string filePath, byte[] pdf)
    {
        _writeAllBytes(filePath, pdf);
        _tryOpen(filePath);
    }
}
