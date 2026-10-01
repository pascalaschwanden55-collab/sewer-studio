using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Wandelt die Dossier-Word-Datei in ein PDF. Microsoft Word bleibt der erste
/// Weg, weil es die Vorlage am genauesten wiedergibt. Fehlt Word oder scheitert
/// die Umwandlung, übernimmt automatisch LibreOffice.
/// </summary>
internal static class DossierWordPdfConverter
{
    public static bool TryConvertToPdf(string wordPath, string? pdfPath)
        => TryConvertToPdf(
            wordPath,
            pdfPath,
            WordInterop.TryConvertToPdf,
            LibreOfficeWriterPdfConverter.TryConvertToPdf);

    internal static bool TryConvertToPdf(
        string wordPath,
        string? pdfPath,
        Func<string, string?, bool> tryMicrosoftWord,
        Func<string, string?, bool> tryLibreOffice)
    {
        ArgumentNullException.ThrowIfNull(tryMicrosoftWord);
        ArgumentNullException.ThrowIfNull(tryLibreOffice);

        if (tryMicrosoftWord(wordPath, pdfPath))
            return true;

        TryDeleteFile(pdfPath);
        return tryLibreOffice(wordPath, pdfPath);
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Der zweite Wandler entscheidet anschliessend selbst, ob er schreiben kann.
        }
    }
}

/// <summary>
/// Word-zu-PDF über LibreOffice Writer. Ein eigener, kurzlebiger Benutzerordner
/// verhindert Konflikte mit einem bereits geöffneten LibreOffice.
/// </summary>
internal static class LibreOfficeWriterPdfConverter
{
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(2);

    private static readonly object Schloss = new();

    public static bool TryConvertToPdf(string wordPath, string? pdfPath)
    {
        if (!OperatingSystem.IsWindows() ||
            string.IsNullOrWhiteSpace(wordPath) ||
            string.IsNullOrWhiteSpace(pdfPath) ||
            !File.Exists(wordPath))
        {
            return false;
        }

        var executable = FindExecutable();
        if (executable is null)
            return false;

        return TryConvertToPdf(
            wordPath,
            pdfPath,
            profil => Wandle(executable, wordPath, pdfPath, profil));
    }

    /// <summary>
    /// Der Ablauf um die eigentliche Umwandlung: ein wiederverwendetes
    /// Benutzerprofil, und bei einem Fehlschlag genau ein zweiter Versuch mit
    /// frischem Profil.
    ///
    /// Das wiederverwendete Profil ist die ganze Beschleunigung — gemessen
    /// 2,35 s je Lauf mit eigenem Profil gegen rund 1,0 s ab dem zweiten Lauf
    /// mit geteiltem. Der zweite Versuch ist der Preis dafuer: Ein beschaedigtes
    /// Profil wuerde sonst jede weitere Umwandlung dauerhaft kosten.
    ///
    /// Serialisiert, weil LibreOffice ein Profil nicht zweimal gleichzeitig
    /// verwenden kann.
    /// </summary>
    internal static bool TryConvertToPdf(
        string wordPath,
        string? pdfPath,
        Func<string, bool> wandleMitProfil)
    {
        ArgumentNullException.ThrowIfNull(wandleMitProfil);
        _ = wordPath;
        _ = pdfPath;

        lock (Schloss)
        {
            if (wandleMitProfil(LibreOfficeProfileStore.Ordner()))
                return true;

            LibreOfficeProfileStore.Erneuere();
            return wandleMitProfil(LibreOfficeProfileStore.Ordner());
        }
    }

    private static bool Wandle(
        string executable,
        string wordPath,
        string pdfPath,
        string profileFolder)
    {
        var workFolder = Path.Combine(
            Path.GetTempPath(),
            "SewerStudio_LibreOffice_" + Guid.NewGuid().ToString("N"));
        var outputFolder = Path.Combine(workFolder, "output");

        try
        {
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(profileFolder);

            var startInfo = CreateStartInfo(
                executable,
                wordPath,
                outputFolder,
                profileFolder);

            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            if (!process.WaitForExit((int)ConversionTimeout.TotalMilliseconds))
            {
                TryStop(process);
                return false;
            }

            if (process.ExitCode != 0)
                return false;

            var generatedPdf = Path.Combine(
                outputFolder,
                Path.GetFileNameWithoutExtension(wordPath) + ".pdf");
            if (!File.Exists(generatedPdf))
                return false;

            var targetFolder = Path.GetDirectoryName(Path.GetFullPath(pdfPath));
            if (!string.IsNullOrWhiteSpace(targetFolder))
                Directory.CreateDirectory(targetFolder);

            File.Move(generatedPdf, pdfPath, overwrite: true);
            return File.Exists(pdfPath);
        }
        catch
        {
            return false;
        }
        finally
        {
            // Nur der Arbeitsordner dieses Laufs — das Profil bleibt stehen,
            // sonst waere die Beschleunigung wieder weg.
            TryDeleteWorkFolder(workFolder);
        }
    }

    /// <summary>
    /// Der PDF-Filter samt Schalter fuer die benannten Ziele.
    ///
    /// Ohne <c>ExportBookmarksToPDFDestination</c> schreibt LibreOffice KEINE
    /// einzige Word-Textmarke in die PDF - gemessen an einem echten Lauf mit der
    /// ausgelieferten Vorlage. Genau diese Ziele braucht die Vorschau, um ein
    /// Feld exakt statt ueber seinen Text zuzuordnen.
    ///
    /// Die Angabe muss EIN Argument bleiben; als mehrere uebergeben deutet
    /// LibreOffice sie als Dateinamen.
    /// </summary>
    internal const string PdfFilter =
        "pdf:writer_pdf_Export:"
        + "{\"ExportBookmarksToPDFDestination\":{\"type\":\"boolean\",\"value\":\"true\"}}";

    internal static ProcessStartInfo CreateStartInfo(
        string executable,
        string wordPath,
        string outputFolder,
        string profileFolder)
    {
        var profileUri = new Uri(
            Path.GetFullPath(profileFolder) + Path.DirectorySeparatorChar).AbsoluteUri;
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        startInfo.ArgumentList.Add("-env:UserInstallation=" + profileUri);
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--nodefault");
        startInfo.ArgumentList.Add("--nofirststartwizard");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(PdfFilter);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputFolder);
        startInfo.ArgumentList.Add(wordPath);
        return startInfo;
    }

    private static string? FindExecutable()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "LibreOffice", "program", "soffice.exe")
        };

        AddInstalledCandidate(candidates, Environment.SpecialFolder.ProgramFiles);
        AddInstalledCandidate(candidates, Environment.SpecialFolder.ProgramFilesX86);

        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathVariable))
        {
            candidates.AddRange(pathVariable
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(folder => Path.Combine(folder.Trim(), "soffice.exe")));
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    private static void AddInstalledCandidate(
        ICollection<string> candidates,
        Environment.SpecialFolder folder)
    {
        var root = Environment.GetFolderPath(folder);
        if (!string.IsNullOrWhiteSpace(root))
            candidates.Add(Path.Combine(root, "LibreOffice", "program", "soffice.exe"));
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(milliseconds: 5_000);
            }
        }
        catch
        {
            // Nur der von SewerStudio gestartete LibreOffice-Prozess wird beendet.
        }
    }

    private static void TryDeleteWorkFolder(string folder)
    {
        try
        {
            var fullFolder = Path.GetFullPath(folder);
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var tempPrefix = Path.EndsInDirectorySeparator(tempRoot)
                ? tempRoot
                : tempRoot + Path.DirectorySeparatorChar;

            if (!fullFolder.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            if (Directory.Exists(fullFolder))
                Directory.Delete(fullFolder, recursive: true);
        }
        catch
        {
            // Ein liegen gebliebener, eigener Temp-Ordner ist harmlos.
        }
    }
}
