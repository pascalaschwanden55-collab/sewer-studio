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
}
