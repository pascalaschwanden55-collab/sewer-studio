using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Fuehrt die fertig ergaenzte Word-Datei und die Beilagen zu einem Gesamt-PDF
/// zusammen.
///
/// Die Word-Datei wird zuerst ueber Microsoft Word und ersatzweise ueber
/// LibreOffice nach PDF gewandelt. Sind beide Wege nicht verfuegbar, entsteht
/// bewusst KEIN Teil-PDF nur aus den Beilagen: Ein Dossier ohne Deckblatt und
/// Eigentuemerangaben saehe vollstaendig aus und waere es nicht.
/// </summary>
public sealed class DossierPdfAssemblyService : IDossierPdfAssemblyService
{
    private readonly IPdfMergeService _pdfMerge;
    private readonly Func<string, string?, bool> _convertWordToPdf;

    public DossierPdfAssemblyService(
        IPdfMergeService pdfMerge,
        Func<string, string?, bool>? convertWordToPdf = null)
    {
        _pdfMerge = pdfMerge ?? throw new ArgumentNullException(nameof(pdfMerge));
        _convertWordToPdf = convertWordToPdf ?? DossierWordPdfConverter.TryConvertToPdf;
    }

    public async Task<DossierPdfAssemblyResult> AssembleAsync(
        string dossierFolder,
        Func<byte[], CancellationToken, Task<IReadOnlySet<int>?>>? waehleSeiten = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dossierFolder);
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(dossierFolder))
            return Fail($"Der Dossier-Ordner fehlt: '{dossierFolder}'.");

        var wordFile = FindNewestWordFile(dossierFolder);
        if (wordFile is null)
        {
            return Fail(
                "Im Dossier-Ordner liegt keine Word-Datei. "
                + "Zuerst „Word erzeugen\" ausführen.");
        }

        var wordPdf = Path.Combine(
            Path.GetTempPath(),
            "dossier_" + Guid.NewGuid().ToString("N") + ".pdf");

        try
        {
            if (!_convertWordToPdf(wordFile, wordPdf) || !File.Exists(wordPdf))
            {
                return Fail(
                    "Die Word-Datei konnte weder mit Microsoft Word noch mit LibreOffice "
                    + "in ein PDF gewandelt werden. Alternative: die Datei in einem der "
                    + "beiden Programme als PDF speichern und die Beilagen "
                    + "von Hand anfügen.");
            }

            var attachments = CollectAttachmentPdfs(dossierFolder);
            var generated = File.ReadAllBytes(wordPdf);
            var merged = attachments.Count == 0
                ? generated
                : _pdfMerge.MergeWithOriginals(generated, attachments);

            // Zwischen „zusammengefuehrt" und „geschrieben": Erst hier stehen
            // alle Blaetter fest — die aus Word UND die Beilagen. Vorher liesse
            // sich gar nicht zeigen, was am Ende in der Datei stuende.
            if (waehleSeiten is not null)
            {
                var ausgeschlossen = await waehleSeiten(merged, ct).ConfigureAwait(false);

                if (ausgeschlossen is null)
                {
                    return new DossierPdfAssemblyResult(
                        false, null, "Das Gesamt-PDF wurde nicht erstellt.");
                }

                try
                {
                    merged = DossierPdfPageFilter.Ohne(merged, ausgeschlossen);
                }
                catch (InvalidOperationException ex)
                {
                    return new DossierPdfAssemblyResult(false, null, ex.Message);
                }
            }

            var targetPath = Path.Combine(
                dossierFolder, DossierFolderPlanner.CombinedPdfFileName);

            var temp = targetPath + ".tmp";
            File.WriteAllBytes(temp, merged);
            if (File.Exists(targetPath))
                File.Replace(temp, targetPath, destinationBackupFileName: null);
            else
                File.Move(temp, targetPath);

            var note = attachments.Count == 0
                ? " (ohne Beilagen — der Ordner „Beilagen\" ist leer)"
                : $" (mit {attachments.Count} Beilagen)";

            return new DossierPdfAssemblyResult(
                true, targetPath, "Gesamt-PDF erstellt" + note + ".");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail($"Das Gesamt-PDF konnte nicht erstellt werden: {ex.Message}");
        }
        finally
        {
            TryDelete(wordPdf);
        }
    }

    /// <summary>
    /// Die zuletzt geaenderte Word-Datei gewinnt: nach einem zweiten
    /// "Word erzeugen" liegen mehrere im Ordner, und gemeint ist die, an der
    /// zuletzt gearbeitet wurde.
    /// </summary>
    private static string? FindNewestWordFile(string folder)
        => Directory
            .EnumerateFiles(folder, "*.docx", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

    /// <summary>
    /// Alle PDFs aus dem Beilagen-Ordner, nach Dateiname sortiert. Die
    /// Nummerierung "01_", "02_" bestimmt damit die Reihenfolge — auch fuer
    /// von Hand hinzugelegte Beilagen wie den Übersichtsplan.
    /// </summary>
    internal static List<string> CollectAttachmentPdfs(string dossierFolder)
    {
        var folder = Path.Combine(dossierFolder, DossierFolderPlanner.AttachmentFolderName);
        if (!Directory.Exists(folder))
            return new List<string>();

        return Directory
            .EnumerateFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DossierPdfAssemblyResult Fail(string message)
        => new(false, null, message);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ein liegen gebliebenes Temp-PDF ist harmlos.
        }
    }
}

/// <summary>
/// Word-zu-PDF ueber das installierte Microsoft Word. Bewusst ueber spaete
/// Bindung, damit ohne Word weder ein Verweis noch ein Ladefehler entsteht.
/// </summary>
internal static class WordInterop
{
    private const int WdExportFormatPdf = 17;
    private const int WdExportCreateWordBookmarks = 2;
    private const int WdDoNotSaveChanges = 0;

    public static bool TryConvertToPdf(string wordPath, string? pdfPath)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(pdfPath))
            return false;

        var wordType = Type.GetTypeFromProgID("Word.Application");
        if (wordType is null)
            return false;

        object? application = null;
        object? documents = null;
        object? document = null;

        try
        {
            application = Activator.CreateInstance(wordType);
            if (application is null)
                return false;

            Set(application, "Visible", false);
            Set(application, "DisplayAlerts", 0);

            documents = Get(application, "Documents");
            document = Invoke(documents, "Open", wordPath, false, true);
            if (document is null)
                return false;

            Invoke(
                document,
                "ExportAsFixedFormat",
                CreateExportAsFixedFormatArguments(pdfPath));
            return File.Exists(pdfPath);
        }
        catch
        {
            return false;
        }
        finally
        {
            TryQuit(document, application);
            Release(document);
            Release(documents);
            Release(application);
        }
    }

    /// <summary>
    /// Argumente fuer Words PDF-Export. Die ausgelassenen Standardwerte bleiben
    /// unveraendert; nur die Word-Textmarken werden ausdruecklich als
    /// PDF-Lesezeichen verlangt. Ohne den elften Wert verwendet Word
    /// <c>wdExportCreateNoBookmarks</c>.
    /// </summary>
    internal static object[] CreateExportAsFixedFormatArguments(string pdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        return
        [
            pdfPath,
            WdExportFormatPdf,
            Type.Missing, // OpenAfterExport
            Type.Missing, // OptimizeFor
            Type.Missing, // Range
            Type.Missing, // From
            Type.Missing, // To
            Type.Missing, // Item
            Type.Missing, // IncludeDocProps
            Type.Missing, // KeepIRM
            WdExportCreateWordBookmarks
        ];
    }

    private static void TryQuit(object? document, object? application)
    {
        try
        {
            if (document is not null)
                Invoke(document, "Close", WdDoNotSaveChanges);
        }
        catch
        {
            // Beim Aufraeumen nicht laut werden.
        }

        try
        {
            if (application is not null)
                Invoke(application, "Quit", WdDoNotSaveChanges);
        }
        catch
        {
            // Word-Prozess bleibt im schlimmsten Fall kurz stehen.
        }
    }

    private static object? Get(object target, string name)
        => target.GetType().InvokeMember(
            name,
            System.Reflection.BindingFlags.GetProperty,
            binder: null,
            target,
            args: null,
            modifiers: null,
            culture: CultureInfo.InvariantCulture,
            namedParameters: null);

    private static void Set(object target, string name, object value)
        => target.GetType().InvokeMember(
            name,
            System.Reflection.BindingFlags.SetProperty,
            binder: null,
            target,
            new[] { value },
            modifiers: null,
            culture: CultureInfo.InvariantCulture,
            namedParameters: null);

    private static object? Invoke(object? target, string name, params object[] args)
        => target?.GetType().InvokeMember(
            name,
            System.Reflection.BindingFlags.InvokeMethod,
            binder: null,
            target,
            args,
            modifiers: null,
            culture: CultureInfo.InvariantCulture,
            namedParameters: null);

    private static void Release(object? comObject)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            if (comObject is not null && Marshal.IsComObject(comObject))
                Marshal.ReleaseComObject(comObject);
        }
        catch
        {
            // Freigabefehler darf den Ablauf nicht stoppen.
        }
    }
}
