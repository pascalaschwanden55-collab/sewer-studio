using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PhotoMeasurementToolPresentationPolicyTests
{
    [Theory]
    [InlineData((int)PhotoTool.LevelWater, LevelMode.Water)]
    [InlineData((int)PhotoTool.LevelDeposit, LevelMode.Deposit)]
    [InlineData((int)PhotoTool.LevelObstacle, LevelMode.Obstacle)]
    public void LevelWerkzeuge_ZeigenReglerUndSetzenModus(int toolValue, LevelMode expectedMode)
    {
        var tool = (PhotoTool)toolValue;
        var state = PhotoMeasurementToolPresentationPolicy.Build(tool, LevelMode.Water, isCalibrated: false);

        Assert.Equal(expectedMode, state.LevelMode);
        Assert.True(state.ShowLevelControls);
        Assert.True(state.ResetLevelSliders);
        Assert.False(state.ShowAngleControls);
        Assert.True(state.ShowDelete);
        Assert.True(state.IsOkEnabled);
        Assert.True(state.UseCrossCursor);
    }

    [Theory]
    [InlineData((int)PhotoTool.Lateral)]
    [InlineData((int)PhotoTool.Bend)]
    public void WinkelWerkzeuge_ZeigenUndInitialisierenWinkelregler(int toolValue)
    {
        var tool = (PhotoTool)toolValue;
        var state = PhotoMeasurementToolPresentationPolicy.Build(tool, LevelMode.Deposit, isCalibrated: false);

        Assert.Equal(LevelMode.Deposit, state.LevelMode);
        Assert.True(state.ShowAngleControls);
        Assert.True(state.ResetAngleSliders);
        Assert.False(state.ShowLevelControls);
        Assert.True(state.IsOkEnabled);
    }

    [Theory]
    [InlineData((int)PhotoTool.Deformation)]
    [InlineData((int)PhotoTool.CrossSection)]
    public void MehrpunktWerkzeuge_ZeigenUndo(int toolValue)
    {
        var tool = (PhotoTool)toolValue;
        var state = PhotoMeasurementToolPresentationPolicy.Build(tool, LevelMode.Water, isCalibrated: true);

        Assert.True(state.ShowUndo);
        Assert.True(state.ShowDelete);
        Assert.False(state.ShowLevelControls);
        Assert.False(state.ShowAngleControls);
    }

    // Der Querschnitt kam 2026-08-17 dazu. Der urspruengliche Gedanke war
    // "Millimeter brauchen eine Referenz, Prozente nicht" - fuer die Verformung
    // stimmt das (Verhaeltnis zweier gemessener Achsen), fuer den Querschnitt
    // nicht: Sein Prozentsatz bezieht sich auf die ROHRFLAECHE, und die kennt
    // man ohne Kalibrierung nicht. Ohne diese Pflicht rechnete das Werkzeug mit
    // einem erfundenen Durchmesser weiter und schrieb das Ergebnis nach Q1.
    [Theory]
    [InlineData((int)PhotoTool.Ruler)]
    [InlineData((int)PhotoTool.Connection)]
    [InlineData((int)PhotoTool.CrossSection)]
    public void MillimeterWerkzeuge_BrauchenKalibrierung(int toolValue)
    {
        var tool = (PhotoTool)toolValue;
        Assert.False(PhotoMeasurementToolPresentationPolicy.Build(tool, LevelMode.Water, false).IsOkEnabled);
        Assert.True(PhotoMeasurementToolPresentationPolicy.Build(tool, LevelMode.Water, true).IsOkEnabled);
    }

    [Fact]
    public void KeinWerkzeug_VerbirgtAktionenUndVerwendetPfeilcursor()
    {
        var state = PhotoMeasurementToolPresentationPolicy.Build(
            PhotoTool.None,
            LevelMode.Obstacle,
            isCalibrated: false);

        Assert.Equal(LevelMode.Obstacle, state.LevelMode);
        Assert.False(state.ShowLevelControls);
        Assert.False(state.ShowAngleControls);
        Assert.False(state.ShowUndo);
        Assert.False(state.ShowDelete);
        Assert.True(state.IsOkEnabled);
        Assert.False(state.UseCrossCursor);
        Assert.Equal("Werkzeug wählen, um mit der Messung zu beginnen.", state.StatusText);
    }

    [Theory]
    [InlineData((int)PhotoTool.Calibration)]
    [InlineData((int)PhotoTool.MarkRect)]
    [InlineData((int)PhotoTool.LevelWater)]
    [InlineData((int)PhotoTool.LevelDeposit)]
    [InlineData((int)PhotoTool.LevelObstacle)]
    [InlineData((int)PhotoTool.Deformation)]
    [InlineData((int)PhotoTool.Ruler)]
    [InlineData((int)PhotoTool.CrossSection)]
    [InlineData((int)PhotoTool.Lateral)]
    [InlineData((int)PhotoTool.Bend)]
    [InlineData((int)PhotoTool.Connection)]
    public void JedesWerkzeug_HatEinenBedienhinweis(int toolValue)
    {
        var tool = (PhotoTool)toolValue;
        var state = PhotoMeasurementToolPresentationPolicy.Build(tool, LevelMode.Water, isCalibrated: true);

        Assert.False(string.IsNullOrWhiteSpace(state.StatusText));
    }

    [Fact]
    public void Verformung_BrauchtKeineKalibrierung()
    {
        // Selbstbezogener Prozentsatz: (groesste - kleinste Achse) / groesste
        // Achse. Ohne Nenndurchmesser faellt DeformationPercent korrekt auf die
        // gemessene groessere Achse zurueck - hier waere eine Pflicht falsch.
        Assert.True(PhotoMeasurementToolPresentationPolicy
            .Build(PhotoTool.Deformation, LevelMode.Water, isCalibrated: false).IsOkEnabled);
    }
}
