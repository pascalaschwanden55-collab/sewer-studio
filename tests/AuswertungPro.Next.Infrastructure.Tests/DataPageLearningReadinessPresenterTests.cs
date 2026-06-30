using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer <see cref="LearningReadinessPresenter"/>.
/// Sichert die Ampel-Schwellenwert-Logik aus DataPageViewModel.UpdateLearningTrafficLight
/// ab (verhaltensneutral).
/// </summary>
public sealed class DataPageLearningReadinessPresenterTests
{
    // --- Schwellenwert-Konstanten ---

    [Fact]
    public void MinimumSamplesForTraining_ist_25()
        => Assert.Equal(25, LearningReadinessPresenter.MinimumSamplesForTraining);

    [Fact]
    public void StrongModelThreshold_ist_100()
        => Assert.Equal(100, LearningReadinessPresenter.StrongModelThreshold);

    // --- Ampel Rot (unter Minimum) ---

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(24)]
    public void Evaluate_liefert_rot_unter_minimum(int samples)
    {
        var (color, text) = LearningReadinessPresenter.Evaluate(samples);
        Assert.Equal("#C62828", color);
        Assert.Equal("Rot", text);
    }

    // --- Ampel Gelb (Minimum bis StrongModel - 1) ---

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(99)]
    public void Evaluate_liefert_gelb_zwischen_minimum_und_strong(int samples)
    {
        var (color, text) = LearningReadinessPresenter.Evaluate(samples);
        Assert.Equal("#F9A825", color);
        Assert.Equal("Gelb", text);
    }

    // --- Ampel Gruen (ab StrongModel) ---

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(1000)]
    public void Evaluate_liefert_gruen_ab_strong_threshold(int samples)
    {
        var (color, text) = LearningReadinessPresenter.Evaluate(samples);
        Assert.Equal("#2E7D32", color);
        Assert.Equal("Gruen", text);
    }

    [Fact]
    public void Build_liefert_null_fall_text_und_rot()
    {
        var stats = new MeasureLearningStats(0, 0, 0, false, null, null, "");

        var presentation = LearningReadinessPresenter.Build(stats);

        Assert.Equal("Lernbasis: 0 Faelle", presentation.Info);
        Assert.Equal("#C62828", presentation.Color);
        Assert.Equal("Rot", presentation.Text);
        Assert.True(presentation.IsVisible);
    }

    [Fact]
    public void Build_liefert_text_mit_aehnlichen_faellen_und_modell_hinweis()
    {
        var stats = new MeasureLearningStats(42, 3, 5, false, null, null, "");

        var presentation = LearningReadinessPresenter.Build(stats, similarCases: 4, estimatedCost: 123.45m);

        Assert.Equal(
            "Lernbasis: 42 Faelle / letzte Kostenschaetzung 123.45 aus 4 aehnlichen Haltungen / KI-Modell ab 25 Faellen",
            presentation.Info);
        Assert.Equal("#F9A825", presentation.Color);
        Assert.Equal("Gelb", presentation.Text);
        Assert.True(presentation.IsVisible);
    }

    [Fact]
    public void Build_liefert_text_mit_aktivem_modell()
    {
        var stats = new MeasureLearningStats(120, 8, 12, true, 98, null, "model.json");

        var presentation = LearningReadinessPresenter.Build(stats, similarCases: 3, estimatedCost: null);

        Assert.Equal(
            "Lernbasis: 120 Faelle / letzte Schaetzung aus 3 aehnlichen Haltungen / KI-Modell aktiv (98 Faelle)",
            presentation.Info);
        Assert.Equal("#2E7D32", presentation.Color);
        Assert.Equal("Gruen", presentation.Text);
        Assert.True(presentation.IsVisible);
    }
}
