using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// V4.3 Phase 3.1 — PhotoMeasurementResult erweitert um Metadaten-Felder.
/// Smoke-Test dass Nullable-Defaults und Setter funktionieren.
/// </summary>
[Trait("Category", "Unit")]
public class PhotoMeasurementResultMetadataTests
{
    [Fact]
    public void Defaults_AreNull()
    {
        var r = new PhotoMeasurementResult();
        Assert.Null(r.Value1);
        Assert.Null(r.Value2);
        Assert.Null(r.Unit1);
        Assert.Null(r.Unit2);
        Assert.Null(r.MeasurementTool);
        Assert.Null(r.MeasurementSubject);
    }

    [Fact]
    public void CanSetToolAndUnit()
    {
        var r = new PhotoMeasurementResult
        {
            Value1 = "15.3",
            Unit1 = "mm",
            MeasurementTool = "Lineal",
        };
        Assert.Equal("15.3", r.Value1);
        Assert.Equal("mm", r.Unit1);
        Assert.Equal("Lineal", r.MeasurementTool);
    }

    [Fact]
    public void CanSetSubjectForCrossSection()
    {
        var r = new PhotoMeasurementResult
        {
            Value1 = "42",
            Unit1 = "%",
            MeasurementTool = "Querschnitt",
            MeasurementSubject = "Wurzel"
        };
        Assert.Equal("Wurzel", r.MeasurementSubject);
    }
}

/// <summary>
/// V4.3 Phase 3.1 — VsaFinding erweitert um Einheit + MeasurementTool + Subject.
/// </summary>
[Trait("Category", "Unit")]
public class VsaFindingMetadataTests
{
    [Fact]
    public void Defaults_AreNull()
    {
        var f = new VsaFinding();
        Assert.Null(f.Einheit1);
        Assert.Null(f.Einheit2);
        Assert.Null(f.MeasurementTool);
        Assert.Null(f.MeasurementSubject);
    }

    [Fact]
    public void Settable_All()
    {
        var f = new VsaFinding
        {
            KanalSchadencode = "BBA",
            Quantifizierung1 = "8",
            Einheit1 = "%",
            MeasurementTool = "Querschnitt",
            MeasurementSubject = "Wurzel"
        };
        Assert.Equal("%", f.Einheit1);
        Assert.Equal("Querschnitt", f.MeasurementTool);
        Assert.Equal("Wurzel", f.MeasurementSubject);
    }
}
