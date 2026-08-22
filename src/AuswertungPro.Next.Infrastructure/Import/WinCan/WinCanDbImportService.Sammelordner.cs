using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Sammelordner-Teil des WinCan-Imports: Erkennung der fachlichen Datenbank, Einlesen
/// mehrerer WinCan-Projekte unter einem gewaehlten Ordner und die Namensvergabe bei
/// gleichnamigen, aber verschiedenen Haltungen.
/// </summary>
public sealed partial class WinCanDbImportService
{
    /// <summary>
    /// Waehlt fuer einen Projektordner die fachliche WinCan-Datenbank.
    ///
    /// Es wird NICHT nach Dateigroesse geraten, sondern in jede Datei hineingeschaut
    /// (<see cref="WinCanDb3Pruefer"/>). Genau dieselbe Pruefung verwendet auch
    /// <see cref="KanalExportDetector"/> — nur so koennen Erkennung und Import nicht
    /// wieder auf verschiedene Dateien laufen.
    /// </summary>
    internal static QuellenwahlErgebnis WaehleDatenbank(string projektWurzel)
        => Quellenwahl.Waehle(
            WinCanDb3Pruefer.FindeKandidaten(projektWurzel),
            WinCanDb3Pruefer.Pruefe);

    /// <summary>
    /// Liefert je gefundenem WinCan-Projekt den Ordner ueber dem "DB"-Verzeichnis.
    ///
    /// Die Auswahl laeuft spaeter JE Projektordner. Ein Sammelordner darf nicht nur
    /// einen einzigen Gewinner haben, sonst fallen die uebrigen Projekte still weg
    /// (Andermatt: 4 von 5 Projekten blieben liegen).
    /// </summary>
    private static List<string> FindWinCanProjektWurzeln(string exportRoot)
    {
        var wurzeln = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in SafeFileEnumeration.EnumerateFilesSafe(exportRoot, "*", recursive: true))
        {
            if (!WinCanDb3Pruefer.IstKandidat(path))
                continue;

            var dbOrdner = Path.GetDirectoryName(path);
            var projektWurzel = dbOrdner is null ? null : Path.GetDirectoryName(dbOrdner);
            if (string.IsNullOrWhiteSpace(projektWurzel) || !gesehen.Add(projektWurzel))
                continue;

            wurzeln.Add(projektWurzel);
        }

        wurzeln.Sort(StringComparer.OrdinalIgnoreCase);
        return wurzeln;
    }

    private Result<ImportStats> ImportMehrereProjekte(
        List<string> projektWurzeln,
        Project project,
        ImportRunContext? ctx)
    {
        var messages = new List<string>
        {
            $"{projektWurzeln.Count} WinCan-Projekte im gewaehlten Ordner gefunden."
        };

        var found = 0;
        var created = 0;
        var updated = 0;
        var errors = 0;
        var uncertain = 0;
        // Ein Gewinner JE Projektordner: die Protokolle aller Projekte werden zu einem
        // gemeinsamen Protokoll zusammengefuehrt, damit das Plausibilitaetstor den
        // ganzen Sammelordner beurteilt und nicht nur das letzte Projekt.
        var alleVersuche = new List<QuellenVersuch>();
        var erwarteteHaltungen = 0;
        var bearbeiteteHaltungen = 0;

        var index = 0;
        foreach (var wurzel in projektWurzeln)
        {
            ctx?.CancellationToken.ThrowIfCancellationRequested();
            index++;

            var name = Path.GetFileName(wurzel);
            ctx?.Progress?.Report(new ImportProgress(
                "WinCan-Projekte importieren", index, projektWurzeln.Count,
                $"Projekt {index}/{projektWurzeln.Count}", name));
            messages.Add($"--- Projekt {index}/{projektWurzeln.Count}: {name}");

            Result<ImportStats> teil;
            try
            {
                teil = ImportEinzelnesProjekt(wurzel, project, ctx, KuerzeZonenName(wurzel));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Ein defektes Projekt darf die restlichen nicht blockieren.
                errors++;
                messages.Add($"Projekt {name} fehlgeschlagen: {ex.Message}");
                ctx?.Log.AddEntry("WinCan", "Projekt", ImportLogStatus.Error,
                    sourceFile: wurzel, detail: ex.Message);
                continue;
            }

            if (!teil.Ok || teil.Value is null)
            {
                errors++;
                messages.Add($"Projekt {name} fehlgeschlagen: {teil.ErrorMessage ?? "unbekannter Fehler"}");
                continue;
            }

            found += teil.Value.Found;
            created += teil.Value.Created;
            updated += teil.Value.Updated;
            errors += teil.Value.Errors;
            uncertain += teil.Value.Uncertain;
            erwarteteHaltungen += teil.Value.ErwarteteHaltungen;
            bearbeiteteHaltungen += teil.Value.BearbeiteteHaltungen;
            if (teil.Value.Quellenprotokoll is { } protokoll)
                alleVersuche.AddRange(protokoll.AlleVersuche);
            messages.AddRange(teil.Value.Messages);
        }

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;

        var gesamtprotokoll = alleVersuche.Count == 0
            ? null
            : new QuellenwahlErgebnis(
                alleVersuche.FirstOrDefault(v => v.Befund.Tauglichkeit == QuellenTauglichkeit.Tauglich),
                alleVersuche);

        return Result<ImportStats>.Success(
            new ImportStats(found, created, updated, errors, uncertain, messages)
            {
                ErwarteteHaltungen = erwarteteHaltungen,
                BearbeiteteHaltungen = bearbeiteteHaltungen,
                Quellenprotokoll = gesamtprotokoll
            });
    }

    /// <summary>
    /// Bildet den Haltungsnamen.
    ///
    /// Eine Haltungsnummer ist <b>Schacht oben - Schacht unten</b>. Die WinCan-Angabe
    /// <c>OBJ_Key</c> ("H6", "H10") ist dagegen nur eine Laufnummer des Operateurs und
    /// innerhalb eines Projekts eindeutig — im Bestand Andermatt kam "H6" in zwei Zonen
    /// fuer zwei voellig verschiedene Haltungen vor.
    ///
    /// Belegt am Kundenbestand (2026-08-21): Der PDF-Protokollimport bildet bereits genau
    /// diese Namen, und alle 15 Haltungen sind so im Abwasserkataster Uri wiederzufinden.
    /// Beide Importwege treffen sich dadurch auf demselben Datensatz und ergaenzen sich,
    /// statt zwei getrennte Bestaende zu erzeugen.
    ///
    /// Ohne beide Schaechte bleibt die WinCan-Bezeichnung als Rueckfall bestehen; dann
    /// greift weiterhin die Trennung gleichnamiger, aber verschiedener Haltungen.
    /// </summary>
    private static string BestimmeHaltungsname(
        Project project,
        WinCanDbSection section,
        Dictionary<string, string> nodeKeyByPk,
        string? zonenName,
        List<string> messages)
    {
        var oben = SchachtOben(section, nodeKeyByPk)?.Trim();
        var unten = SchachtUnten(section, nodeKeyByPk)?.Trim();

        if (!string.IsNullOrWhiteSpace(oben) && !string.IsNullOrWhiteSpace(unten))
        {
            var nummer = $"{oben}-{unten}";
            if (!string.Equals(nummer, section.Key, StringComparison.OrdinalIgnoreCase))
            {
                // Die WinCan-Bezeichnung bleibt im Bericht nachvollziehbar — die Medien
                // heissen weiterhin danach (H6_00001.mp4).
                messages.Add($"Haltung {nummer} (WinCan-Bezeichnung {section.Key})");
            }

            return nummer;
        }

        messages.Add(
            $"Haltung {section.Key}: Schacht oben/unten unvollstaendig, "
            + "Haltungsnummer konnte nicht aus dem Schachtpaar gebildet werden.");

        return BestimmeRueckfallname(project, section, nodeKeyByPk, zonenName, messages);
    }

    /// <summary>
    /// Rueckfall ohne vollstaendiges Schachtpaar: Die WinCan-Bezeichnung wird verwendet.
    /// Weil sie nur je Projekt eindeutig ist, wird eine gleichnamige, aber andere Haltung
    /// mit dem Zonennamen getrennt.
    /// </summary>
    private static string BestimmeRueckfallname(
        Project project,
        WinCanDbSection section,
        Dictionary<string, string> nodeKeyByPk,
        string? zonenName,
        List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(zonenName))
            return section.Key;

        var vorhanden = FindRecord(project, section.Key);
        if (vorhanden is null || IstSelbeHaltung(vorhanden, section, nodeKeyByPk))
            return section.Key;

        var kandidat = $"{section.Key} ({zonenName})";
        var lauf = 2;
        while (true)
        {
            var belegt = FindRecord(project, kandidat);
            if (belegt is null)
            {
                messages.Add(
                    $"Haltung {section.Key} kommt mehrfach vor und meint verschiedene Haltungen. "
                    + $"Getrennt angelegt als \"{kandidat}\".");
                return kandidat;
            }

            if (IstSelbeHaltung(belegt, section, nodeKeyByPk))
                return kandidat;

            kandidat = $"{section.Key} ({zonenName}-{lauf})";
            lauf++;
        }
    }

    /// <summary>
    /// Gleiche Haltung, wenn beide Schaechte uebereinstimmen. Ein noch leeres Schachtfeld
    /// am Bestandssatz gilt als vertraeglich — sonst wuerde ein zuvor aus PDF oder XTF
    /// angelegter Datensatz ohne Schaechte faelschlich verdoppelt.
    /// </summary>
    private static bool IstSelbeHaltung(
        HaltungRecord record,
        WinCanDbSection section,
        Dictionary<string, string> nodeKeyByPk)
    {
        return PasstSchacht(record.GetFieldValue("Schacht_oben"), SchachtOben(section, nodeKeyByPk))
               && PasstSchacht(record.GetFieldValue("Schacht_unten"), SchachtUnten(section, nodeKeyByPk));

        static bool PasstSchacht(string? bestand, string? neu)
            => string.IsNullOrWhiteSpace(bestand)
               || string.IsNullOrWhiteSpace(neu)
               || string.Equals(bestand.Trim(), neu.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? SchachtOben(WinCanDbSection section, Dictionary<string, string> nodeKeyByPk)
        => LeseKnoten(section.FromNodeFk, nodeKeyByPk);

    private static string? SchachtUnten(WinCanDbSection section, Dictionary<string, string> nodeKeyByPk)
        => LeseKnoten(section.ToNodeFk, nodeKeyByPk);

    private static string? LeseKnoten(string? fk, Dictionary<string, string> nodeKeyByPk)
        => !string.IsNullOrWhiteSpace(fk) && nodeKeyByPk.TryGetValue(fk, out var key) ? key : null;

    /// <summary>
    /// Kurzer Zonenname aus dem Projektordner, z. B. "Zone 2.11" aus
    /// "2.26.049 Andermatt Zone 2.11_Missionsweg_Kirchgasse". Ohne erkennbare Zone
    /// bleibt der Ordnername als Unterscheidung erhalten.
    /// </summary>
    private static string KuerzeZonenName(string projektWurzel)
    {
        var ordner = Path.GetFileName(projektWurzel.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(ordner))
            return "Projekt";

        var treffer = Regex.Match(
            ordner,
            @"Zone\s*([0-9]+(?:\.[0-9]+)*)",
            RegexOptions.IgnoreCase);

        return treffer.Success ? $"Zone {treffer.Groups[1].Value}" : ordner;
    }
}
