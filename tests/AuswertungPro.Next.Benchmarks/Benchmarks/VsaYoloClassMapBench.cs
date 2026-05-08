using BenchmarkDotNet.Attributes;
using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.Benchmarks.Benchmarks;

/// <summary>
/// Roadmap Z.1: <see cref="VsaYoloClassMap.TryGetClassId"/> wird im
/// Operateur-Annotation-Confirm-Pfad pro Sample einmal gerufen. Bei einem
/// Bulk-Re-Indexing-Run (Slice 2) skaliert das auf zehntausende Aufrufe —
/// hier muss der Lookup unter 1us bleiben.
/// </summary>
[MemoryDiagnoser]
public class VsaYoloClassMapBench
{
    [Benchmark]
    public bool TryGetClassId_Known()
        => VsaYoloClassMap.TryGetClassId("BAB B", out _);

    [Benchmark]
    public bool TryGetClassId_Unknown()
        => VsaYoloClassMap.TryGetClassId("ZZZ Q", out _);

    [Benchmark]
    public int GetClassId_Hot()
        => VsaYoloClassMap.GetClassId("BCD");
}
