using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Rohdaten-Zeile aus SECOBS, bevor sie zu ProfileEvent umgewandelt wird.
/// </summary>
public sealed record RawEvent(
    string OpCode,
    string? Char1,
    string? Char2,
    double? Distance,
    double? TimeSek,
    string? ClockPos1,
    string? ClockPos2,
    string? Q1Value,
    double? ContDefectLength,
    string? Observation,
    int SortOrder);

/// <summary>
/// Extrahiert Inspektions-Profile aus WinCan DB3-Dateien (SQLite).
/// Alle oeffentlichen Methoden sind statisch fuer direkte Testbarkeit.
/// </summary>
public static class InspectionProfileExtractor
{
    // -------------------------------------------------------------------------
    // Hilfsmethoden
    // -------------------------------------------------------------------------

    /// <summary>
    /// Liest einen double-Wert sicher aus einem SqliteDataReader.
    /// Faellt auf String-Parsing zurueck falls GetDouble fehlschlaegt.
    /// </summary>
    private static double? SafeReadDouble(SqliteDataReader r, int idx)
    {
        if (r.IsDBNull(idx)) return null;
        try { return r.GetDouble(idx); }
        catch
        {
            try { return double.Parse(r.GetString(idx), CultureInfo.InvariantCulture); }
            catch { return null; }
        }
    }

    /// <summary>
    /// Liest einen String sicher aus einem SqliteDataReader (null wenn DBNull).
    /// </summary>
    private static string? SafeReadString(SqliteDataReader r, int idx)
    {
        if (r.IsDBNull(idx)) return null;
        var s = r.GetString(idx).Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    // -------------------------------------------------------------------------
    // Oeffentliche Kernmethoden (alle static fuer Unit-Tests)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parst "HH:MM:SS.ff" (WinCan OBS_TimeCtr) in Sekunden.
    /// Gibt null zurueck bei leerem oder ungueltigem Wert.
    /// </summary>
    public static double? ParseTimeCtr(string? timeCtr)
    {
        if (string.IsNullOrWhiteSpace(timeCtr)) return null;

        // Format: HH:MM:SS.ff  (ff = Hundertstel)
        var parts = timeCtr.Trim().Split(':');
        if (parts.Length != 3) return null;

        try
        {
            double h = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double m = double.Parse(parts[1], CultureInfo.InvariantCulture);
            // Sekunden koennen Dezimalstelle enthalten
            double s = double.Parse(parts[2], CultureInfo.InvariantCulture);
            return h * 3600 + m * 60 + s;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Baut den kanonischen Code aus OpCode, Char1 und Char2.
    /// CodeMain = nur OpCode (z.B. "BAB").
    /// CodeFull = OpCode + Char1 + Char2 ohne null-Teile (z.B. "BABBA").
    /// </summary>
    public static (string codeMain, string codeFull) BuildCanonicalCode(
        string opCode, string? char1, string? char2)
    {
        var codeMain = opCode.Trim().ToUpperInvariant();
        var full = codeMain;
        if (!string.IsNullOrWhiteSpace(char1))
            full += char1.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(char2))
            full += char2.Trim().ToUpperInvariant();
        return (codeMain, full);
    }

    /// <summary>
    /// Baut ein vollstaendiges InspectionProfile aus Rohdaten.
    /// Sortiert Events nach Zeit, berechnet Segmente, Luecken und Statistik.
    /// </summary>
    public static InspectionProfile BuildProfile(
        string haltungKey,
        double? laengeM,
        List<RawEvent> rawEvents,
        string? videoPfad,
        double videoConfidence)
    {
        // Events sortieren: primaer nach Zeit, sekundaer nach SortOrder
        var sorted = rawEvents
            .OrderBy(e => e.TimeSek ?? 0)
            .ThenBy(e => e.SortOrder)
            .ToList();

        // RawEvents zu ProfileEvents umwandeln
        var events = sorted.Select(e =>
        {
            var (codeMain, codeFull) = BuildCanonicalCode(e.OpCode, e.Char1, e.Char2);
            return new ProfileEvent(
                ZeitSek: e.TimeSek ?? 0,
                Meter: e.Distance,
                CodeMain: codeMain,
                CodeFull: codeFull,
                Char1: e.Char1,
                Char2: e.Char2,
                Uhr1: e.ClockPos1,
                Uhr2: e.ClockPos2,
                Q1: e.Q1Value,
                Streckenlaenge: e.ContDefectLength,
                Bemerkung: e.Observation);
        }).ToList();

        // Gesamtdauer ermitteln (letzte - erste Zeit)
        double dauerSek = events.Count > 1
            ? events[^1].ZeitSek - events[0].ZeitSek
            : 0;

        // Luecken zwischen Events berechnen
        var luecken = BuildGaps(events);

        // Segmente klassifizieren
        var segmente = BuildSegments(events);

        // Qualitaetspruefung
        var flags = BuildQualityFlags(events, videoPfad, laengeM);

        // Statistik berechnen
        var statistik = BuildStatistik(events, luecken, laengeM);

        return new InspectionProfile(
            HaltungKey: haltungKey,
            LaengeM: laengeM,
            DauerSekunden: dauerSek,
            VideoPfad: videoPfad,
            VideoMatchConfidence: videoConfidence,
            Ereignisse: events.AsReadOnly(),
            Segmente: segmente.AsReadOnly(),
            Luecken: luecken.AsReadOnly(),
            Statistik: statistik,
            QualityFlags: flags);
    }

    /// <summary>
    /// Berechnet Luecken (zeitliche Pausen ohne Beobachtung) zwischen Events.
    /// Schwellwert: > 5 Sekunden ohne Codierung gilt als Luecke.
    /// </summary>
    private static List<ProfileGap> BuildGaps(List<ProfileEvent> events)
    {
        const double minLueckeSek = 5.0;
        var gaps = new List<ProfileGap>();

        for (int i = 1; i < events.Count; i++)
        {
            var prev = events[i - 1];
            var curr = events[i];
            double deltaT = curr.ZeitSek - prev.ZeitSek;

            if (deltaT < minLueckeSek) continue;

            double? distanzM = null;
            if (prev.Meter.HasValue && curr.Meter.HasValue)
                distanzM = curr.Meter.Value - prev.Meter.Value;

            gaps.Add(new ProfileGap(
                VonZeit: prev.ZeitSek,
                BisZeit: curr.ZeitSek,
                VonMeter: prev.Meter,
                BisMeter: curr.Meter,
                DauerSek: deltaT,
                DistanzM: distanzM));
        }

        return gaps;
    }

    /// <summary>
    /// Klassifiziert Segmente zwischen aufeinanderfolgenden Events nach Heuristiken:
    /// - "schacht"          → vor dem ersten BCD-Event
    /// - "uebergang"        → 2s-Fenster um BCD oder BCE
    /// - "detailbetrachtung"→ delta_meter &lt; 0.05m AND delta_time > 4s AND danach weiterer Event
    /// - "mehrfachcodierung"→ delta_meter &lt; 0.05m AND mehrere Events an gleicher Stelle
    /// - "stillstand"       → delta_meter &lt; 0.05m AND delta_time > 2s
    /// - "axial_fahrt"      → normaler Fahrtabschnitt (Standardfall)
    /// </summary>
    public static List<ProfileSegment> BuildSegments(List<ProfileEvent> events)
    {
        var segmente = new List<ProfileSegment>();
        if (events.Count == 0) return segmente;

        // Index des ersten BCD-Events ermitteln
        int bcdIdx = events.FindIndex(e => e.CodeMain == "BCD");
        int bceIdx = events.FindIndex(e => e.CodeMain == "BCE");

        for (int i = 0; i < events.Count - 1; i++)
        {
            var von = events[i];
            var bis = events[i + 1];

            double deltaT = bis.ZeitSek - von.ZeitSek;
            double? deltaM = (von.Meter.HasValue && bis.Meter.HasValue)
                ? Math.Abs(bis.Meter.Value - von.Meter.Value)
                : null;

            string typ;
            double konfidenz;

            // Vor erstem BCD → Schachtphase
            if (bcdIdx < 0 || i < bcdIdx)
            {
                typ = "schacht";
                konfidenz = 0.85;
            }
            // 2s-Fenster um BCD oder BCE
            else if ((bcdIdx >= 0 && (i == bcdIdx || i == bcdIdx - 1)) ||
                     (bceIdx >= 0 && (i == bceIdx || i == bceIdx - 1)))
            {
                typ = "uebergang";
                konfidenz = 0.90;
            }
            // Kamera bewegt sich kaum (delta < 5cm)
            else if (deltaM.HasValue && deltaM.Value < 0.05)
            {
                if (deltaT > 4.0)
                {
                    // Lang stillgestanden mit nachfolgendem Event → Detailbetrachtung
                    typ = "detailbetrachtung";
                    konfidenz = 0.80;
                }
                else
                {
                    // Mehrere Events an gleicher Stelle (sehr kurze Zeit)
                    // Oder kurzer Stillstand
                    bool mehrfach = i + 2 < events.Count &&
                                    events[i + 2].Meter.HasValue &&
                                    Math.Abs(events[i + 2].Meter!.Value - (von.Meter ?? 0)) < 0.05;
                    if (mehrfach)
                    {
                        typ = "mehrfachcodierung";
                        konfidenz = 0.75;
                    }
                    else if (deltaT > 2.0)
                    {
                        typ = "stillstand";
                        konfidenz = 0.70;
                    }
                    else
                    {
                        typ = "axial_fahrt";
                        konfidenz = 0.60;
                    }
                }
            }
            else
            {
                typ = "axial_fahrt";
                konfidenz = 0.90;
            }

            // Geschwindigkeit berechnen falls moeglich
            double? geschwindigkeit = null;
            if (deltaM.HasValue && deltaT > 0)
                geschwindigkeit = deltaM.Value / deltaT;

            segmente.Add(new ProfileSegment(
                Typ: typ,
                VonZeit: von.ZeitSek,
                BisZeit: bis.ZeitSek,
                VonMeter: von.Meter,
                BisMeter: bis.Meter,
                DauerSek: deltaT,
                DistanzM: deltaM,
                GeschwindigkeitMS: geschwindigkeit,
                Quelle: "heuristik",
                Konfidenz: konfidenz,
                ReferenzEventIndices: [i, i + 1]));
        }

        return segmente;
    }

    /// <summary>
    /// Prueft die Qualitaet eines Inspektionsprofils und setzt Warnungs-Flags.
    /// </summary>
    public static QualityFlags BuildQualityFlags(
        List<ProfileEvent> events,
        string? videoPfad,
        double? laenge)
    {
        var warnings = new List<string>();

        bool missingVideo = string.IsNullOrWhiteSpace(videoPfad);
        bool missingSectionLength = !laenge.HasValue || laenge.Value <= 0;

        // Monotonie der Distanz pruefen
        bool nonMonotonicDistance = false;
        for (int i = 1; i < events.Count; i++)
        {
            if (!events[i].Meter.HasValue || !events[i - 1].Meter.HasValue) continue;
            if (events[i].Meter!.Value < events[i - 1].Meter!.Value - 0.01)
            {
                nonMonotonicDistance = true;
                warnings.Add($"Nicht-monotone Distanz bei Event {i}: {events[i - 1].Meter:F2}m -> {events[i].Meter:F2}m");
                break;
            }
        }

        // Monotonie der Zeit pruefen
        bool nonMonotonicTime = false;
        for (int i = 1; i < events.Count; i++)
        {
            if (events[i].ZeitSek < events[i - 1].ZeitSek - 0.001)
            {
                nonMonotonicTime = true;
                warnings.Add($"Nicht-monotone Zeit bei Event {i}: {events[i - 1].ZeitSek:F2}s -> {events[i].ZeitSek:F2}s");
                break;
            }
        }

        // Doppelte Events zur gleichen Zeit
        bool duplicateEventsSameTime = events
            .GroupBy(e => Math.Round(e.ZeitSek, 2))
            .Any(g => g.Count() > 1);

        if (duplicateEventsSameTime)
            warnings.Add("Mehrere Events mit gleicher Zeit (Doppel-Codierung)");

        // BCD vorhanden?
        bool missingBcd = !events.Any(e => e.CodeMain == "BCD");
        if (missingBcd)
            warnings.Add("Kein BCD-Event (Rohranfang) gefunden");

        // BCE vorhanden?
        bool missingBce = !events.Any(e => e.CodeMain == "BCE");
        if (missingBce)
            warnings.Add("Kein BCE-Event (Rohrende) gefunden");

        // Wenige Events
        bool fewEvents = events.Count < 3;
        if (fewEvents)
            warnings.Add($"Sehr wenige Events ({events.Count}) — Inspektion moeglicherweise unvollstaendig");

        if (missingVideo)
            warnings.Add("Kein Video-Pfad vorhanden");

        if (missingSectionLength)
            warnings.Add("Haltungslaenge fehlt oder ist 0");

        return new QualityFlags(
            MissingVideo: missingVideo,
            MissingSectionLength: missingSectionLength,
            NonMonotonicDistance: nonMonotonicDistance,
            NonMonotonicTime: nonMonotonicTime,
            DuplicateEventsSameTime: duplicateEventsSameTime,
            MissingBcd: missingBcd,
            MissingBce: missingBce,
            AmbiguousVideoMatch: false, // wird vom VideoResolver gesetzt
            FewEvents: fewEvents,
            Warnings: warnings);
    }

    /// <summary>
    /// Berechnet Statistikkennzahlen fuer ein Inspektionsprofil.
    /// </summary>
    private static ProfileStatistik BuildStatistik(
        List<ProfileEvent> events,
        List<ProfileGap> luecken,
        double? laengeM)
    {
        // Codierungen pro Meter
        double? codierungenProMeter = null;
        if (laengeM.HasValue && laengeM.Value > 0)
            codierungenProMeter = events.Count / laengeM.Value;

        // Mittlere Luecke in Sekunden
        double mittlereLueckeSek = luecken.Count > 0
            ? luecken.Average(g => g.DauerSek)
            : 0;

        // Mittlere Luecke in Metern
        double? mittlereLueckeM = null;
        var lueckenMitDistanz = luecken.Where(g => g.DistanzM.HasValue).ToList();
        if (lueckenMitDistanz.Count > 0)
            mittlereLueckeM = lueckenMitDistanz.Average(g => g.DistanzM!.Value);

        // Fahrgeschwindigkeit: Median ueber Abschnitte mit messbarer Distanz und Zeit
        double? fahrgeschwindigkeitMS = null;
        var geschwindigkeiten = new List<double>();

        for (int i = 1; i < events.Count; i++)
        {
            var prev = events[i - 1];
            var curr = events[i];
            if (!prev.Meter.HasValue || !curr.Meter.HasValue) continue;
            double deltaM = curr.Meter.Value - prev.Meter.Value;
            double deltaT = curr.ZeitSek - prev.ZeitSek;
            if (deltaM > 0.05 && deltaT > 0)
                geschwindigkeiten.Add(deltaM / deltaT);
        }

        if (geschwindigkeiten.Count > 0)
        {
            geschwindigkeiten.Sort();
            int mid = geschwindigkeiten.Count / 2;
            fahrgeschwindigkeitMS = geschwindigkeiten.Count % 2 == 0
                ? (geschwindigkeiten[mid - 1] + geschwindigkeiten[mid]) / 2
                : geschwindigkeiten[mid];
        }

        return new ProfileStatistik(
            CodierungenProMeter: codierungenProMeter,
            MittlereLueckeSek: mittlereLueckeSek,
            MittlereLueckeM: mittlereLueckeM,
            FahrgeschwindigkeitMS: fahrgeschwindigkeitMS);
    }

    // -------------------------------------------------------------------------
    // Datenbank-Zugriff
    // -------------------------------------------------------------------------

    /// <summary>
    /// Liest alle Inspektions-Profile aus einer WinCan DB3-Datei (SQLite).
    /// Tabellen: SECTION, SECINSP, SECOBS, SECOBSMM
    /// </summary>
    public static List<InspectionProfile> ExtractFromDb3(string db3Path)
    {
        if (!File.Exists(db3Path))
            throw new FileNotFoundException($"DB3 nicht gefunden: {db3Path}");

        var connectionString = $"Data Source={db3Path};Mode=ReadOnly;";
        using var con = new SqliteConnection(connectionString);
        con.Open();

        // 1) Haltungen laden (SECTION)
        var haltungen = new Dictionary<long, (string key, double? laenge)>();
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = "SELECT OBJ_PK, OBJ_Key, OBJ_Length FROM SECTION";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long pk = reader.GetInt64(0);
                string key = reader.IsDBNull(1) ? $"Section_{pk}" : reader.GetString(1).Trim();
                double? laenge = SafeReadDouble(reader, 2);
                haltungen[pk] = (key, laenge);
            }
        }

        // 2) Inspektionen laden (SECINSP)
        var inspSectionMap = new Dictionary<long, long>(); // INS_PK → OBJ_PK (Section)
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = "SELECT INS_PK, INS_Section_FK FROM SECINSP";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;
                long insPk = reader.GetInt64(0);
                long secFk = reader.GetInt64(1);
                inspSectionMap[insPk] = secFk;
            }
        }

        // 3) Beobachtungen laden (SECOBS), geloeschte ausschliessen
        // Gruppiert nach InspectionFK
        var obsMap = new Dictionary<long, List<RawEvent>>(); // InspectionFK → Events

        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT OBS_Inspection_FK,
                       OBS_OpCode,
                       OBS_Char1,
                       OBS_Char2,
                       OBS_Distance,
                       OBS_TimeCtr,
                       OBS_ClockPos1,
                       OBS_ClockPos2,
                       OBS_Q1_Value,
                       OBS_ContDefectLength,
                       OBS_Observation,
                       OBS_SortOrder
                FROM SECOBS
                WHERE OBS_Deleted IS NULL
                ORDER BY OBS_Inspection_FK, OBS_SortOrder";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(0)) continue;
                long insFk = reader.GetInt64(0);

                string opCode = reader.IsDBNull(1) ? "UNK" : reader.GetString(1).Trim();
                string? char1 = SafeReadString(reader, 2);
                string? char2 = SafeReadString(reader, 3);
                double? distance = SafeReadDouble(reader, 4);
                string? timeCtrRaw = SafeReadString(reader, 5);
                double? timeSek = ParseTimeCtr(timeCtrRaw);
                string? clockPos1 = SafeReadString(reader, 6);
                string? clockPos2 = SafeReadString(reader, 7);
                string? q1Value = SafeReadString(reader, 8);
                double? contDefectLength = SafeReadDouble(reader, 9);
                string? observation = SafeReadString(reader, 10);
                int sortOrder = reader.IsDBNull(11) ? 0 : reader.GetInt32(11);

                if (!obsMap.ContainsKey(insFk))
                    obsMap[insFk] = [];

                obsMap[insFk].Add(new RawEvent(
                    OpCode: opCode,
                    Char1: char1,
                    Char2: char2,
                    Distance: distance,
                    TimeSek: timeSek,
                    ClockPos1: clockPos1,
                    ClockPos2: clockPos2,
                    Q1Value: q1Value,
                    ContDefectLength: contDefectLength,
                    Observation: observation,
                    SortOrder: sortOrder));
            }
        }

        // 4) Multimedia-Dateien laden (SECOBSMM), um Video-Dateinamen zu ermitteln
        // Verknuepfung: OMM_Observation_FK → SECOBS → InspectionFK
        var videoMap = new Dictionary<long, List<string>>(); // InspectionFK → Dateinamen

        // Pruefen ob SECOBSMM-Tabelle existiert
        bool secobsmmExists;
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SECOBSMM'";
            secobsmmExists = Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
        }

        if (secobsmmExists)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT o.OBS_Inspection_FK, m.OMM_FileName, m.OMM_FileType
                FROM SECOBSMM m
                INNER JOIN SECOBS o ON m.OMM_Observation_FK = o.OBS_PK
                WHERE o.OBS_Deleted IS NULL
                  AND m.OMM_FileName IS NOT NULL";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(0)) continue;
                long insFk = reader.GetInt64(0);
                string fileName = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim();
                if (string.IsNullOrEmpty(fileName)) continue;

                if (!videoMap.ContainsKey(insFk))
                    videoMap[insFk] = [];
                videoMap[insFk].Add(fileName);
            }
        }

        // 5) Profile zusammenstellen
        var profiles = new List<InspectionProfile>();

        foreach (var (insPk, secPk) in inspSectionMap)
        {
            if (!haltungen.TryGetValue(secPk, out var haltung)) continue;
            if (!obsMap.TryGetValue(insPk, out var rawEvents)) continue;
            if (rawEvents.Count == 0) continue;

            // Video-Dateinamen fuer diesen Inspektionsschluessel
            videoMap.TryGetValue(insPk, out var dbFileNames);

            // Kein VideoResolver-Aufruf hier — Pfad wird spaeter aufgeloest
            string? videoPfad = dbFileNames?.FirstOrDefault();

            var profile = BuildProfile(
                haltungKey: haltung.key,
                laengeM: haltung.laenge,
                rawEvents: rawEvents,
                videoPfad: videoPfad,
                videoConfidence: videoPfad != null ? 0.95 : 0.0);

            profiles.Add(profile);
        }

        return profiles;
    }

    /// <summary>
    /// Speichert Profile als JSON-Dateien. Pro Haltung eine Datei + _index.json.
    /// </summary>
    public static void SaveProfiles(List<InspectionProfile> profiles, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var indexEntries = new List<object>();

        foreach (var profile in profiles)
        {
            // Dateiname: Haltungsschluessel bereinigen
            string safeName = string.Concat(
                profile.HaltungKey
                    .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

            string filePath = Path.Combine(outputDir, $"{safeName}.json");
            string json = JsonSerializer.Serialize(profile, options);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

            indexEntries.Add(new
            {
                haltung_key = profile.HaltungKey,
                datei = $"{safeName}.json",
                ereignisse = profile.Ereignisse.Count,
                laenge_m = profile.LaengeM,
                quality_flags = new
                {
                    missing_video = profile.QualityFlags.MissingVideo,
                    missing_bcd = profile.QualityFlags.MissingBcd,
                    missing_bce = profile.QualityFlags.MissingBce,
                    non_monotonic_distance = profile.QualityFlags.NonMonotonicDistance,
                    few_events = profile.QualityFlags.FewEvents
                }
            });
        }

        // Index-Datei schreiben
        string indexPath = Path.Combine(outputDir, "_index.json");
        string indexJson = JsonSerializer.Serialize(indexEntries, options);
        File.WriteAllText(indexPath, indexJson, System.Text.Encoding.UTF8);
    }
}
