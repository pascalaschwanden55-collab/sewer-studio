using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>Was die Verknüpfung eines verteilten Schachtprotokolls ergeben hat.</summary>
/// <param name="Verknuepft">Anzahl Schaechte, deren PDF-Verweis gesetzt wurde.</param>
/// <param name="Meldungen">Offene Faelle mit Herkunft — je Zeile ein Protokollteil.</param>
public sealed record SchachtVerknuepfungsErgebnis(
    int Verknuepft,
    IReadOnlyList<string> Meldungen);

/// <summary>
/// Verbindet verteilte Schachtprotokolle mit den Schaechten des Projekts.
///
/// Bis 2026-09-05 lag diese Regel allein im Export-ViewModel. Der Ein-Knopf-Import
/// brauchte sie ein zweites Mal — und eine zweite Umsetzung waere genau die Stelle, an
/// der beide Wege spaeter auseinanderlaufen.
///
/// Zwei fachliche Grenzen:
///
/// 1. <b>Es wird kein Schacht angelegt.</b> Ein Protokollteil, dessen Nummer das Projekt
///    nicht kennt, wird gemeldet — die Datei liegt im Ordner, der Verweis fehlt. Einen
///    Schacht allein aus einer PDF-Seite zu erfinden, waere fachlich nicht belegt.
/// 2. <b>Ein vorhandener Verweis wird nicht ersetzt.</b> Er kann von Hand gesetzt oder
///    aus einem eindeutigen Einzelprotokoll gekommen sein.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class SchachtProtokollVerknuepfung
{
    /// <param name="teile">
    /// Je Protokollteil: der gespeicherte Zielpfad und der Schachtordner, in dem er liegt.
    /// </param>
    /// <param name="projektWurzel">Projektstamm, um den Verweis relativ zu machen.</param>
    public static SchachtVerknuepfungsErgebnis Verknuepfe(
        IReadOnlyList<(string ZielPfad, string SchachtOrdner, string Herkunft)> teile,
        Project project,
        string? projektWurzel)
    {
        ArgumentNullException.ThrowIfNull(teile);
        ArgumentNullException.ThrowIfNull(project);

        var verknuepft = 0;
        var meldungen = new List<string>();

        foreach (var (zielPfad, schachtOrdner, herkunft) in teile)
        {
            if (string.IsNullOrWhiteSpace(zielPfad) || string.IsNullOrWhiteSpace(schachtOrdner))
                continue;

            var record = FindeSchacht(project, schachtOrdner);
            if (record is null)
            {
                meldungen.Add(
                    $"Schachtprotokoll {Dateiname(zielPfad)} aus {Dateiname(herkunft)}: "
                    + $"Schacht {Ordnername(schachtOrdner)} ist im Projekt nicht bekannt — "
                    + "die Datei liegt im Ordner, der Verweis fehlt.");
                continue;
            }

            var vorhanden = record.GetFieldValue("PDF_Path")?.Trim();
            var neu = ProjectPathResolver.MakeRelativeIfInsideProject(zielPfad, projektWurzel);
            if (!string.IsNullOrWhiteSpace(vorhanden)
                && !string.Equals(vorhanden, neu, StringComparison.OrdinalIgnoreCase))
            {
                meldungen.Add(
                    $"Schacht {Ordnername(schachtOrdner)}: hat bereits ein Protokoll; "
                    + $"{Dateiname(zielPfad)} liegt zusaetzlich im Ordner.");
                continue;
            }

            // Nur zaehlen, was sich wirklich aendert. Mehrere Teile desselben Schachts
            // mit demselben Zielpfad sind EINE Verknuepfung — sonst meldet der Bericht
            // mehr Verknuepfungen als es Schaechte mit Protokoll gibt (gemessen an
            // Goeschenen Unterdorfstrasse: 70 gemeldet, 50 tatsaechlich).
            if (string.Equals(vorhanden, neu, StringComparison.OrdinalIgnoreCase))
                continue;

            record.SetFieldValue("PDF_Path", neu);
            verknuepft++;
        }

        if (verknuepft > 0)
        {
            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;
        }

        return new SchachtVerknuepfungsErgebnis(verknuepft, meldungen);
    }

    /// <summary>
    /// Sucht den Schacht ueber den Ordnernamen — von hinten, damit auch ein Zielbaum mit
    /// Gemeinde und Jahr aufgeloest wird.
    /// </summary>
    private static SchachtRecord? FindeSchacht(Project project, string schachtOrdner)
    {
        var segmente = schachtOrdner.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = segmente.Length - 1; i >= 0; i--)
        {
            var ordnername = ProjectPathResolver.SanitizePathSegment(segmente[i]);
            var treffer = project.SchaechteData.FirstOrDefault(x =>
                string.Equals(
                    ProjectPathResolver.SanitizePathSegment((x.GetFieldValue("Schachtnummer") ?? "").Trim()),
                    ordnername,
                    StringComparison.OrdinalIgnoreCase));
            if (treffer is not null)
                return treffer;
        }

        return null;
    }

    private static string Ordnername(string pfad)
    {
        var segmente = pfad.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segmente.Length > 0 ? segmente[^1] : pfad;
    }

    private static string Dateiname(string pfad)
    {
        var trenner = pfad.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return trenner >= 0 && trenner < pfad.Length - 1 ? pfad[(trenner + 1)..] : pfad;
    }
}
