using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Serialisiert den kompletten Beilagenlauf pro kanonischem Ordner. Der
/// benannte Mutex gilt auch zwischen zwei SewerStudio-Prozessen; dadurch
/// gehoeren Manifest-Lesen, PDF-Publikation und Manifest-Schreiben immer zu
/// demselben Ausgangsstand.
/// </summary>
internal sealed class DossierAttachmentFolderLock : IDisposable
{
    private const int WaitSliceMilliseconds = 100;

    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private DossierAttachmentFolderLock(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public static DossierAttachmentFolderLock Acquire(
        string attachmentFolder,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentFolder);

        var canonical = Path.GetFullPath(attachmentFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var mutex = new Mutex(
            initiallyOwned: false,
            name: "Local\\SewerStudio.DossierAttachments." + digest);

        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (mutex.WaitOne(WaitSliceMilliseconds))
                        return new DossierAttachmentFolderLock(mutex, ownsMutex: true);
                }
                catch (AbandonedMutexException)
                {
                    // Der vorige Prozess ist mitten im Lauf beendet worden.
                    // Der Mutex gehoert jetzt diesem Aufrufer; Manifest und
                    // Hashpruefungen entscheiden anschliessend fail-closed.
                    return new DossierAttachmentFolderLock(mutex, ownsMutex: true);
                }
            }
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (!_ownsMutex)
            return;

        _ownsMutex = false;
        try
        {
            _mutex.ReleaseMutex();
        }
        finally
        {
            _mutex.Dispose();
        }
    }
}
