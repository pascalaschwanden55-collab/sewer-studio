using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

/// <summary>
/// Tests fuer CoordinateTransform LV95 -> WebMercator (Naeherungsformel).
///
/// Referenzwerte (REFRAME-API geodesy.geo.admin.ch, abgefragt 2026-05-30):
///   E=2'600'000 / N=1'200'000  -> lon=7.438632420871814  lat=46.9510827728495
///   E=2'690'511.225 / N=1'194'863.079 -> lon=8.626486614290537  lat=46.89871734160188
///
/// WebMercator daraus berechnet:  X = lon * 20037508.342789244 / 180
///                                 Y = ln(tan((90+lat)*PI/360)) * 20037508.342789244 / 180
///   Fundamentalpunkt: X=828064.773  Y=103569.464
///   Uri-Punkt:        X=960296.097  Y=103420.494
///
/// Toleranz: +-10 m (Naeherungsformel hat ca. 1 m Genauigkeit).
/// </summary>
public class CoordinateTransformTests
{
    private const double Toleranz = 10.0; // Meter

    [Fact]
    public void Lv95ToWebMercator_Fundamentalpunkt_StimmtMitReferenz()
    {
        // Arrange
        // LV95-Fundamentalpunkt (Nullpunkt des Netzes, nahe Bern)
        // Referenz REFRAME geodesy.geo.admin.ch: lon=7.438632420871814 lat=46.9510827728495
        // -> WebMercator X=828064.773  Y=103569.464
        const double expectedX = 828064.773;
        const double expectedY = 103569.464;

        // Act
        var (x, y) = CoordinateTransform.Lv95ToWebMercator(2_600_000.0, 1_200_000.0);

        // Assert
        Assert.True(System.Math.Abs(x - expectedX) < Toleranz,
            $"X-Abweichung {System.Math.Abs(x - expectedX):F2} m > {Toleranz} m  (erwartet {expectedX}, berechnet {x:F3})");
        Assert.True(System.Math.Abs(y - expectedY) < Toleranz,
            $"Y-Abweichung {System.Math.Abs(y - expectedY):F2} m > {Toleranz} m  (erwartet {expectedY}, berechnet {y:F3})");
    }

    [Fact]
    public void Lv95ToWebMercator_UriAltdorf_StimmtMitReferenz()
    {
        // Arrange
        // Uri/Altdorf-Punkt aus Haltungsdaten
        // Referenz REFRAME geodesy.geo.admin.ch: lon=8.626486614290537 lat=46.89871734160188
        // -> WebMercator X=960296.097  Y=103420.494
        const double expectedX = 960296.097;
        const double expectedY = 103420.494;

        // Act
        var (x, y) = CoordinateTransform.Lv95ToWebMercator(2_690_511.225, 1_194_863.079);

        // Assert
        Assert.True(System.Math.Abs(x - expectedX) < Toleranz,
            $"X-Abweichung {System.Math.Abs(x - expectedX):F2} m > {Toleranz} m  (erwartet {expectedX}, berechnet {x:F3})");
        Assert.True(System.Math.Abs(y - expectedY) < Toleranz,
            $"Y-Abweichung {System.Math.Abs(y - expectedY):F2} m > {Toleranz} m  (erwartet {expectedY}, berechnet {y:F3})");
    }
}
