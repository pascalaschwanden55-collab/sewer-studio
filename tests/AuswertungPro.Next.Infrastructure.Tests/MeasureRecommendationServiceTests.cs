using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer K2 aus V4.2 Gesamt-Audit: 659 Zeilen MeasureRecommendationService
/// waren komplett ungetestet obwohl CLAUDE.md explizit fordert „Tests NUR fuer
/// Recommendation- und QualityGate-Logik". Diese Suite schliesst die Luecke.
///
/// Abgedeckt: Learn (Dedup, User-Confirmed-Gate, Code-Normalisierung,
/// Cost-Aggregate, Sanitizer), Recommend (Score-Aggregation, Durchschnittsberechnung,
/// Trained-Model-Priorisierung), TrainModel (Min-Samples-Guard, Roundtrip),
/// Stats, Signature-Stabilitaet, Persistenz-Roundtrip.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MeasureRecommendationServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storePath;
    private readonly string _modelPath;

    public MeasureRecommendationServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SewerStudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storePath = Path.Combine(_tempDir, "measure_store.json");
        _modelPath = Path.Combine(_tempDir, "measure_model.zip");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    // ── Helpers ──

    private MeasureRecommendationService NewService()
        => new(_storePath, _modelPath);

    /// <summary>Erzeugt einen HaltungRecord mit Codes + Massnahmen + optionalen Kosten.
    /// UserEdited ist default true, weil Learn() das verlangt.</summary>
    private static HaltungRecord NewRecord(
        IEnumerable<string>? codes = null,
        string? massnahmen = null,
        decimal? kosten = null,
        decimal? inlinerM = null,
        int? inlinerStk = null,
        int? anschluesse = null,
        int? manschette = null,
        int? kurzliner = null,
        bool userConfirmed = true)
    {
        var rec = new HaltungRecord();
        if (codes is not null)
            foreach (var c in codes)
                rec.VsaFindings.Add(new VsaFinding { KanalSchadencode = c });

        void Set(string field, string? value)
        {
            if (value is null) return;
            rec.SetFieldValue(field, value,
                source: FieldSource.Manual,
                userEdited: userConfirmed);
        }

        Set("Empfohlene_Sanierungsmassnahmen", massnahmen);
        Set("Kosten", kosten?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("Renovierung_Inliner_m", inlinerM?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("Renovierung_Inliner_Stk", inlinerStk?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("Anschluesse_verpressen", anschluesse?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("Reparatur_Manschette", manschette?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("Reparatur_Kurzliner", kurzliner?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return rec;
    }

    // ── Learn: Basics ──

    [Fact]
    public void Learn_NullRecord_ReturnsFalse()
    {
        var svc = NewService();
        Assert.False(svc.Learn(null!));
    }

    [Fact]
    public void Learn_OhneUserEdited_LernkandidatWirdIgnoriert()
    {
        var svc = NewService();
        var rec = NewRecord(
            codes: new[] { "BAB" },
            massnahmen: "Inliner",
            userConfirmed: false);

        Assert.False(svc.Learn(rec));
        Assert.Equal(0, svc.GetStats().TotalSamples);
    }

    [Fact]
    public void Learn_OhneCodes_LerntNicht()
    {
        var svc = NewService();
        var rec = NewRecord(codes: Array.Empty<string>(), massnahmen: "Inliner");
        Assert.False(svc.Learn(rec));
    }

    [Fact]
    public void Learn_OhneMassnahmen_LerntNicht()
    {
        var svc = NewService();
        var rec = NewRecord(codes: new[] { "BAB" }, massnahmen: null);
        Assert.False(svc.Learn(rec));
    }

    [Fact]
    public void Learn_GleicherRecord_WirdNurEinmalGezaehlt()
    {
        var svc = NewService();
        var rec = NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner");

        Assert.True(svc.Learn(rec));
        Assert.False(svc.Learn(rec)); // Signatur existiert bereits → dedup
        Assert.Equal(1, svc.GetStats().TotalSamples);
    }

    // ── Recommend: Score-Aggregation ──

    [Fact]
    public void Recommend_OhneDamageCodes_LiefertEmpty()
    {
        var svc = NewService();
        var rec = NewRecord(codes: Array.Empty<string>());
        var result = svc.Recommend(rec);
        Assert.Same(MeasureRecommendationResult.Empty, result);
    }

    [Fact]
    public void Recommend_OhnePassendenLerndatensatz_LiefertEmpty()
    {
        var svc = NewService();
        var rec = NewRecord(codes: new[] { "BAB" });
        var result = svc.Recommend(rec);
        Assert.Empty(result.Measures);
    }

    [Fact]
    public void Recommend_NimmtMassnahmeMitHoechsterHaeufigkeit()
    {
        var svc = NewService();
        // 2× Inliner + 1× Manschette fuer Code BAB
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner"));
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner", kosten: 1m)); // anderer Record
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Manschette"));

        var result = svc.Recommend(NewRecord(codes: new[] { "BAB" }));

        Assert.NotEmpty(result.Measures);
        Assert.Equal("Inliner", result.Measures[0]);
    }

    [Fact]
    public void Recommend_AggregiertScoreUeberMehrereCodes()
    {
        var svc = NewService();
        // Code A sagt „Inliner", Code B sagt „Inliner" → aggregiert 2
        // Code A sagt „Manschette" → 1
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner"));
        svc.Learn(NewRecord(codes: new[] { "BAC" }, massnahmen: "Inliner"));
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Manschette", kosten: 1m));

        // Haltung hat beide Codes → Summe soll Inliner(2) > Manschette(1) ergeben
        var result = svc.Recommend(NewRecord(codes: new[] { "BAB", "BAC" }));

        Assert.NotEmpty(result.Measures);
        Assert.Equal("Inliner", result.Measures[0]);
    }

    [Fact]
    public void Recommend_MaxSuggestionsLimitiertListe()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner"));
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Manschette", kosten: 1m));
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Kurzliner", kosten: 2m));

        var result = svc.Recommend(NewRecord(codes: new[] { "BAB" }), maxSuggestions: 2);

        Assert.Equal(2, result.Measures.Count);
    }

    // ── Recommend: Cost-Mittelung ──

    [Fact]
    public void Recommend_MitteltKostenUeberMehrereLerndaten()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner", kosten: 10_000m));
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner", kosten: 20_000m));
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner", kosten: 30_000m));

        var result = svc.Recommend(NewRecord(codes: new[] { "BAB" }));

        Assert.Equal(3, result.SimilarCasesCount);
        Assert.Equal(20_000m, result.EstimatedTotalCost);
    }

    [Fact]
    public void Recommend_IgnoriertNegativeOderAbsurdeKostenBeiMittelung()
    {
        // Sanitizer verwirft Kosten > 10_000_000 und <= 0
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner", kosten: 10_000m));
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner",
            kosten: 99_999_999m)); // Sanitizer wirft das raus
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner", kosten: 20_000m));

        var result = svc.Recommend(NewRecord(codes: new[] { "BAB" }));

        // Samples=3, aber Kosten nur 2x gezaehlt → Mittel von 10k+20k = 15k
        Assert.Equal(3, result.SimilarCasesCount);
        Assert.Equal(15_000m, result.EstimatedTotalCost);
    }

    [Fact]
    public void Recommend_LiefertNullFuerUnbekannteKosten()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner"));
        var result = svc.Recommend(NewRecord(codes: new[] { "BAB" }));
        Assert.Null(result.EstimatedTotalCost);
        Assert.Null(result.RenovierungInlinerM);
    }

    // ── Code-Signature: Stabilitaet ──

    [Fact]
    public void Recommend_CodeSignatureIstReihenfolgeUnabhaengig()
    {
        var svc = NewService();
        // Gelernt mit Reihenfolge {BAC, BAB}, Recommend mit {BAB, BAC} → gleiche Signatur
        svc.Learn(NewRecord(codes: new[] { "BAC", "BAB" }, massnahmen: "Inliner", kosten: 50_000m));

        var result = svc.Recommend(NewRecord(codes: new[] { "BAB", "BAC" }));

        Assert.Equal(1, result.SimilarCasesCount);
        Assert.Equal(50_000m, result.EstimatedTotalCost);
    }

    [Fact]
    public void Recommend_CodeNormalisierungLowerUndUpperCaseGleich()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "bab" }, massnahmen: "Inliner"));
        var result = svc.Recommend(NewRecord(codes: new[] { "BAB" }));
        Assert.NotEmpty(result.Measures);
    }

    [Fact]
    public void Recommend_SonderzeichenInCodesWerdenIgnoriert()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB-01/x" }, massnahmen: "Inliner"));
        // „BAB-01/x" → normalisiert → „BAB01X"
        var result = svc.Recommend(NewRecord(codes: new[] { "BAB01X" }));
        Assert.NotEmpty(result.Measures);
    }

    // ── TrainModel ──

    [Fact]
    public void TrainModel_UnterMinSamples_GibtFehlerOhneCrash()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner"));

        var result = svc.TrainModel(minSamples: 5);

        Assert.False(result.Trained);
        Assert.Equal(1, result.TotalSamples);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void TrainModel_MitGenugSamples_PersistiertModellUndIstWiederverwendbar()
    {
        // Arrange: 2 Samples → TrainModel mit minSamples=2 → Modell gespeichert
        var svc1 = NewService();
        svc1.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner"));
        svc1.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner", kosten: 12_000m));

        var train = svc1.TrainModel(minSamples: 2);
        Assert.True(train.Trained);
        Assert.Equal(2, train.TotalSamples);

        // Neue Instanz zeigt auf gleiche Pfade → muss das Modell laden
        var svc2 = NewService();
        var stats = svc2.GetStats();
        Assert.True(stats.TrainedModelAvailable);
        Assert.Equal(2, stats.TrainedModelSamples);

        var result = svc2.Recommend(NewRecord(codes: new[] { "BAB" }));
        Assert.True(result.UsedTrainedModel);
        Assert.Equal("Inliner", result.Measures[0]);
    }

    // ── Persistenz ──

    [Fact]
    public void Store_WirdZwischenInstanzenPersistiert()
    {
        var svc1 = NewService();
        svc1.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner", kosten: 5_000m));

        // Neue Instanz laedt die JSON-Datei
        var svc2 = NewService();
        var result = svc2.Recommend(NewRecord(codes: new[] { "BAB" }));
        Assert.NotEmpty(result.Measures);
        Assert.Equal("Inliner", result.Measures[0]);
        Assert.Equal(5_000m, result.EstimatedTotalCost);
    }

    // ── Stats ──

    [Fact]
    public void GetStats_ZaehltCodesUndSignaturen()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner"));
        svc.Learn(NewRecord(codes: new[] { "BAC" }, massnahmen: "Manschette"));
        svc.Learn(NewRecord(codes: new[] { "BAB", "BAC" }, massnahmen: "Inliner"));

        var stats = svc.GetStats();
        Assert.Equal(3, stats.TotalSamples);
        Assert.Equal(2, stats.DistinctDamageCodes);
        Assert.Equal(3, stats.CodeSignatures); // {BAB}, {BAC}, {BAB,BAC}
        Assert.False(stats.TrainedModelAvailable);
    }

    // ── Primaere_Schaeden-Feld ──

    [Fact]
    public void Recommend_BeruecksichtigtPrimaereSchaedenFeldWennKeineVsaFindings()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner"));

        // Neuer Record: keine VsaFindings, aber „Primaere_Schaeden" mit BAB als erstem Token
        var rec = new HaltungRecord();
        rec.SetFieldValue("Primaere_Schaeden", "BAB @ 12.5m", FieldSource.Manual, userEdited: false);

        var result = svc.Recommend(rec);
        Assert.NotEmpty(result.Measures);
        Assert.Equal("Inliner", result.Measures[0]);
    }

    // ── Massnahmen-Parser ──

    [Fact]
    public void Learn_ParstMassnahmenMitVerschiedenenTrennzeichen()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "Inliner; Manschette, Kurzliner"));

        var result = svc.Recommend(NewRecord(codes: new[] { "BAB" }), maxSuggestions: 5);
        Assert.Contains("Inliner", result.Measures);
        Assert.Contains("Manschette", result.Measures);
        Assert.Contains("Kurzliner", result.Measures);
    }

    [Fact]
    public void Learn_EntferntBulletZeichenAusMassnahmen()
    {
        var svc = NewService();
        svc.Learn(NewRecord(codes: new[] { "BAB" }, massnahmen: "- Inliner\n- Manschette"));

        var result = svc.Recommend(NewRecord(codes: new[] { "BAB" }), maxSuggestions: 5);
        Assert.Contains("Inliner", result.Measures);
        Assert.Contains("Manschette", result.Measures);
    }
}
