using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Verteilt beim Ein-Knopf-Import Dichtheitspruefungsprotokolle (DP) aus der
/// Quelle in die Haltungsordner — Dateiname &lt;JJJJMMTT&gt;_&lt;Haltung&gt;_DP.pdf.
/// Kandidaten sind PDFs, deren Inhalt deterministisch als Dichtheitspruefung
/// erkannt wird. Die KI-Zweitmeinung bleibt auf DP-/Dichtheits-Ordner
/// begrenzt, damit normale Dokumentenordner nicht breit geraten werden.
/// Die Haltungszuordnung uebernimmt die bestehende Logik
/// <see cref="HoldingFolderDistributor.DistributeDichtheitFiles"/> (liest die
/// Schaechte aus dem PDF-Inhalt). Kanalfernseh- und DP-Protokolle liegen damit
/// gemeinsam im Haltungen_Verteilt-Ordner.
/// </summary>
public static class DichtheitImportDistributor
{
    public sealed record Result(
        int Verteilt,
        int NichtZugeordnet,
        int Uebersprungen,
        IReadOnlyList<string> Messages);

    // Ordnersegment weist auf Dichtheitspruefung hin: "DP" als eigenes Wort
    // (048473_DP_Gross) oder "Dichtheit..." im Namen.
    private static readonly Regex DpOrdnerRegex = new(
        @"(^|[_\-\s])DP($|[_\-\s])|Dichtheit",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static Result Distribute(Project project, string projectFolder, string sourceFolder, PdfKiSchiedsrichter? ki = null)
    {
        var messages = new List<string>();
        var kandidaten = new List<string>(FindeKandidaten(sourceFolder));

        // R4: KI-Zweitmeinung fuer DP-Ordner-PDFs, die die deterministische
        // Typ-Erkennung NICHT sicher zuordnen konnte (nur Vorschlag, im Report gekennzeichnet).
        if (ki is not null)
        {
            foreach (var unsicher in FindeUnsichereKandidaten(sourceFolder))
            {
                var klassifikation = FrageKi(ki, unsicher);
                if (klassifikation?.Typ == PdfDokumentTyp.Dichtheitspruefung)
                {
                    kandidaten.Add(unsicher);
                    messages.Add($"Per KI als Dichtheitspruefung klassifiziert: {Path.GetFileName(unsicher)}");
                }
            }
        }

        if (kandidaten.Count == 0)
            return new Result(0, 0, 0, messages);

        var zielRoot = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);

        // Idempotenz-Guard: bereits verteilte DP-Protokolle (gleiche Dateigroesse)
        // nicht erneut kopieren — DistributeDichtheitFiles wuerde sonst bei jedem
        // Lauf _01-Duplikate anlegen.
        var vorhandeneGroessen = LeseVorhandeneDpGroessen(zielRoot);
        var neue = new List<string>();
        var uebersprungen = 0;
        foreach (var kandidat in kandidaten)
        {
            long groesse;
            try
            {
                groesse = new FileInfo(kandidat).Length;
            }
            catch (Exception ex)
            {
                // Best effort: eine unlesbare Datei darf die restlichen DP-Kandidaten nicht blockieren.
                System.Diagnostics.Debug.WriteLine($"[DichtheitImport] Kandidat uebersprungen, Groesse nicht lesbar: {kandidat}: {ex.Message}");
                continue;
            }

            if (vorhandeneGroessen.Contains(groesse))
                uebersprungen++;
            else
                neue.Add(kandidat);
        }

        if (neue.Count == 0)
            return new Result(0, 0, uebersprungen, messages);

        var results = HoldingFolderDistributor.DistributeDichtheitFiles(neue, zielRoot, project: project);

        var verteilt = results.Count(r => r.Success);
        var nichtZugeordnet = 0;
        foreach (var r in results.Where(r => !r.Success))
        {
            // R4: Wenn der Inhalt-Parser das Schachtpaar nicht fand, darf die KI
            // einen Vorschlag machen — Zuordnung wird im Report als "per KI" gekennzeichnet.
            if (ki is not null && !string.IsNullOrWhiteSpace(r.SourcePdfPath)
                && VerteilePerKi(ki, r.SourcePdfPath!, zielRoot, messages))
            {
                verteilt++;
                continue;
            }

            nichtZugeordnet++;
            messages.Add($"DP nicht zugeordnet: {Path.GetFileName(r.SourcePdfPath ?? "?")} — {r.Message}");
        }

        return new Result(verteilt, nichtZugeordnet, uebersprungen, messages);
    }

    /// <summary>KI-Aufruf synchron mit hartem Timeout — Ollama-Ausfall stoppt den Import nie.</summary>
    private static PdfKiKlassifikation? FrageKi(PdfKiSchiedsrichter ki, string pdfPath)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(25));
            return ki.KlassifiziereAsync(pdfPath, cts.Token).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Legt ein per KI zugeordnetes DP-Protokoll unter dem erkannten Schachtpaar ab.</summary>
    private static bool VerteilePerKi(PdfKiSchiedsrichter ki, string pdfPath, string zielRoot, List<string> messages)
    {
        var k = FrageKi(ki, pdfPath);
        if (k is null
            || string.IsNullOrWhiteSpace(k.SchachtVon)
            || string.IsNullOrWhiteSpace(k.SchachtBis))
            return false;

        try
        {
            var haltung = AuswertungPro.Next.Application.Common.ProjectPathResolver
                .SanitizePathSegment($"{k.SchachtVon}-{k.SchachtBis}");
            var stamp = "00000000";
            if (DateTime.TryParseExact(k.Datum ?? "", "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var datum))
                stamp = datum.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

            var dir = Path.Combine(zielRoot, haltung);
            Directory.CreateDirectory(dir);
            var ziel = Path.Combine(dir, $"{stamp}_{haltung}_DP.pdf");
            if (!(File.Exists(ziel) && new FileInfo(ziel).Length == new FileInfo(pdfPath).Length))
            {
                ziel = KanalImportDistributor.UniquePath(ziel);
                File.Copy(pdfPath, ziel, overwrite: false);
            }

            messages.Add($"DP per KI zugeordnet: {Path.GetFileName(pdfPath)} → {haltung}");
            return true;
        }
        catch (Exception ex)
        {
            messages.Add($"DP-KI-Zuordnung fehlgeschlagen ({Path.GetFileName(pdfPath)}): {ex.Message}");
            return false;
        }
    }

    /// <summary>DP-Ordner-PDFs, deren Typ die deterministische Erkennung NICHT bestimmen konnte.</summary>
    internal static IReadOnlyList<string> FindeUnsichereKandidaten(string sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            return Array.Empty<string>();

        try
        {
            return SafeFileEnumeration.EnumerateFilesSafe(sourceFolder, "*.pdf", recursive: true)
                .Where(p => LiegtInDpOrdner(p, sourceFolder))
                .Where(p => PdfDokumentTypErkennung.ErkenneDatei(p) == PdfDokumentTyp.Unbekannt)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Sicher erkannte Dichtheitspruefungs-PDFs der Quelle (rekursiv).</summary>
    internal static IReadOnlyList<string> FindeKandidaten(string sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            return Array.Empty<string>();

        try
        {
            return SafeFileEnumeration.EnumerateFilesSafe(sourceFolder, "*.pdf", recursive: true)
                .Where(p => IstDichtheitsKandidat(p, sourceFolder))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IstDichtheitsKandidat(string pdfPath, string sourceFolder)
    {
        var text = PdfDokumentTypErkennung.ReadPdfTextPrefix(pdfPath, maxPages: 6);
        var typMitDateiname = PdfDokumentTypErkennung.ErkenneText(text, Path.GetFileName(pdfPath));
        if (typMitDateiname != PdfDokumentTyp.Dichtheitspruefung)
            return false;

        if (LiegtInDpOrdner(pdfPath, sourceFolder))
            return true;

        // In neutralen Ordnern wie "Dokumente" reicht ein Dateiname mit "dicht"
        // nicht aus; der Inhalt muss selbst eine Dichtheitspruefung belegen.
        return PdfDokumentTypErkennung.ErkenneText(text, fileName: null) == PdfDokumentTyp.Dichtheitspruefung;
    }

    private static bool LiegtInDpOrdner(string pdfPath, string sourceFolder)
    {
        var dir = Path.GetDirectoryName(pdfPath);
        while (!string.IsNullOrEmpty(dir)
               && dir.StartsWith(sourceFolder, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(dir, sourceFolder, StringComparison.OrdinalIgnoreCase))
        {
            if (DpOrdnerRegex.IsMatch(Path.GetFileName(dir)))
                return true;
            dir = Path.GetDirectoryName(dir);
        }

        return false;
    }

    private static HashSet<long> LeseVorhandeneDpGroessen(string zielRoot)
    {
        var groessen = new HashSet<long>();
        if (!Directory.Exists(zielRoot))
            return groessen;

        try
        {
            foreach (var pfad in SafeFileEnumeration.EnumerateFilesSafe(zielRoot, "*_DP*.pdf", recursive: true))
            {
                try
                {
                    groessen.Add(new FileInfo(pfad).Length);
                }
                catch (Exception ex)
                {
                    // Best effort: defekte Ziel-Dateien verhindern nur die Duplikat-Erkennung fuer diese Datei.
                    System.Diagnostics.Debug.WriteLine($"[DichtheitImport] Vorhandenes DP-Protokoll ohne Groesse ignoriert: {pfad}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Best effort: wenn der Zielbaum nicht lesbar ist, laeuft der Import ohne Idempotenz-Guard weiter.
            System.Diagnostics.Debug.WriteLine($"[DichtheitImport] Vorhandene DP-Groessen konnten nicht gelesen werden: {zielRoot}: {ex.Message}");
        }

        return groessen;
    }
}
