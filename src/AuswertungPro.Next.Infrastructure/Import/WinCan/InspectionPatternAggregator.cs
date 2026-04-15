using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Aggregiert statistische Muster aus einer Sammlung von Inspektionsprofilen.
/// Liefert Median-Kennzahlen, Code-Verteilungen, Uebergangsmatrizen und Sequenz-Regeln.
/// </summary>
public static class InspectionPatternAggregator
{
    // -------------------------------------------------------------------------
    // Oeffentliche Methoden
    // -------------------------------------------------------------------------

    /// <summary>
    /// Berechnet aggregierte Muster aus einer Liste von Inspektionsprofilen.
    /// </summary>
    public static AggregatedPatterns Aggregate(List<InspectionProfile> profiles)
    {
        if (profiles == null || profiles.Count == 0)
        {
            return new AggregatedPatterns(
                AnzahlHaltungen: 0,
                AnzahlBeobachtungen: 0,
                MedianFahrgeschwindigkeit: 0,
                MedianCodierungenProMeter: 0,
                MedianLueckeMeter: 0,
                CodeVerteilung: new Dictionary<string, double>(),
                UebergangsMatrix: new Dictionary<string, int>(),
                DistanzBisNaechsterCode: new Dictionary<string, GeschwindigkeitKontext>(),
                SequenzRegeln: new List<SequenzRegel>(),
                Aufnahmetechnik: new AufnahmetechnikMuster(0, 0, 0, 0),
                GeschwindigkeitNachKontext: new Dictionary<string, GeschwindigkeitKontext>());
        }

        int anzahlHaltungen = profiles.Count;
        int anzahlBeobachtungen = profiles.Sum(p => p.Ereignisse.Count);

        // 1. Median Fahrgeschwindigkeit
        double medianFahrgeschwindigkeit = BerechneMedianFahrgeschwindigkeit(profiles);

        // 2. Median Codierungen pro Meter
        double medianCodierungenProMeter = BerechneMedianCodierungenProMeter(profiles);

        // 3. Median Luecke in Metern
        double medianLueckeMeter = BerechneMedianLueckeMeter(profiles);

        // 4. Code-Verteilung (Prozent pro CodeMain)
        var codeVerteilung = BerechneCodeVerteilung(profiles);

        // 5. Uebergangsmatrix
        var uebergangsMatrix = BerechneUebergangsMatrix(profiles);

        // 6. Distanz bis naechster Code (Median/P10/P90 pro CodeMain)
        var distanzBisNaechsterCode = BerechneDistanzBisNaechsterCode(profiles);

        // 7. Sequenz-Regeln mit Support
        var sequenzRegeln = BerechneSequenzRegeln(profiles);

        // 8. Aufnahmetechnik-Muster
        var aufnahmetechnik = BerechneAufnahmetechnikMuster(profiles);

        // 9. Geschwindigkeit nach Kontext (Segment-Typ)
        var geschwindigkeitNachKontext = BerechneGeschwindigkeitNachKontext(profiles);

        return new AggregatedPatterns(
            AnzahlHaltungen: anzahlHaltungen,
            AnzahlBeobachtungen: anzahlBeobachtungen,
            MedianFahrgeschwindigkeit: medianFahrgeschwindigkeit,
            MedianCodierungenProMeter: medianCodierungenProMeter,
            MedianLueckeMeter: medianLueckeMeter,
            CodeVerteilung: codeVerteilung,
            UebergangsMatrix: uebergangsMatrix,
            DistanzBisNaechsterCode: distanzBisNaechsterCode,
            SequenzRegeln: sequenzRegeln,
            Aufnahmetechnik: aufnahmetechnik,
            GeschwindigkeitNachKontext: geschwindigkeitNachKontext);
    }

    /// <summary>
    /// Speichert aggregierte Muster als eingerueckte JSON-Datei.
    /// </summary>
    public static void SavePatterns(AggregatedPatterns patterns, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string json = JsonSerializer.Serialize(patterns, options);
        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath, json, System.Text.Encoding.UTF8);
    }

    // -------------------------------------------------------------------------
    // Private Berechnungsmethoden
    // -------------------------------------------------------------------------

    /// <summary>
    /// Berechnet den Median der Fahrgeschwindigkeiten ueber alle Profile (nur > 0).
    /// </summary>
    private static double BerechneMedianFahrgeschwindigkeit(List<InspectionProfile> profiles)
    {
        var werte = profiles
            .Where(p => p.Statistik.FahrgeschwindigkeitMS.HasValue &&
                        p.Statistik.FahrgeschwindigkeitMS.Value > 0)
            .Select(p => p.Statistik.FahrgeschwindigkeitMS!.Value)
            .OrderBy(v => v)
            .ToList();

        return werte.Count > 0 ? Median(werte) : 0;
    }

    /// <summary>
    /// Berechnet den Median der Codierungen pro Meter ueber alle Profile (nur > 0).
    /// </summary>
    private static double BerechneMedianCodierungenProMeter(List<InspectionProfile> profiles)
    {
        var werte = profiles
            .Where(p => p.Statistik.CodierungenProMeter.HasValue &&
                        p.Statistik.CodierungenProMeter.Value > 0)
            .Select(p => p.Statistik.CodierungenProMeter!.Value)
            .OrderBy(v => v)
            .ToList();

        return werte.Count > 0 ? Median(werte) : 0;
    }

    /// <summary>
    /// Berechnet den Median der Luecken-Distanzen in Metern ueber alle Profile (nur DistanzM > 0).
    /// </summary>
    private static double BerechneMedianLueckeMeter(List<InspectionProfile> profiles)
    {
        var werte = profiles
            .SelectMany(p => p.Luecken)
            .Where(g => g.DistanzM.HasValue && g.DistanzM.Value > 0)
            .Select(g => g.DistanzM!.Value)
            .OrderBy(v => v)
            .ToList();

        return werte.Count > 0 ? Median(werte) : 0;
    }

    /// <summary>
    /// Berechnet die relative Haeufigkeit (Prozent) jedes CodeMain ueber alle Profile.
    /// </summary>
    private static Dictionary<string, double> BerechneCodeVerteilung(List<InspectionProfile> profiles)
    {
        var alle = profiles.SelectMany(p => p.Ereignisse).ToList();
        int total = alle.Count;

        if (total == 0)
            return new Dictionary<string, double>();

        return alle
            .GroupBy(e => e.CodeMain)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(100.0 * g.Count() / total, 2));
    }

    /// <summary>
    /// Berechnet die Uebergangsmatrix: "CodeA→CodeB" → Anzahl aufeinanderfolgender Paare.
    /// </summary>
    private static Dictionary<string, int> BerechneUebergangsMatrix(List<InspectionProfile> profiles)
    {
        var matrix = new Dictionary<string, int>();

        foreach (var profile in profiles)
        {
            var events = profile.Ereignisse;
            for (int i = 0; i < events.Count - 1; i++)
            {
                string schluessel = events[i].CodeMain + "→" + events[i + 1].CodeMain;
                matrix.TryGetValue(schluessel, out int count);
                matrix[schluessel] = count + 1;
            }
        }

        return matrix;
    }

    /// <summary>
    /// Berechnet Median/P10/P90 der Distanz zum naechsten Event pro CodeMain.
    /// </summary>
    private static Dictionary<string, GeschwindigkeitKontext> BerechneDistanzBisNaechsterCode(
        List<InspectionProfile> profiles)
    {
        // CodeMain → Liste der Distanzen zum naechsten Event
        var distanzenProCode = new Dictionary<string, List<double>>();

        foreach (var profile in profiles)
        {
            var events = profile.Ereignisse;
            for (int i = 0; i < events.Count - 1; i++)
            {
                var aktuell = events[i];
                var naechster = events[i + 1];

                if (!aktuell.Meter.HasValue || !naechster.Meter.HasValue) continue;
                double distanz = naechster.Meter.Value - aktuell.Meter.Value;
                if (distanz <= 0) continue;

                if (!distanzenProCode.TryGetValue(aktuell.CodeMain, out var liste))
                {
                    liste = new List<double>();
                    distanzenProCode[aktuell.CodeMain] = liste;
                }
                liste.Add(distanz);
            }
        }

        var ergebnis = new Dictionary<string, GeschwindigkeitKontext>();

        foreach (var (code, distanzen) in distanzenProCode)
        {
            distanzen.Sort();
            ergebnis[code] = new GeschwindigkeitKontext(
                Median: Median(distanzen),
                P10: Percentile(distanzen, 10),
                P90: Percentile(distanzen, 90));
        }

        return ergebnis;
    }

    /// <summary>
    /// Berechnet Sequenz-Regeln mit Support-Wert ueber alle Profile.
    /// </summary>
    private static List<SequenzRegel> BerechneSequenzRegeln(List<InspectionProfile> profiles)
    {
        int n = profiles.Count;
        if (n == 0) return new List<SequenzRegel>();

        var regeln = new List<SequenzRegel>();

        // Regel 1: BCD ist typischerweise erster Code
        int bcdErst = profiles.Count(p =>
            p.Ereignisse.Count > 0 && p.Ereignisse[0].CodeMain == "BCD");
        int ausnahmenBcdErst = n - bcdErst;
        regeln.Add(new SequenzRegel(
            Regel: "BCD ist typischerweise erster Code",
            Support: Math.Round((double)bcdErst / n, 4),
            Ausnahmen: ausnahmenBcdErst));

        // Regel 2: BCE ist typischerweise letzter Code
        int bceLetzt = profiles.Count(p =>
            p.Ereignisse.Count > 0 && p.Ereignisse[^1].CodeMain == "BCE");
        int ausnahmenBceLetzt = n - bceLetzt;
        regeln.Add(new SequenzRegel(
            Regel: "BCE ist typischerweise letzter Code",
            Support: Math.Round((double)bceLetzt / n, 4),
            Ausnahmen: ausnahmenBceLetzt));

        // Regel 3: BDB folgt auf BCD innerhalb 2m
        int bdbNachBcd = 0;
        foreach (var profile in profiles)
        {
            var events = profile.Ereignisse;
            int bcdIdx = events.ToList().FindIndex(e => e.CodeMain == "BCD");
            if (bcdIdx < 0) continue;

            var bcdEvent = events[bcdIdx];
            if (!bcdEvent.Meter.HasValue) continue;

            // Suche BDB innerhalb 2m nach BCD
            bool gefunden = false;
            for (int i = bcdIdx + 1; i < events.Count && !gefunden; i++)
            {
                if (events[i].CodeMain != "BDB") continue;
                if (!events[i].Meter.HasValue) continue;
                double distanz = events[i].Meter!.Value - bcdEvent.Meter!.Value;
                if (distanz >= 0 && distanz <= 2.0)
                    gefunden = true;
            }
            if (gefunden) bdbNachBcd++;
        }
        int ausnahmenBdb = n - bdbNachBcd;
        regeln.Add(new SequenzRegel(
            Regel: "BDB folgt auf BCD innerhalb 2m",
            Support: Math.Round((double)bdbNachBcd / n, 4),
            Ausnahmen: ausnahmenBdb));

        return regeln;
    }

    /// <summary>
    /// Berechnet Aufnahmetechnik-Muster: Schacht-Dauer und erste Nutzcodierung.
    /// </summary>
    private static AufnahmetechnikMuster BerechneAufnahmetechnikMuster(List<InspectionProfile> profiles)
    {
        // SchachtPhase: Zeit des ersten BCD-Events pro Profil
        var schachtZeiten = new List<double>();
        foreach (var profile in profiles)
        {
            var bcdEvent = profile.Ereignisse.FirstOrDefault(e => e.CodeMain == "BCD");
            if (bcdEvent == null) continue;
            double zeitVonAnfang = bcdEvent.ZeitSek -
                (profile.Ereignisse.Count > 0 ? profile.Ereignisse[0].ZeitSek : 0);
            if (zeitVonAnfang >= 0)
                schachtZeiten.Add(bcdEvent.ZeitSek);
        }

        double schachtMedian = 0, schachtP90 = 0;
        if (schachtZeiten.Count > 0)
        {
            schachtZeiten.Sort();
            schachtMedian = Median(schachtZeiten);
            schachtP90 = Percentile(schachtZeiten, 90);
        }

        // Erste Codierung: Erster Meter eines Events das NICHT BCD/BDB ist
        var ersteCodierungMeter = new List<double>();
        foreach (var profile in profiles)
        {
            var ersteNutzcodierung = profile.Ereignisse
                .FirstOrDefault(e => e.CodeMain != "BCD" && e.CodeMain != "BDB" &&
                                     e.Meter.HasValue);
            if (ersteNutzcodierung?.Meter.HasValue == true)
                ersteCodierungMeter.Add(ersteNutzcodierung.Meter!.Value);
        }

        double ersteMeterMedian = 0, ersteMeterP90 = 0;
        if (ersteCodierungMeter.Count > 0)
        {
            ersteCodierungMeter.Sort();
            ersteMeterMedian = Median(ersteCodierungMeter);
            ersteMeterP90 = Percentile(ersteCodierungMeter, 90);
        }

        return new AufnahmetechnikMuster(
            SchachtPhaseSekMedian: Math.Round(schachtMedian, 2),
            SchachtPhaseSekP90: Math.Round(schachtP90, 2),
            ErsteCodierungMeterMedian: Math.Round(ersteMeterMedian, 2),
            ErsteCodierungMeterP90: Math.Round(ersteMeterP90, 2));
    }

    /// <summary>
    /// Berechnet Median/P10/P90 der Geschwindigkeit pro Segment-Typ.
    /// </summary>
    private static Dictionary<string, GeschwindigkeitKontext> BerechneGeschwindigkeitNachKontext(
        List<InspectionProfile> profiles)
    {
        // SegmentTyp → Liste der Geschwindigkeiten
        var geschwindigkeitenProTyp = new Dictionary<string, List<double>>();

        foreach (var profile in profiles)
        {
            foreach (var segment in profile.Segmente)
            {
                if (!segment.GeschwindigkeitMS.HasValue || segment.GeschwindigkeitMS.Value <= 0)
                    continue;

                if (!geschwindigkeitenProTyp.TryGetValue(segment.Typ, out var liste))
                {
                    liste = new List<double>();
                    geschwindigkeitenProTyp[segment.Typ] = liste;
                }
                liste.Add(segment.GeschwindigkeitMS.Value);
            }
        }

        var ergebnis = new Dictionary<string, GeschwindigkeitKontext>();

        foreach (var (typ, geschwindigkeiten) in geschwindigkeitenProTyp)
        {
            geschwindigkeiten.Sort();
            ergebnis[typ] = new GeschwindigkeitKontext(
                Median: Math.Round(Median(geschwindigkeiten), 4),
                P10: Math.Round(Percentile(geschwindigkeiten, 10), 4),
                P90: Math.Round(Percentile(geschwindigkeiten, 90), 4));
        }

        return ergebnis;
    }

    // -------------------------------------------------------------------------
    // Statistische Hilfsmethoden
    // -------------------------------------------------------------------------

    /// <summary>
    /// Berechnet den Median einer bereits sortierten Liste. Leere Liste gibt 0 zurueck.
    /// </summary>
    private static double Median(List<double> sorted)
    {
        if (sorted.Count == 0) return 0;
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    /// <summary>
    /// Berechnet ein Perzentil (0-100) einer bereits sortierten Liste.
    /// Verwendet lineare Interpolation. Leere Liste gibt 0 zurueck.
    /// </summary>
    private static double Percentile(List<double> sorted, int p)
    {
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];

        double index = (p / 100.0) * (sorted.Count - 1);
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);

        if (lower == upper) return sorted[lower];

        double fraction = index - lower;
        return sorted[lower] + fraction * (sorted[upper] - sorted[lower]);
    }
}
