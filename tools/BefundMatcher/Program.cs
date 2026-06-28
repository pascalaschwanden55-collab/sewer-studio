using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Evaluation;

// ── BefundMatcher (Konsolen-Werkzeug) ───────────────────────────────────────
// Duennes CLI um den geteilten Abgleich AuswertungPro.Next.Application.Ai.Evaluation.BefundMatcher.
// Rechnet vorhandene ClassifierPilot-Reports mit der ehrlichen, gestuften Methode neu durch.
//
// Aufruf:
//   dotnet run --project tools/BefundMatcher -- --demo
//   dotnet run --project tools/BefundMatcher -- <report.json>
//   dotnet run --project tools/BefundMatcher -- <ordner-mit-classifier_pilot_*.json>

return App.Run(args);

// ─────────────────────────────────────────────────────────────────────────────

static class App
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Aufruf:");
            Console.WriteLine("  --demo                        Selbsttest mit Beispiel-Befunden");
            Console.WriteLine("  --harvest [ordner]            Trainings-Ernte: Falscher-Code-Konfusionen aggregieren");
            Console.WriteLine("  <report.json>                 ein ClassifierPilot-Report");
            Console.WriteLine("  <ordner>                      alle classifier_pilot_*.json darin");
            return 1;
        }

        if (args[0] == "--demo")
            return Demo.Run();

        if (args[0] == "--harvest")
            return Harvest.Run(args.Length > 1 ? args[1] : Path.Combine("docs", "benchmarks"));

        var path = args[0];
        var files = new List<string>();
        if (Directory.Exists(path))
            files.AddRange(Directory.GetFiles(path, "classifier_pilot_*.json").OrderBy(f => f));
        else if (File.Exists(path))
            files.Add(path);
        else
        {
            Console.WriteLine($"FEHLER: nicht gefunden: {path}");
            return 1;
        }

        if (files.Count == 0)
        {
            Console.WriteLine("Keine classifier_pilot_*.json gefunden.");
            return 1;
        }

        var total = new BefundMatchResult();
        int altTpSum = 0, altFnSum = 0, altFpSum = 0;

        foreach (var file in files)
        {
            var report = ReportReader.Read(file);
            if (report is null)
            {
                Console.WriteLine($"WARNUNG: uebersprungen (kein lesbarer Report): {Path.GetFileName(file)}");
                continue;
            }

            var outcome = BefundMatcher.Match(report.GroundTruth, report.Detections, BefundMatchOptions.Default);
            PrintCase(Path.GetFileNameWithoutExtension(file), report, outcome);

            total.Add(outcome);
            altTpSum += report.AltTp;
            altFnSum += report.AltFn;
            altFpSum += report.AltFp;
        }

        if (files.Count > 1)
        {
            Console.WriteLine();
            Console.WriteLine("════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"SUMME ueber {files.Count} Reports (Hinweis: teils dieselbe Haltung mehrfach)");
            PrintMetrics(total);
            Console.WriteLine();
            Console.WriteLine("Vergleich der Mess-Methode (Treffer-Zahl):");
            Console.WriteLine($"  ALT (±2.0 m, lose):       TP {altTpSum,3}   FN {altFnSum,3}   FP {altFpSum,3}");
            Console.WriteLine($"  NEU (gestuft, 1:1):       TP {total.Treffer.Count,3}   FN {total.Verpasst.Count,3}   FP {total.Fehlalarm.Count,3}   (+ {total.FalscherCode.Count} falscher Code)");
            Console.WriteLine("  → Die strengere Toleranz zaehlt weniger, aber ehrlichere Treffer.");
        }

        return 0;
    }

    static void PrintCase(string caseId, PilotReport report, BefundMatchResult o)
    {
        Console.WriteLine();
        Console.WriteLine("────────────────────────────────────────────────────────────────────");
        Console.WriteLine($"Fall: {caseId}");
        Console.WriteLine($"  Protokoll-Befunde (ohne BCD/BCE): {report.GroundTruth.Count(f => !BefundMatchOptions.Default.IsExcluded(f))}" +
                          $"   |   KI-Befunde (ohne BCD/BCE): {report.Detections.Count(f => !BefundMatchOptions.Default.IsExcluded(f))}");
        if (o.OhneCode > 0)
            Console.WriteLine($"  (davon {o.OhneCode} KI-Detections ohne aufgeloesten VSA-Code – echte Erkennungen, zaehlen als Fehlalarm)");

        PrintMetrics(o);

        Print("TREFFER (TP)", o.Treffer.Select(p => $"{p.Tier,-5} {p.Gt.Code,-6}@{p.Gt.MeterStart,6:F2}m  ↔  KI {p.Ki.Code,-6}@{p.Ki.MeterStart,6:F2}m   (Δ {p.Gap:F2} m)"));
        Print("FALSCHER CODE (WC)", o.FalscherCode.Select(p => $"      {p.Gt.Code,-6}@{p.Gt.MeterStart,6:F2}m  ↔  KI {p.Ki.Code,-6}@{p.Ki.MeterStart,6:F2}m   (Δ {p.Gap:F2} m)"));
        Print("VERPASST (FN)", o.Verpasst.Select(f => $"      {f.Code,-6}@{f.MeterStart,6:F2}m   {Trim(f.Label, 40)}"));
        Print("FEHLALARM (FP)", o.Fehlalarm.Select(f => $"      {(f.Code.Length == 0 ? "(leer)" : f.Code),-6}@{f.MeterStart,6:F2}m   {Trim(f.Label, 40)}"));
    }

    static void PrintMetrics(BefundMatchResult o)
    {
        Console.WriteLine($"  Treffer {o.Treffer.Count}  (gruen {o.Treffer.Count(p => p.Tier == "gruen")}, gelb {o.Treffer.Count(p => p.Tier == "gelb")})" +
                          $"   Falscher-Code {o.FalscherCode.Count}   Verpasst {o.Verpasst.Count}   Fehlalarm {o.Fehlalarm.Count}");
        Console.WriteLine($"  Praezision {o.Precision:P0}   Recall {o.Recall:P0}");
    }

    static void Print(string title, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0) return;
        Console.WriteLine($"  {title}: {list.Count}");
        foreach (var s in list) Console.WriteLine($"    {s}");
    }

    static string Trim(string? s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");
}

// ─────────────────────────────────────────────────────────────────────────────

sealed record PilotReport(List<BefundMatchFinding> GroundTruth, List<BefundMatchFinding> Detections, int AltTp, int AltFn, int AltFp);

static class ReportReader
{
    /// <summary>
    /// Liest einen ClassifierPilot-Report aus einer Datei.
    /// Delegiert an <see cref="ReadFromJson"/> nach dem Einlesen des Dateiinhalts.
    /// </summary>
    public static PilotReport? Read(string file)
    {
        try
        {
            return ReadFromJson(File.ReadAllText(file));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parst einen ClassifierPilot-Report aus einem JSON-String.
    /// Bildet ground_truth/detections/vergleich auf <see cref="BefundMatchFinding"/>-Listen ab,
    /// ohne eine Datei zu benoetigen.
    /// </summary>
    public static PilotReport? ReadFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var gt = new List<BefundMatchFinding>();
            if (root.TryGetProperty("ground_truth", out var gtArr) && gtArr.ValueKind == JsonValueKind.Array)
                foreach (var e in gtArr.EnumerateArray())
                    gt.Add(new BefundMatchFinding(Str(e, "VsaCode"), Num(e, "MeterStart"), Num(e, "MeterEnd"), Str(e, "Text")));

            var ki = new List<BefundMatchFinding>();
            if (root.TryGetProperty("detections", out var dArr) && dArr.ValueKind == JsonValueKind.Array)
                foreach (var e in dArr.EnumerateArray())
                    ki.Add(new BefundMatchFinding(Str(e, "Code"), Num(e, "MeterStart"), Num(e, "MeterEnd"), Str(e, "FindingLabel")));

            int altTp = 0, altFn = 0, altFp = 0;
            if (root.TryGetProperty("vergleich", out var v) && v.ValueKind == JsonValueKind.Object)
            {
                altTp = CountArray(v, "tp");
                altFn = CountArray(v, "fn");
                altFp = CountArray(v, "fp");
            }

            if (gt.Count == 0 && ki.Count == 0) return null;
            return new PilotReport(gt, ki, altTp, altFn, altFp);
        }
        catch
        {
            return null;
        }
    }

    static double Num(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0.0;

    static string Str(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    static int CountArray(JsonElement obj, string prop)
        => obj.TryGetProperty(prop, out var a) && a.ValueKind == JsonValueKind.Array ? a.GetArrayLength() : 0;
}

// ─────────────────────────────────────────────────────────────────────────────

static class Harvest
{
    // Trainings-Ernte: aggregiert ueber alle Reports die "Falscher Code"-Faelle
    // (richtige Stelle, falsche Codierung) und zeigt, welche Verwechslungen die KI
    // systematisch macht. Das ist die konkrete Liste, was der Klassifikator lernen muss.
    public static int Run(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Console.WriteLine($"FEHLER: Ordner nicht gefunden: {folder}");
            return 1;
        }

        var files = Directory.GetFiles(folder, "classifier_pilot_*.json").OrderBy(f => f).ToList();
        if (files.Count == 0)
        {
            Console.WriteLine($"Keine classifier_pilot_*.json in {folder}.");
            return 1;
        }

        var wrongCode = new List<WcCase>();
        var verpasst = new List<FnCase>();

        foreach (var file in files)
        {
            var report = ReportReader.Read(file);
            if (report is null) continue;
            var caseId = Path.GetFileNameWithoutExtension(file);
            var r = BefundMatcher.Match(report.GroundTruth, report.Detections, BefundMatchOptions.Default);

            foreach (var p in r.FalscherCode)
                wrongCode.Add(new WcCase(caseId, p.Gt.MeterStart, p.Gt.Code, BefundMatcher.MainCode(p.Gt.Code),
                    p.Ki.Code, BefundMatcher.MainCode(p.Ki.Code), p.Gt.Label));

            foreach (var f in r.Verpasst)
                verpasst.Add(new FnCase(caseId, f.MeterStart, f.Code, BefundMatcher.MainCode(f.Code), f.Label));
        }

        Console.WriteLine("=== TRAININGS-ERNTE: Falscher Code (richtige Stelle, falsche Codierung) ===");
        Console.WriteLine($"Reports: {files.Count} | Falscher-Code-Faelle: {wrongCode.Count} | Verpasst: {verpasst.Count}");
        Console.WriteLine();

        var confusion = wrongCode
            .GroupBy(w => (w.GtMain, w.KiMain))
            .OrderByDescending(g => g.Count())
            .ToList();

        Console.WriteLine("KONFUSIONEN (KI vergibt ___ statt richtig ___):");
        foreach (var g in confusion)
            Console.WriteLine($"  richtig {g.Key.GtMain,-5} → KI {(string.IsNullOrEmpty(g.Key.KiMain) ? "(leer)" : g.Key.KiMain),-6} {g.Count(),3}×");

        Console.WriteLine();
        Console.WriteLine("DETAIL (Korrektur = der richtige Code an dieser Stelle):");
        foreach (var w in wrongCode.OrderBy(w => w.GtMain).ThenBy(w => w.Report).ThenBy(w => w.Meter))
            Console.WriteLine($"  {w.GtCode,-6}@{w.Meter,6:F2}m  KI: {(string.IsNullOrEmpty(w.KiCode) ? "(leer)" : w.KiCode),-6}  {Trim(w.GtText, 38)}");

        Console.WriteLine();
        Console.WriteLine("VERPASST (Protokoll-Code, von KI gar nicht gefunden) nach Hauptcode:");
        foreach (var g in verpasst.GroupBy(v => v.GtMain).OrderByDescending(g => g.Count()))
            Console.WriteLine($"  {g.Key,-5} {g.Count(),3}×");

        var outPath = Path.Combine(folder, "falscher_code_harvest.json");
        var payload = new
        {
            reports = files.Count,
            falscher_code_total = wrongCode.Count,
            verpasst_total = verpasst.Count,
            konfusionen = confusion.Select(g => new { richtig = g.Key.GtMain, ki = g.Key.KiMain, anzahl = g.Count() }),
            faelle = wrongCode.Select(w => new { w.Report, meter = w.Meter, richtig = w.GtCode, ki = w.KiCode, text = w.GtText }),
            verpasst = verpasst.Select(v => new { v.Report, meter = v.Meter, code = v.GtCode, text = v.GtText }),
        };
        File.WriteAllText(outPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine();
        Console.WriteLine($"Gespeichert: {outPath}");
        return 0;
    }

    static string Trim(string? s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");

    sealed record WcCase(string Report, double Meter, string GtCode, string GtMain, string KiCode, string KiMain, string GtText);
    sealed record FnCase(string Report, double Meter, string GtCode, string GtMain, string GtText);
}

static class Demo
{
    public static int Run()
    {
        // Bewusst konstruierter Fall, der alle vier Toepfe UND die Staerke der
        // globalen Zuordnung zeigt:
        //   - Zwei Risse (BAB) bei 2.00 und 2.10; KI hat BAB bei 2.02 und bei 1.55.
        //     "Naechster gewinnt" wuerde 2.02 an den 2.00er-Riss geben und den
        //     2.10er-Riss leer ausgehen lassen (nur 1 Treffer). Die globale
        //     Zuordnung schiebt um: 2.00↔1.55 (gelb) und 2.10↔2.02 (gruen) = 2 Treffer.
        var gt = new List<BefundMatchFinding>
        {
            new("BCD",   0.00, 0.00, "Rohranfang"),      // Anker → ignoriert
            new("BAB",   2.00, 2.00, "Riss A"),
            new("BAB",   2.10, 2.10, "Riss B"),
            new("BAC",   5.00, 5.00, "Bruch"),           // wird Falscher-Code-Partner
            new("BBA",   7.00, 7.00, "Wurzeln"),         // Verpasst (kein KI)
        };

        var ki = new List<BefundMatchFinding>
        {
            new("BCD",   0.00, 0.00, "pipe start"),      // Anker → ignoriert
            new("BAB",   2.02, 2.02, "crack X"),
            new("BAB",   1.55, 1.55, "crack Y"),
            new("BAB",   5.05, 5.05, "crack near break"),// andere Familie als BAC → Falscher Code
            new("BBC",   9.00, 9.00, "deposit"),         // Fehlalarm
            new("",      3.00, 3.00, "root ball"),       // ohne Code → echte Erkennung, zaehlt als Fehlalarm
        };

        var o = BefundMatcher.Match(gt, ki, BefundMatchOptions.Default);

        Console.WriteLine("=== DEMO / Selbsttest ===");
        Console.WriteLine();
        Console.WriteLine($"Treffer (TP):       {o.Treffer.Count}   (gruen {o.Treffer.Count(p => p.Tier == "gruen")}, gelb {o.Treffer.Count(p => p.Tier == "gelb")})");
        foreach (var p in o.Treffer)
            Console.WriteLine($"   {p.Tier,-5} {p.Gt.Code}@{p.Gt.MeterStart:F2}  ↔  KI {p.Ki.Code}@{p.Ki.MeterStart:F2}  (Δ {p.Gap:F2} m)");
        Console.WriteLine($"Falscher Code (WC): {o.FalscherCode.Count}");
        foreach (var p in o.FalscherCode)
            Console.WriteLine($"         {p.Gt.Code}@{p.Gt.MeterStart:F2}  ↔  KI {p.Ki.Code}@{p.Ki.MeterStart:F2}  (Δ {p.Gap:F2} m)");
        Console.WriteLine($"Verpasst (FN):      {o.Verpasst.Count}   [{string.Join(", ", o.Verpasst.Select(f => $"{f.Code}@{f.MeterStart:F2}"))}]");
        Console.WriteLine($"Fehlalarm (FP):     {o.Fehlalarm.Count}   [{string.Join(", ", o.Fehlalarm.Select(f => $"{(f.Code.Length == 0 ? "(ohne Code)" : f.Code)}@{f.MeterStart:F2}"))}]   (davon {o.OhneCode} ohne Code)");
        Console.WriteLine($"Ignoriert (Anker):  {o.IgnoriertAnker} (BCD/BCE)");
        Console.WriteLine($"Praezision {o.Precision:P0}   Recall {o.Recall:P0}");
        Console.WriteLine();

        // ── Selbsttest: erwartete Werte hart pruefen ──
        var checks = new (string Name, bool Ok)[]
        {
            ("2 Treffer (globale Zuordnung schiebt um)", o.Treffer.Count == 2),
            ("davon 1 gruen + 1 gelb",                   o.Treffer.Count(p => p.Tier == "gruen") == 1 && o.Treffer.Count(p => p.Tier == "gelb") == 1),
            ("1 Falscher Code (BAC↔BAB @5)",             o.FalscherCode.Count == 1 && o.FalscherCode[0].Gt.Code == "BAC"),
            ("1 Verpasst (BBA@7)",                       o.Verpasst.Count == 1 && o.Verpasst[0].Code == "BBA"),
            ("2 Fehlalarm (BBC@9 + ohne-Code@3)",        o.Fehlalarm.Count == 2),
            ("davon 1 ohne Code, als FP gezaehlt",       o.OhneCode == 1 && o.Fehlalarm.Any(f => f.Code.Length == 0)),
            ("2 Anker (BCD/BCE) ignoriert",              o.IgnoriertAnker == 2),
            ("Praezision = 40% (2/5)",                   Math.Abs(o.Precision - 0.4) < 1e-9),
            ("Recall = 50% (2/4)",                       Math.Abs(o.Recall - 0.5) < 1e-9),
        };

        var allOk = true;
        foreach (var (name, ok) in checks)
        {
            Console.WriteLine($"   [{(ok ? "OK " : "FEHL")}] {name}");
            allOk &= ok;
        }

        Console.WriteLine();
        Console.WriteLine(allOk ? "ERGEBNIS: alle Selbsttests bestanden ✓" : "ERGEBNIS: SELBSTTEST FEHLGESCHLAGEN ✗");
        return allOk ? 0 : 2;
    }
}
