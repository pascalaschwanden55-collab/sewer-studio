using System.Linq;
using BenchmarkDotNet.Attributes;
using AuswertungPro.Next.Application.Ai.Annotation;

namespace AuswertungPro.Next.Benchmarks.Benchmarks;

/// <summary>
/// Roadmap Z.1: PDF-Beobachtungs-Parser ist im Operateur-Annotation-Workflow
/// auf dem User-Klick-Pfad (Submodus-Start). Ueber 500 Codes in einem grossen
/// Protokoll sollten unter 100 ms parsen, sonst spuert der Operator das.
/// </summary>
[MemoryDiagnoser]
public class BeobachtungParserBench
{
    private string _smallText = "";
    private string _largeText = "";

    [GlobalSetup]
    public void Setup()
    {
        // Klein: ~10 Beobachtungen, typischer Haltungsordner
        _smallText = string.Join("\n",
            Enumerable.Range(0, 10).Select(i =>
                $"{i * 5}.{i:00}  BAB B   Riss laengs Eintrag {i}"));

        // Gross: 500 Beobachtungen, maximaler Sammler-Inspektionslauf
        _largeText = string.Join("\n",
            Enumerable.Range(0, 500).Select(i =>
                $"{i * 0.5:F2}  BAB B   Riss laengs Eintrag {i}"));
    }

    [Benchmark]
    public int Parse_Small_10Rows() => BeobachtungParser.Parse(_smallText).Count;

    [Benchmark]
    public int Parse_Large_500Rows() => BeobachtungParser.Parse(_largeText).Count;
}
