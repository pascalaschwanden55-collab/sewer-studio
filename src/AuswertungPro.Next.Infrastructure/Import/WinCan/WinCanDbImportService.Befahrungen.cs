using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Eine WinCan-Haltung kann mehrere Befahrungen tragen. Dieser Teil baut die
/// Protokollzeilen JE Befahrung und legt die weiteren Befahrungen verlustfrei ab.
///
/// Anlass (Audit 2026-09-05): Bis dahin wurde nur die neueste Untersuchung uebernommen.
/// Die uebrigen wurden gemeldet — ihre Befunde, Fotos und Videosekunden gingen aber
/// verloren. In Seilergasse waren das 12 Befunde, 9 Fotos und 1 Video bei "0 Fehler".
///
/// Herausgeloest aus <c>WinCanDbImportService.cs</c>, damit die ohnehin grosse Datei
/// nicht weiter waechst. Der Inhalt der Schleife ist unveraendert uebernommen; neu ist
/// nur, dass die gefundenen Videopfade zurueckgegeben statt sofort gesetzt werden.
/// </summary>
public sealed partial class WinCanDbImportService
{
    private List<ProtocolEntry> BaueBefahrungsEintraege(
        IReadOnlyList<WinCanDbObservation> obsList,
        IReadOnlyDictionary<string, List<WinCanDbMedia>> mediaByObs,
        Dictionary<string, List<string>> fileIndex,
        string sectionKey,
        List<string> messages,
        out List<string> videoPfade,
        out int medienfehler)
    {
        medienfehler = 0;
        videoPfade = new List<string>();
            var entries = new List<ProtocolEntry>();
            foreach (var obs in obsList.OrderBy(o => o.SortOrder))
            {
                // Wurzel-Fix: rohen WinCan-OpCode normalisieren (Punkt-Trenner und Meter-Suffixe
                // entfernen, Hauptcode + Laenge gegen Katalog pruefen), damit typischer Parsing-Muell
                // nicht ins Protokoll und spaeter ins Training gelangt. CodeMeta.Code erbt entry.Code.
                var rawCode = obs.OpCode ?? "";
                var normalizedCode = VsaCodeValidator.TryNormalizeKnownCode(rawCode) ?? "";
                if (normalizedCode.Length == 0 && !string.IsNullOrWhiteSpace(rawCode))
                    messages.Add($"WinCan: Code '{rawCode}' unbekannt/ungueltig - leer uebernommen (Haltung {sectionKey}).");

                var entry = new ProtocolEntry
                {
                    Code = normalizedCode,
                    Beschreibung = obs.Observation ?? "",
                    MeterStart = obs.Distance,
                    MeterEnd = obs.Distance.HasValue && obs.ContDefectLength.HasValue && obs.ContDefectLength.Value > 0
                        ? obs.Distance.Value + obs.ContDefectLength.Value
                        : obs.Distance,
                    IsStreckenschaden = obs.ContDefectLength.HasValue && obs.ContDefectLength.Value > 0,
                    Mpeg = obs.TimeCtr,
                    Zeit = ParseTimeSpan(obs.TimeCtr),
                    Source = ProtocolEntrySource.Imported
                };

                var parameters = BuildObsParameters(obs);
                if (parameters.Count > 0)
                {
                    entry.CodeMeta = new ProtocolEntryCodeMeta
                    {
                        Code = entry.Code,
                        Parameters = parameters,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                }

                if (mediaByObs.TryGetValue(obs.Pk, out var mediaList))
                {
                    foreach (var media in mediaList)
                    {
                        if (string.IsNullOrWhiteSpace(media.FileName))
                            continue;

                        // Ein leerer Medientyp in der Datenbank darf eine vorhandene
                        // Datei nicht verwerfen — dann entscheidet die Dateiendung.
                        var medientyp = WinCanValueNormalizer.MedientypOderEndung(
                            media.FileType, media.FileName);

                        if (IsVideo(medientyp))
                        {
                            var videoPath = ResolveFile(fileIndex, media.FileName);
                            if (!string.IsNullOrWhiteSpace(videoPath))
                                videoPfade.Add(videoPath!);
                            else
                            {
                                medienfehler++;
                                messages.Add($"Haltung {sectionKey}: Video fehlt oder ist nicht eindeutig: {media.FileName}");
                            }
                        }
                        else if (IsImage(medientyp))
                        {
                            var photoPath = ResolveFile(fileIndex, media.FileName);
                            if (!string.IsNullOrWhiteSpace(photoPath))
                                entry.FotoPaths.Add(photoPath);
                            else
                            {
                                medienfehler++;
                                messages.Add($"Haltung {sectionKey}: Foto fehlt oder ist nicht eindeutig: {media.FileName}");
                            }
                        }
                    }
                }

                entries.Add(entry);
            }

        return entries;
    }

    /// <summary>
    /// Legt die weiteren Befahrungen einer Haltung ab und vergibt die Videorollen.
    ///
    /// Die uebernommene Befahrung bleibt Arbeitskopie; jede weitere kommt als eigene
    /// Revision in die Historie. So geht nichts verloren, und niemand muss raten, welche
    /// Befahrung "die richtige" ist.
    ///
    /// Die Rolle der Videos bestimmt <see cref="Befahrungsrollen"/> aus der
    /// Kamerarichtung (<c>INS_InspectionDir</c>) und der Dateinamenskonvention — nie aus
    /// der Reihenfolge. Nur eine belegte Gegenbefahrung landet in <c>Link_G</c>.
    /// </summary>
    /// <returns>Anzahl Faelle, die eine menschliche Entscheidung brauchen.</returns>
    private int UebernehmeWeitereBefahrungen(
        HaltungRecord record,
        string haltungsname,
        WinCanDbInspection? uebernommen,
        IReadOnlyList<WinCanDbInspection> kandidaten,
        IReadOnlyDictionary<string, List<WinCanDbObservation>> obsByInspection,
        IReadOnlyDictionary<string, List<WinCanDbMedia>> mediaByObs,
        Dictionary<string, List<string>> fileIndex,
        string sectionKey,
        List<string> videoPfadeDerUebernommenen,
        List<string> messages,
        out int medienfehler)
    {
        medienfehler = 0;
        var offen = 0;
        var alleVideos = new List<(string Pfad, WinCanDbInspection Untersuchung)>();

        if (record.Protocol?.Current is { } current)
        {
            current.ImportVideoPaths = videoPfadeDerUebernommenen.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (uebernommen is not null && obsByInspection.TryGetValue(uebernommen.Pk, out var aktuelleBeobachtungen))
                current.ImportFingerprint = BefahrungsFingerabdruck(sectionKey, uebernommen, aktuelleBeobachtungen, mediaByObs);
        }

        foreach (var pfad in videoPfadeDerUebernommenen.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (uebernommen is not null)
                alleVideos.Add((pfad, uebernommen));
        }

        foreach (var weitere in kandidaten.Where(k => uebernommen is null || k.Pk != uebernommen.Pk))
        {
            if (!obsByInspection.TryGetValue(weitere.Pk, out var obsList) || obsList.Count == 0)
                continue;

            var eintraege = BaueBefahrungsEintraege(
                obsList, mediaByObs, fileIndex, sectionKey, messages, out var videos, out var weitereMedienfehler);
            medienfehler += weitereMedienfehler;

            foreach (var pfad in videos.Distinct(StringComparer.OrdinalIgnoreCase))
                alleVideos.Add((pfad, weitere));

            if (eintraege.Count == 0)
                continue;

            record.Protocol ??= new ProtocolDocument { HaltungId = haltungsname };
            record.Protocol.History ??= new List<ProtocolRevision>();
            var fingerprint = BefahrungsFingerabdruck(sectionKey, weitere, obsList, mediaByObs);
            if (new[] { record.Protocol.Original, record.Protocol.Current }.Concat(record.Protocol.History)
                .Any(r => r.ImportFingerprint == fingerprint))
                continue;
            record.Protocol.History.Add(new ProtocolRevision
            {
                ImportFingerprint = fingerprint,
                Comment = $"Weitere WinCan-Befahrung vom {Datumstext(weitere)}",
                ImportVideoPaths = videos.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Entries = eintraege.Select(ProtocolEntryCloner.CloneLegacyProtocolEntry).ToList()
            });

            offen++;
            messages.Add(
                $"Haltung {sectionKey}: weitere Befahrung vom {Datumstext(weitere)} mit "
                + $"{eintraege.Count} Befunden als Revision abgelegt.");
        }

        // Automatische Altverweise dürfen nicht beim neuen Protokoll stehen bleiben.
        // Handgesetzte Verweise schützt der Setter weiterhin.
        record.SetFieldValue(FieldKeys.Link, "", FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("Link_G", "", FieldSource.Legacy, userEdited: false);
        if (alleVideos.Count == 0)
            return offen;

        var inhalte = new Common.MedienInhaltsIndex().Pruefe(alleVideos.Select(v => v.Pfad).ToList())
            .ToDictionary(v => v.Pfad, StringComparer.OrdinalIgnoreCase);
        var belege = alleVideos
            .Select(v => new BefahrungsBeleg(v.Pfad, Kamerarichtung: LiesRichtung(v.Untersuchung),
                Inhaltsschluessel: inhalte[v.Pfad].Inhaltsschluessel,
                IstAktiveUntersuchung: v.Untersuchung.Pk == uebernommen?.Pk))
            .ToList();
        var rollen = Befahrungsrollen.Ordne(haltungsname, belege);

        var haupt = rollen.FirstOrDefault(r => r.Rolle == Befahrungsrolle.Hauptbefahrung);
        var gegen = rollen.FirstOrDefault(r => r.Rolle == Befahrungsrolle.Gegenbefahrung);

        if (haupt is not null && videoPfadeDerUebernommenen.Contains(haupt.Pfad, StringComparer.OrdinalIgnoreCase))
            record.SetFieldValue(FieldKeys.Link, haupt.Pfad, FieldSource.Legacy, userEdited: false);
        else if (videoPfadeDerUebernommenen.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
        {
            // Genau ein Video gehört laut Datenbank zur aktiven Untersuchung.
            record.SetFieldValue(
                FieldKeys.Link, videoPfadeDerUebernommenen[^1], FieldSource.Legacy, userEdited: false);
        }

        if (gegen is not null && !string.Equals(gegen.Pfad, record.GetFieldValue(FieldKeys.Link), StringComparison.OrdinalIgnoreCase))
        {
            record.SetFieldValue("Link_G", gegen.Pfad, FieldSource.Legacy, userEdited: false);
            messages.Add($"Haltung {sectionKey}: Gegenbefahrung belegt ({gegen.Grund}).");
        }

        foreach (var offenesVideo in rollen.Where(r => r.Rolle == Befahrungsrolle.Ungeklaert))
        {
            offen++;
            messages.Add(
                $"Video {sectionKey}: Rolle ungeklaert fuer {Path.GetFileName(offenesVideo.Pfad)} "
                + $"— {offenesVideo.Grund}");
        }

        return offen;
    }

    private static string BefahrungsFingerabdruck(string haltung, WinCanDbInspection untersuchung,
        IReadOnlyList<WinCanDbObservation> beobachtungen, IReadOnlyDictionary<string, List<WinCanDbMedia>> medien)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
            {
                Haltung = haltung, Untersuchung = untersuchung,
                Beobachtungen = beobachtungen.OrderBy(b => b.Pk, StringComparer.Ordinal),
                Medien = beobachtungen.OrderBy(b => b.Pk, StringComparer.Ordinal)
                    .Select(b => medien.TryGetValue(b.Pk, out var m) ? m : new List<WinCanDbMedia>())
            })));

    /// <summary>
    /// Die Kamerarichtung als Klartext. WinCan schreibt sie als Kuerzel
    /// (<c>U</c>/<c>UP</c>/<c>UPSTREAM</c>/<c>2</c> = gegen die Fliessrichtung); dieselbe
    /// Karte verwendet auch <c>M150ValueExtractor.ShouldReverseWinCanDirection</c>.
    /// Ein unbekanntes Kuerzel bleibt bewusst ohne Aussage.
    /// </summary>
    private static string? LiesRichtung(WinCanDbInspection inspection)
    {
        var wert = (inspection.InspectionDir ?? "").Trim();
        if (wert.Length == 0)
            return null;

        if (wert.Equals("U", StringComparison.OrdinalIgnoreCase)
            || wert.Equals("UP", StringComparison.OrdinalIgnoreCase)
            || wert.Equals("UPSTREAM", StringComparison.OrdinalIgnoreCase)
            || wert.Equals("2", StringComparison.Ordinal))
        {
            return "gegen_Fliessrichtung";
        }

        if (wert.Equals("D", StringComparison.OrdinalIgnoreCase)
            || wert.Equals("DOWN", StringComparison.OrdinalIgnoreCase)
            || wert.Equals("DOWNSTREAM", StringComparison.OrdinalIgnoreCase)
            || wert.Equals("1", StringComparison.Ordinal))
        {
            return "in_Fliessrichtung";
        }

        return null;
    }
}
