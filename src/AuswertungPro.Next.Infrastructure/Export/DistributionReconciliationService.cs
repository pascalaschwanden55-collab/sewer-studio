using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Export;

/// <summary>
/// Vergleicht die Verteilordner mit den Haltungs- und Schachtnummern des Projekts und
/// verschiebt alles ohne Gegenstueck in den Papierkorb.
///
/// Zugeordnet wird nach DERSELBEN Regel wie beim Verteilen: sanitisierter Name,
/// IBAK-Normalisierung und die vertauschte Schachtreihenfolge (A-B ist B-A). Wer hier
/// strenger vergleicht, raeumt Ordner weg, die die Verteilung selbst als Treffer ansieht.
/// </summary>
public sealed class DistributionReconciliationService : IDistributionReconciliationService
{
    /// <summary>Ordner im Projekt, in den Nicht-Zugeordnetes wandert.</summary>
    public const string PapierkorbOrdner = ProjectStructure.Papierkorb;

    private static readonly string[] Verteilordner =
    {
        ProjectStructure.HaltungenVerteilt,
        ProjectStructure.SchaechteVerteilt
    };

    public DistributionReconciliationPlan Plan(string projectFolder, Project project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentNullException.ThrowIfNull(project);

        var bekannt = SammleBekannteNamen(project);
        if (bekannt.Haltungen.Count == 0 && bekannt.Schaechte.Count == 0)
        {
            // Ein versehentlich leeres Projekt wuerde sonst beide Ordner komplett ausraeumen.
            return new DistributionReconciliationPlan(
                Array.Empty<DistributionReconciliationEntry>(),
                Array.Empty<string>(),
                "Im Projekt sind weder Haltungen noch Schaechte geladen. Der Abgleich würde "
                + "die Verteilordner vollständig leeren und wird deshalb nicht ausgeführt.");
        }

        var zuVerschieben = new List<DistributionReconciliationEntry>();
        var uebersprungen = new List<string>();

        foreach (var wurzelName in Verteilordner)
        {
            var wurzel = Path.Combine(projectFolder, wurzelName);
            if (!Directory.Exists(wurzel))
                continue;

            // Haltungsordner nur gegen Haltungen, Schachtordner nur gegen Schaechte (GA02): Eine Haltung
            // «A-B» verdeckte sonst einen verwaisten Schachtordner «A-B».
            var namen = bekannt.Fuer(wurzelName);
            if (namen.Count == 0)
            {
                // Wie beim leeren Projekt: Eine leere Liste darf einen Verteilordner nicht ausraeumen.
                uebersprungen.Add($"{wurzelName}: Im Projekt sind keine {ObjektWort(wurzelName)} geladen — "
                    + "dieser Ordner wird nicht abgeglichen.");
                continue;
            }

            PruefeOrdner(wurzel, wurzelName, namen, zuVerschieben, uebersprungen);
            PruefeLoseDateien(wurzel, wurzelName, uebersprungen, zuVerschieben);
        }

        return new DistributionReconciliationPlan(zuVerschieben, uebersprungen, null);
    }

    public DistributionReconciliationResult Apply(
        string projectFolder,
        DistributionReconciliationPlan plan,
        DateTime nowLocal)
        => ApplyCore(projectFolder, plan, nowLocal, aktuellesProjekt: null);

    public DistributionReconciliationResult Apply(
        string projectFolder,
        DistributionReconciliationPlan plan,
        DateTime nowLocal,
        Project aktuellesProjekt)
    {
        ArgumentNullException.ThrowIfNull(aktuellesProjekt);
        return ApplyCore(projectFolder, plan, nowLocal, aktuellesProjekt);
    }

    private DistributionReconciliationResult ApplyCore(
        string projectFolder,
        DistributionReconciliationPlan plan,
        DateTime nowLocal,
        Project? aktuellesProjekt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentNullException.ThrowIfNull(plan);

        var meldungen = new List<string>();
        if (!string.IsNullOrWhiteSpace(plan.BlockedReason))
        {
            meldungen.Add(plan.BlockedReason!);
            return new DistributionReconciliationResult(0, 0, null, meldungen);
        }

        if (plan.ToMove.Count == 0)
            return new DistributionReconciliationResult(0, 0, null, meldungen);

        var laufName = Path.Combine(
            PapierkorbOrdner,
            nowLocal.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        var laufOrdner = Path.Combine(projectFolder, laufName);

        // Der Projektstand beim Bestaetigen, nicht der beim Planen (GA02): Wurde inzwischen eine passende
        // Haltung oder ein Schacht ergaenzt, bleibt der Ordner liegen.
        var jetztBekannt = aktuellesProjekt is null ? null : SammleBekannteNamen(aktuellesProjekt);
        var pfade = new ProjectWritePathGuard(projectFolder);

        var ordner = 0;
        var dateien = 0;
        foreach (var eintrag in plan.ToMove)
        {
            var quelle = Path.Combine(projectFolder, eintrag.RelativePath);
            var ziel = Path.Combine(laufOrdner, eintrag.RelativePath);

            if (jetztBekannt is not null && eintrag.IsDirectory && IstInzwischenBekannt(jetztBekannt, eintrag.RelativePath))
            {
                meldungen.Add($"Nicht verschoben (inzwischen im Projekt vorhanden): {eintrag.RelativePath}");
                continue;
            }

            // Zwischen Plan und Ausfuehrung kann sich etwas geaendert haben; erneut pruefen.
            if (!IstSicheresZiel(projectFolder, quelle) || IstVerknuepfung(quelle))
            {
                meldungen.Add($"Nicht angefasst (unsicherer Pfad): {eintrag.RelativePath}");
                continue;
            }

            try
            {
                // Quelle UND Ziel hinter der Schreibgrenze des Projekts, unmittelbar vor der Bewegung: Ist der
                // Papierkorb (oder ein Ordner davor) eine Verknuepfung, landete der Inhalt sonst ausserhalb (GA02).
                pfade.EnsureSafeDirectoryTarget(Path.GetDirectoryName(quelle)!);
                var zielOrdner = pfade.EnsureSafeDirectoryTarget(Path.GetDirectoryName(ziel)!);
                Directory.CreateDirectory(zielOrdner);
                pfade.EnsureSafeDirectoryTarget(zielOrdner);
                ziel = pfade.EnsureSafeFileTarget(ziel);
                if (eintrag.IsDirectory)
                {
                    if (!Directory.Exists(quelle))
                        continue;
                    Directory.Move(quelle, ziel);
                    ordner++;
                }
                else
                {
                    if (!File.Exists(quelle))
                        continue;
                    File.Move(quelle, ziel, overwrite: false);
                    dateien++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                           or ArgumentException or NotSupportedException)
            {
                meldungen.Add($"Nicht verschoben: {eintrag.RelativePath} ({ex.Message})");
            }
        }

        foreach (var hinweis in plan.Skipped)
            meldungen.Add(hinweis);

        return new DistributionReconciliationResult(
            ordner,
            dateien,
            ordner + dateien > 0 ? laufName : null,
            meldungen);
    }

    // ---- Zuordnung --------------------------------------------------------

    /// <summary>
    /// Alle Namen, die im Verteilordner erlaubt sind - normalisiert und inklusive der
    /// vertauschten Schachtreihenfolge.
    /// </summary>
    private static BekannteNamen SammleBekannteNamen(Project project)
    {
        var haltungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var schaechte = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in project.Data.ToList())
        {
            var name = record.GetFieldValue(FieldKeys.HoldingName);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            Aufnehmen(haltungen, name);

            var teile = name.Split('-');
            if (teile.Length == 2)
                Aufnehmen(haltungen, teile[1] + "-" + teile[0]);
        }

        foreach (var schacht in project.SchaechteData.ToList())
        {
            var nummer = schacht.GetFieldValue("Schachtnummer");
            if (!string.IsNullOrWhiteSpace(nummer))
                Aufnehmen(schaechte, nummer);
        }

        return new BekannteNamen(haltungen, schaechte);
    }

    /// <summary>Erlaubte Ordnernamen je Verteilordner: Haltungen und Schaechte getrennt (GA02).</summary>
    private sealed record BekannteNamen(HashSet<string> Haltungen, HashSet<string> Schaechte)
    {
        public HashSet<string> Fuer(string wurzelName)
            => string.Equals(wurzelName, ProjectStructure.SchaechteVerteilt, StringComparison.OrdinalIgnoreCase)
                ? Schaechte
                : Haltungen;
    }

    private static bool IstInzwischenBekannt(BekannteNamen bekannt, string relativerPfad)
    {
        var teile = relativerPfad.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return teile.Length >= 2 && bekannt.Fuer(teile[0]).Contains(Schluessel(teile[^1]));
    }

    private static string ObjektWort(string wurzelName)
        => string.Equals(wurzelName, ProjectStructure.SchaechteVerteilt, StringComparison.OrdinalIgnoreCase)
            ? "Schächte"
            : "Haltungen";

    private static void Aufnehmen(HashSet<string> bekannt, string name)
    {
        var schluessel = Schluessel(name);
        if (!string.IsNullOrWhiteSpace(schluessel))
            bekannt.Add(schluessel);
    }

    /// <summary>
    /// Derselbe Weg wie beim Verteilen: erst sanitisieren (so heisst der Ordner auf der
    /// Platte), dann normalisieren (so vergleicht der Verteiler).
    /// </summary>
    private static string Schluessel(string? name)
        => HoldingKeyNormalizer.NormalizeIbak(ProjectPathResolver.SanitizePathSegment(name));

    // ---- Durchsuchen ------------------------------------------------------

    private static void PruefeOrdner(
        string wurzel,
        string wurzelName,
        HashSet<string> bekannt,
        List<DistributionReconciliationEntry> zuVerschieben,
        List<string> uebersprungen)
    {
        string[] unterordner;
        try
        {
            unterordner = Directory.GetDirectories(wurzel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            uebersprungen.Add($"{wurzelName}: Ordner nicht lesbar ({ex.Message}).");
            return;
        }

        foreach (var ordner in unterordner.OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(ordner);

            if (IstVerknuepfung(ordner))
            {
                // Der Inhalt laege ausserhalb des Projekts - nie verschieben.
                uebersprungen.Add(
                    $"{wurzelName}\\{name}: Verknüpfung, wurde nicht angefasst.");
                continue;
            }

            if (bekannt.Contains(Schluessel(name)))
                continue;

            zuVerschieben.Add(new DistributionReconciliationEntry(
                Path.Combine(wurzelName, name), IsDirectory: true));
        }
    }

    /// <summary>
    /// Dateien direkt in der Wurzel eines Verteilordners gehoeren dort nicht hin -
    /// die Verteilung legt immer einen Unterordner je Haltung/Schacht an.
    /// </summary>
    private static void PruefeLoseDateien(
        string wurzel,
        string wurzelName,
        List<string> uebersprungen,
        List<DistributionReconciliationEntry> zuVerschieben)
    {
        string[] dateien;
        try
        {
            dateien = Directory.GetFiles(wurzel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            uebersprungen.Add($"{wurzelName}: Dateien nicht lesbar ({ex.Message}).");
            return;
        }

        foreach (var datei in dateien.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(datei);
            if (IstVerknuepfung(datei))
            {
                uebersprungen.Add($"{wurzelName}\\{name}: Verknüpfung, wurde nicht angefasst.");
                continue;
            }

            zuVerschieben.Add(new DistributionReconciliationEntry(
                Path.Combine(wurzelName, name), IsDirectory: false));
        }
    }

    private static bool IstVerknuepfung(string pfad)
    {
        try
        {
            return (File.GetAttributes(pfad) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            // Nicht lesbar heisst im Zweifel: nicht anfassen.
            return true;
        }
    }

    private static bool IstSicheresZiel(string projectFolder, string kandidat)
    {
        try
        {
            var wurzel = Path.GetFullPath(projectFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var voll = Path.GetFullPath(kandidat);
            return voll.StartsWith(
                wurzel + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
            or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
