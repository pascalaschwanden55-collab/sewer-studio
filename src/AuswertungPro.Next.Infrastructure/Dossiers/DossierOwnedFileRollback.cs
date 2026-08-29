using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.Win32.SafeHandles;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Entfernt eine vom Dossier-Speicher angelegte Datei nur dann, wenn exakt
/// dieselbe geoeffnete Datei noch den erwarteten Inhalt besitzt.
/// </summary>
internal static class DossierOwnedFileRollback
{
    private const uint GenericRead = 0x80000000;
    private const uint DeleteAccess = 0x00010000;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const int FileDispositionInfoClass = 4;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;

    public static bool DeleteIfSha256Matches(
        string path,
        ReadOnlySpan<byte> expectedSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (expectedSha256.Length != SHA256.HashSizeInBytes)
        {
            throw new ArgumentException(
                "Der erwartete SHA-256-Wert ist ungueltig.",
                nameof(expectedSha256));
        }

        // SewerStudio ist eine Windows-WPF-Anwendung. Auf anderen Plattformen
        // bleibt die Datei aus Sicherheitsgruenden unangetastet.
        if (!OperatingSystem.IsWindows())
            return false;

        using var handle = CreateFileW(
            path,
            GenericRead | DeleteAccess,
            shareMode: 0,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if (error is ErrorFileNotFound or ErrorPathNotFound)
                return false;

            throw new Win32Exception(
                error,
                "Die eigene Dossierdatei konnte fuer die sichere Ruecknahme nicht gesperrt werden.");
        }

        // Ohne Freigaben kann die Datei zwischen Pruefung und Loeschmarkierung
        // weder ersetzt noch veraendert werden. Die Loeschung gilt fuer genau
        // diesen Handle und wird erst beim Schliessen wirksam.
        using var stream = new FileStream(handle, FileAccess.Read);
        var actualSha256 = SHA256.HashData(stream);
        if (!CryptographicOperations.FixedTimeEquals(actualSha256, expectedSha256))
            return false;

        var disposition = new FileDispositionInfo { DeleteFile = true };
        if (!SetFileInformationByHandle(
                handle,
                FileDispositionInfoClass,
                ref disposition,
                (uint)Marshal.SizeOf<FileDispositionInfo>()))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Die eigene Dossierdatei konnte nicht sicher zurueckgenommen werden.");
        }

        return true;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int fileInformationClass,
        ref FileDispositionInfo fileInformation,
        uint bufferSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool DeleteFile;
    }
}
