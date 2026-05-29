using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer die Recommendation-Logik (laut CLAUDE.md testpflichtig).
/// Deckt ab: Recommend (leer/limit/ranking), Learn (Bestaetigung/Dedup/Massnahmen)
/// und TrainModel (minSamples-Schwelle).
/// </summary>
public sealed class MeasureRecommendationServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _storePath;
    private readonly string _modelPath;

    public MeasureRecommendationServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"measurereco_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _storePath = Path.Combine(_dir, "store.json");
        _modelPath = Path.Combine(_dir, "model.zip");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* Best-Effort-Cleanup der Temp-Dateien */ }
    }

    private MeasureRecommendationService NewService() => new(_storePath, _modelPath);

    /// <summary>Baut einen Lern-/Abfrage-Record. measuresUserEdited=true erfuellt IsUserConfirmed.</summary>
    private static HaltungRecord Record(string? codes, string? measures, bool measuresUserEdited = true)
    {
        var rec = new HaltungRecord();
        if (codes is not null)
            rec.SetFieldValue("Primaere_Schaeden", codes, FieldSource.Manual, userEdited: false);
        if (measures is not null)
            rec.SetFieldValue("Empfohlene_Sanierungsmassnahmen", measures, FieldSource.Manual, userEdited: measuresUserEdited);
        return rec;
    }

    // ── Recommend ───────────────────────────────────────────────────

    [Fact]
    public void Recommend_NullRecord_ReturnsEmpty()
        => Assert.Same(MeasureRecommendationResult.Empty, NewService().Recommend(null!));

    [Fact]
    public void Recommend_MaxSuggestionsZero_ReturnsEmpty()
        => Assert.Same(MeasureRecommendationResult.Empty,
            NewService().Recommend(Record("BAB", null), maxSuggestions: 0));

    [Fact]
    public void Recommend_NoDamageCodes_ReturnsEmpty()
        => Assert.Same(MeasureRecommendationResult.Empty,
            NewService().Recommend(Record(codes: null, measures: null)));

    [Fact]
    public void Recommend_AfterLearn_ReturnsLearnedMeasure()
    {
        var svc = NewService();
        Assert.True(svc.Learn(Record("BAB", "Inliner")));

        var result = svc.Recommend(Record("BAB", null));

        Assert.Contains("Inliner", result.Measures);
        Assert.Equal(1, result.SimilarCasesCount);
        Assert.False(result.UsedTrainedModel);
    }

    [Fact]
    public void Recommend_RanksMeasuresByFrequency()
    {
        var svc = NewService();
        svc.Learn(Record("BAB", "Inliner"));      // eigener Record (neue Id)
        svc.Learn(Record("BAB", "Inliner"));      // erneut → Inliner zaehlt 2x
        svc.Learn(Record("BAB", "Manschette"));   // Manschette zaehlt 1x

        var result = svc.Recommend(Record("BAB", null));

        Assert.Equal("Inliner", result.Measures[0]);
    }

    [Fact]
    public void Recommend_RespectsMaxSuggestions()
    {
        var svc = NewService();
        svc.Learn(Record("BAB", "Inliner;Manschette;Kurzliner"));

        var result = svc.Recommend(Record("BAB", null), maxSuggestions: 2);

        Assert.Equal(2, result.Measures.Count);
    }

    // ── Learn ───────────────────────────────────────────────────────

    [Fact]
    public void Learn_NotUserConfirmed_ReturnsFalse()
        => Assert.False(NewService().Learn(Record("BAB", "Inliner", measuresUserEdited: false)));

    [Fact]
    public void Learn_NoMeasures_ReturnsFalse()
    {
        var rec = new HaltungRecord();
        rec.SetFieldValue("Primaere_Schaeden", "BAB", FieldSource.Manual, userEdited: false);
        rec.SetFieldValue("Kosten", "1000", FieldSource.Manual, userEdited: true); // bestaetigt, aber keine Massnahme

        Assert.False(NewService().Learn(rec));
    }

    [Fact]
    public void Learn_SameRecordTwice_SecondIsIgnored()
    {
        var svc = NewService();
        var rec = Record("BAB", "Inliner");

        Assert.True(svc.Learn(rec));
        Assert.False(svc.Learn(rec)); // gleiche Id+Inhalt → gleiche Signatur
        Assert.Equal(1, svc.GetStats().TotalSamples);
    }

    // ── TrainModel ──────────────────────────────────────────────────

    [Fact]
    public void TrainModel_TooFewSamples_ReturnsFailure()
    {
        var result = NewService().TrainModel(minSamples: 25);

        Assert.False(result.Trained);
        Assert.Equal("Zu wenige Trainingsfaelle", result.ErrorMessage);
        Assert.Equal(0, result.TotalSamples);
    }

    [Fact]
    public void TrainModel_EnoughSamples_TrainsAndModelIsUsed()
    {
        var svc = NewService();
        svc.Learn(Record("BAB", "Inliner"));
        svc.Learn(Record("BAC", "Manschette"));

        var result = svc.TrainModel(minSamples: 1);

        Assert.True(result.Trained);
        Assert.Equal(2, result.TotalSamples);
        Assert.True(svc.GetStats().TrainedModelAvailable);
        Assert.True(svc.Recommend(Record("BAB", null)).UsedTrainedModel);
    }
}
