namespace AuswertungPro.Next.Application.Ai.Evaluation;

// ── BefundMatcher ───────────────────────────────────────────────────────────
// Vergleicht KI-Befunde gegen Protokoll-Ground-Truth – EHRLICH und ohne den
// Frame-Vergleich (der kommt erst spaeter als Verfeinerung). Drei Prinzipien:
//
//   1) GESTUFTE Meter-Toleranz statt einer groben ±2.0 m:
//        gruen  : Abstand <= 0.20 m  (sicherer Treffer)
//        gelb   : Abstand <= 0.50 m  (wahrscheinlicher Treffer)
//        darueber: kein Treffer.
//   2) EINS-ZU-EINS-Zuordnung ueber ALLE Paare gleichzeitig (maximale Anzahl
//      Treffer bei minimalem Gesamt-Abstand, reihenfolge-unabhaengig) – nicht
//      "der naechste gewinnt".
//   3) VIER Toepfe statt zwei:
//        Treffer (TP)        – gleiche Code-Familie + Meter passt
//        Falscher Code (WC)  – richtige Stelle, falsche Codierung
//        Verpasst (FN)       – Protokoll-Befund ohne KI-Partner
//        Fehlalarm (FP)      – KI-Befund ohne Protokoll-Partner (inkl. Detections
//                              ohne aufgeloesten VSA-Code: echte Erkennungen)
//      Praezision und Recall werden getrennt gerechnet.

/// <summary>
/// Ein einzelner Befund (Protokoll oder KI) – reduziert auf das fuer das Matching Noetige.
/// <paramref name="RefId"/> ist optional und wird vom Matching nicht ausgewertet; Aufrufer
/// koennen damit die Herkunft (z.B. EntryId) durchreichen, um Treffer auf ihre Objekte
/// (z.B. CodingEvents) zurueckzufuehren.
/// </summary>
public sealed record BefundMatchFinding(string Code, double MeterStart, double MeterEnd, string Label, string? RefId = null);

/// <summary>Ein zugeordnetes Paar (Protokoll ↔ KI) mit Abstand und Stufe (gruen/gelb).</summary>
public sealed record BefundMatchPair(BefundMatchFinding Gt, BefundMatchFinding Ki, double Gap, string Tier);

/// <summary>Konfiguration: Toleranzen und ausgeschlossene Code-Familien.</summary>
public sealed class BefundMatchOptions
{
    /// <summary>Sicherer Treffer: Abstand &lt;= dieser Wert (Meter).</summary>
    public double TolGruen { get; init; } = 0.20;

    /// <summary>Wahrscheinlicher Treffer: Abstand &lt;= dieser Wert (Meter).</summary>
    public double TolGelb { get; init; } = 0.50;

    /// <summary>Struktur-Anker, die nicht als Schaden gewertet werden (beidseitig herausgefiltert).</summary>
    public HashSet<string> ExcludedFamilies { get; init; } =
        new(StringComparer.OrdinalIgnoreCase) { "BCD", "BCE" };

    public static BefundMatchOptions Default { get; } = new();

    public bool IsExcluded(BefundMatchFinding f) => ExcludedFamilies.Contains(BefundMatcher.MainCode(f.Code));
}

/// <summary>Ergebnis eines Abgleichs: die vier Toepfe plus abgeleitete Kennzahlen.</summary>
public sealed class BefundMatchResult
{
    public List<BefundMatchPair> Treffer { get; } = new();        // TP – gleiche Familie + Meter
    public List<BefundMatchPair> FalscherCode { get; } = new();   // WC – Meter passt, Familie nicht
    public List<BefundMatchFinding> Verpasst { get; } = new();    // FN
    public List<BefundMatchFinding> Fehlalarm { get; } = new();   // FP (inkl. KI-Detections ohne Code)
    public int OhneCode { get; set; }                             // KI-Detections ohne VSA-Code (in Fehlalarm enthalten)
    public int IgnoriertAnker { get; set; }                       // BCD/BCE beidseitig (echte Anker, nicht gewertet)

    /// <summary>Praezision = Treffer / (Treffer + Fehlalarm + Falscher Code). 0 bei leerem Nenner.</summary>
    public double Precision
    {
        get
        {
            var nenner = Treffer.Count + Fehlalarm.Count + FalscherCode.Count;
            return nenner == 0 ? 0 : (double)Treffer.Count / nenner;
        }
    }

    /// <summary>Recall = Treffer / (Treffer + Verpasst + Falscher Code). 0 bei leerem Nenner.</summary>
    public double Recall
    {
        get
        {
            var nenner = Treffer.Count + Verpasst.Count + FalscherCode.Count;
            return nenner == 0 ? 0 : (double)Treffer.Count / nenner;
        }
    }

    /// <summary>Haengt ein weiteres Ergebnis an (fuer gepoolte Summen ueber mehrere Faelle).</summary>
    public void Add(BefundMatchResult other)
    {
        Treffer.AddRange(other.Treffer);
        FalscherCode.AddRange(other.FalscherCode);
        Verpasst.AddRange(other.Verpasst);
        Fehlalarm.AddRange(other.Fehlalarm);
        OhneCode += other.OhneCode;
        IgnoriertAnker += other.IgnoriertAnker;
    }
}

public static class BefundMatcher
{
    /// <summary>Hauptcode = die ersten 3 Zeichen (z.B. BCCBY → BCC). Leer bei fehlendem Code.</summary>
    public static string MainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        // Allen Whitespace entfernen (auch innen / Tabs), dann die ersten 3 Zeichen:
        // "BA B" -> "BAB". Schuetzt vor kaputten Familien mit Leerzeichen.
        var t = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        return t.Length <= 3 ? t : t[..3];
    }

    /// <summary>
    /// Abstand zweier Befunde in Metern. 0 wenn die Bereiche sich ueberlappen
    /// (so wird ein Punktschaden korrekt gegen einen Streckenschaden geprueft).
    /// </summary>
    public static double Gap(BefundMatchFinding a, BefundMatchFinding b)
    {
        double aS = Math.Min(a.MeterStart, a.MeterEnd), aE = Math.Max(a.MeterStart, a.MeterEnd);
        double bS = Math.Min(b.MeterStart, b.MeterEnd), bE = Math.Max(b.MeterStart, b.MeterEnd);
        double overlapStart = Math.Max(aS, bS), overlapEnd = Math.Min(aE, bE);
        return overlapStart <= overlapEnd ? 0.0 : overlapStart - overlapEnd;
    }

    public static BefundMatchResult Match(
        IReadOnlyList<BefundMatchFinding> groundTruth,
        IReadOnlyList<BefundMatchFinding> detections,
        BefundMatchOptions? options = null)
    {
        var opts = options ?? BefundMatchOptions.Default;
        var outcome = new BefundMatchResult();

        // ── Vorfilter: Anker (BCD/BCE) raus; KI ohne Code BLEIBT (zaehlt als Fehlalarm) ──
        var gt = new List<BefundMatchFinding>();
        foreach (var f in groundTruth)
        {
            if (opts.IsExcluded(f)) { outcome.IgnoriertAnker++; continue; }
            gt.Add(f);
        }

        var ki = new List<BefundMatchFinding>();
        foreach (var f in detections)
        {
            if (opts.IsExcluded(f)) { outcome.IgnoriertAnker++; continue; } // nur echte Anker (BCD/BCE) raus
            // KI-Detections OHNE Code sind ECHTE Modell-Erkennungen (Label vorhanden),
            // die nur nicht auf einen VSA-Code aufgeloest wurden. Sie zaehlen als Fehlalarm
            // und werden NICHT still verworfen (sonst waere die Praezision geschoent).
            if (MainCode(f.Code).Length == 0) outcome.OhneCode++;
            ki.Add(f);
        }

        // ── Phase 1: gleiche Familie + Meter <= gelb  →  Treffer (max. Anzahl, min. Abstand) ──
        var phase1 = MinCostMaxMatch(
            gt.Count, ki.Count,
            valid: (g, k) => MainCode(gt[g].Code) == MainCode(ki[k].Code) && Gap(gt[g], ki[k]) <= opts.TolGelb,
            cost: (g, k) => Gap(gt[g], ki[k]));

        var gtUsed = new bool[gt.Count];
        var kiUsed = new bool[ki.Count];
        foreach (var (g, k) in phase1)
        {
            gtUsed[g] = true; kiUsed[k] = true;
            var gap = Gap(gt[g], ki[k]);
            outcome.Treffer.Add(new BefundMatchPair(gt[g], ki[k], gap, gap <= opts.TolGruen ? "gruen" : "gelb"));
        }

        // ── Phase 2: Reste, Meter <= gelb aber andere Familie  →  Falscher Code ──
        var gtRest = Enumerable.Range(0, gt.Count).Where(g => !gtUsed[g]).ToList();
        var kiRest = Enumerable.Range(0, ki.Count).Where(k => !kiUsed[k]).ToList();

        var phase2 = MinCostMaxMatch(
            gtRest.Count, kiRest.Count,
            // andere Familie, aber KI-Code muss vorhanden sein: leere KI-Codes bleiben
            // garantiert Fehlalarm (FP) und werden nie faelschlich als "Falscher Code" gezaehlt.
            valid: (gi, ki2) => MainCode(ki[kiRest[ki2]].Code).Length > 0
                                && MainCode(gt[gtRest[gi]].Code) != MainCode(ki[kiRest[ki2]].Code)
                                && Gap(gt[gtRest[gi]], ki[kiRest[ki2]]) <= opts.TolGelb,
            cost: (gi, ki2) => Gap(gt[gtRest[gi]], ki[kiRest[ki2]]));

        var gtWc = new bool[gtRest.Count];
        var kiWc = new bool[kiRest.Count];
        foreach (var (gi, ki2) in phase2)
        {
            gtWc[gi] = true; kiWc[ki2] = true;
            var a = gt[gtRest[gi]]; var b = ki[kiRest[ki2]];
            outcome.FalscherCode.Add(new BefundMatchPair(a, b, Gap(a, b), "—"));
        }

        // ── Phase 3: was uebrig bleibt ──
        for (int gi = 0; gi < gtRest.Count; gi++)
            if (!gtWc[gi]) outcome.Verpasst.Add(gt[gtRest[gi]]);
        for (int ki2 = 0; ki2 < kiRest.Count; ki2++)
            if (!kiWc[ki2]) outcome.Fehlalarm.Add(ki[kiRest[ki2]]);

        return outcome;
    }

    /// <summary>
    /// Eins-zu-Eins-Zuordnung mit GLOBAL minimalem Gesamt-Abstand bei MAXIMALER
    /// Paarzahl (Min-Cost-Max-Cardinality via successive shortest paths / SPFA).
    /// Liefert immer die groesstmoegliche Anzahl gueltiger Paare UND – unter diesen –
    /// die mit der kleinsten Summe der Meter-Abstaende. Dadurch ist die gruen/gelb-
    /// Aufteilung reihenfolge-unabhaengig und nicht "gierig". n ist klein (wenige
    /// Befunde je Haltung), daher ist die Laufzeit unkritisch.
    /// </summary>
    private static List<(int L, int R)> MinCostMaxMatch(int nL, int nR, Func<int, int, bool> valid, Func<int, int, double> cost)
    {
        int source = 0;
        int sink = nL + nR + 1;
        int nodes = nL + nR + 2;
        var graph = new List<FlowEdge>[nodes];
        for (int i = 0; i < nodes; i++) graph[i] = new List<FlowEdge>();

        void AddEdge(int from, int to, double edgeCost)
        {
            graph[from].Add(new FlowEdge { To = to, Cap = 1, Cost = edgeCost, Rev = graph[to].Count });
            graph[to].Add(new FlowEdge { To = from, Cap = 0, Cost = -edgeCost, Rev = graph[from].Count - 1 });
        }

        for (int l = 0; l < nL; l++) AddEdge(source, 1 + l, 0);
        for (int r = 0; r < nR; r++) AddEdge(1 + nL + r, sink, 0);
        for (int l = 0; l < nL; l++)
            for (int r = 0; r < nR; r++)
                if (valid(l, r)) AddEdge(1 + l, 1 + nL + r, cost(l, r));

        // Successive shortest paths: solange ein guenstigster Augmentierungspfad
        // Source->Sink existiert, eine Einheit schieben. Negative Rest-Kanten -> SPFA.
        while (true)
        {
            var dist = new double[nodes];
            Array.Fill(dist, double.PositiveInfinity);
            dist[source] = 0;
            var inQueue = new bool[nodes];
            var prevNode = new int[nodes];
            var prevEdge = new int[nodes];
            Array.Fill(prevNode, -1);

            var queue = new Queue<int>();
            queue.Enqueue(source);
            inQueue[source] = true;
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                inQueue[u] = false;
                var edges = graph[u];
                for (int e = 0; e < edges.Count; e++)
                {
                    var ed = edges[e];
                    if (ed.Cap > 0 && dist[u] + ed.Cost < dist[ed.To] - 1e-12)
                    {
                        dist[ed.To] = dist[u] + ed.Cost;
                        prevNode[ed.To] = u;
                        prevEdge[ed.To] = e;
                        if (!inQueue[ed.To]) { queue.Enqueue(ed.To); inQueue[ed.To] = true; }
                    }
                }
            }

            if (double.IsPositiveInfinity(dist[sink])) break; // kein Augmentierungspfad mehr

            for (int cur = sink; cur != source; cur = prevNode[cur])
            {
                var ed = graph[prevNode[cur]][prevEdge[cur]];
                ed.Cap -= 1;
                graph[ed.To][ed.Rev].Cap += 1;
            }
        }

        // Gesaettigte Vorwaerts-Kanten links->rechts = gewaehlte Paare.
        var pairs = new List<(int, int)>();
        for (int l = 0; l < nL; l++)
            foreach (var ed in graph[1 + l])
                if (ed.To >= 1 + nL && ed.To <= nL + nR && ed.Cap == 0)
                    pairs.Add((l, ed.To - 1 - nL));
        return pairs;
    }

    /// <summary>Kante im Fluss-Graphen fuer das Min-Cost-Matching (mit Rest-Kante).</summary>
    private sealed class FlowEdge
    {
        public int To;
        public int Cap;
        public double Cost;
        public int Rev;
    }
}
