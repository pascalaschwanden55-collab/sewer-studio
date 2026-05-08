using BenchmarkDotNet.Attributes;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Benchmarks.Benchmarks;

/// <summary>
/// Roadmap Z.1: <see cref="TrainingSample.BuildCanonicalSignature"/> ist die
/// zentrale Dedup-Funktion. Wird beim Self-Training fuer jeden Sample
/// einmal ausgewertet — bei 21k Samples in der KB darf das nicht zum
/// Bottleneck werden.
/// </summary>
[MemoryDiagnoser]
public class TrainingSampleSignatureBench
{
    [Benchmark]
    public string Build_NoClock()
        => TrainingSample.BuildCanonicalSignature(
            caseId: "haltung-100-200",
            code: "BAB B",
            meterCenter: 12.34,
            meterEnd: 12.34);

    [Benchmark]
    public string Build_WithClock()
        => TrainingSample.BuildCanonicalSignature(
            caseId: "haltung-100-200",
            code: "BAB B",
            meterCenter: 12.34,
            meterEnd: 14.50,
            clock: "3 Uhr");
}
