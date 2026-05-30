using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

/// <summary>
/// Tests fuer CoordinateTransform LV95 -> WebMercator (Naeherungsformel).
///
/// WGS84-Referenzwerte (REFRAME-API geodesy.geo.admin.ch, abgefragt 2026-05-30):
///   E=2'600'000 / N=1'200'000  -> lon=7.438632420871814  lat=46.9510827728495
///   E=2'690'511.225 / N=1'194'863.079 -> lon=8.626486614290537  lat=46.89871734160188
///
/// EPSG:3857-Referenzwerte berechnet mit:
///   X = lon * 20037508.342789244 / 180          (= lon * R * PI/180, korrekt)
///   Y = ln(tan((90+lat)*PI/360)) * 6378137.0    (Erdradius R, korrekt)
/// Quelle WGS84-Werte: https://geodesy.geo.admin.ch/reframe/lv95towgs84 (swisstopo REFRAME)
///   Fundamentalpunkt: X=828064.773  Y=5934093.188
///   Uri-Punkt:        X=960296.097  Y=5925557.806
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
        // -> EPSG:3857 X = lon*20037508.342789244/180, Y = ln(tan((90+lat)*PI/360))*6378137.0
        // -> WebMercator X=828064.773  Y=5934093.188
        const double expectedX = 828064.773;
        const double expectedY = 5934093.188;

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
        // -> EPSG:3857 X = lon*20037508.342789244/180, Y = ln(tan((90+lat)*PI/360))*6378137.0
        // -> WebMercator X=960296.097  Y=5925557.806
        const double expectedX = 960296.097;
        const double expectedY = 5925557.806;

        // Act
        var (x, y) = CoordinateTransform.Lv95ToWebMercator(2_690_511.225, 1_194_863.079);

        // Assert
        Assert.True(System.Math.Abs(x - expectedX) < Toleranz,
            $"X-Abweichung {System.Math.Abs(x - expectedX):F2} m > {Toleranz} m  (erwartet {expectedX}, berechnet {x:F3})");
        Assert.True(System.Math.Abs(y - expectedY) < Toleranz,
            $"Y-Abweichung {System.Math.Abs(y - expectedY):F2} m > {Toleranz} m  (erwartet {expectedY}, berechnet {y:F3})");
    }
}
