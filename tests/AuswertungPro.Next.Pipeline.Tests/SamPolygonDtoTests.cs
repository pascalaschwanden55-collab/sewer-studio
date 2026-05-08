using System.Collections.Generic;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Slice 1 (Operateur-Annotation): SamRequest und SamMaskResult werden um
/// optionale Polygon-Felder ergaenzt. Backward-Kompatibilitaet ist Pflicht —
/// alte Sidecar-Antworten ohne polygon_points muessen weiterhin parsen.
/// </summary>
public sealed class SamPolygonDtoTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    [Fact]
    public void SamRequest_ReturnPolygon_DefaultsToFalse()
    {
        var req = new SamRequest(
            ImageBase64: "x",
            BoundingBoxes: System.Array.Empty<SamBoundingBox>());

        Assert.False(req.ReturnPolygon);
    }

    [Fact]
    public void SamRequest_ReturnPolygon_SerializedWhenTrue()
    {
        var req = new SamRequest(
            ImageBase64: "x",
            BoundingBoxes: System.Array.Empty<SamBoundingBox>(),
            ReturnPolygon: true);

        var json = JsonSerializer.Serialize(req, JsonOpts);

        Assert.Contains("\"return_polygon\":true", json);
    }

    [Fact]
    public void SamMaskResult_LegacyJson_WithoutPolygon_RoundTripsAsNull()
    {
        // Sidecar-Antwort vor Slice 1: kein polygon_points-Feld.
        const string legacyJson = """
        {
          "label": "crack",
          "confidence": 0.85,
          "bbox": [10.0, 20.0, 30.0, 40.0],
          "mask_rle": "rle-data",
          "mask_area_pixels": 1234,
          "image_area_pixels": 1920000,
          "height_pixels": 50,
          "width_pixels": 60,
          "centroid_x": 22.5,
          "centroid_y": 33.5
        }
        """;

        var mask = JsonSerializer.Deserialize<SamMaskResult>(legacyJson, JsonOpts);

        Assert.NotNull(mask);
        Assert.Equal("crack", mask!.Label);
        Assert.Equal(50, mask.HeightPixels);
        Assert.Equal(60, mask.WidthPixels);
        Assert.Equal(22.5, mask.CentroidX);
        Assert.Equal(33.5, mask.CentroidY);
        Assert.Null(mask.PolygonPoints);
    }

    [Fact]
    public void SamMaskResult_NewJson_WithPolygon_DeserializesPoints()
    {
        const string newJson = """
        {
          "label": "crack",
          "confidence": 0.85,
          "bbox": [10.0, 20.0, 30.0, 40.0],
          "mask_rle": "rle-data",
          "mask_area_pixels": 1234,
          "image_area_pixels": 1920000,
          "height_pixels": 50,
          "width_pixels": 60,
          "centroid_x": 22.5,
          "centroid_y": 33.5,
          "polygon_points": [[10.0, 20.0], [30.0, 20.0], [30.0, 40.0], [10.0, 40.0]]
        }
        """;

        var mask = JsonSerializer.Deserialize<SamMaskResult>(newJson, JsonOpts);

        Assert.NotNull(mask);
        Assert.NotNull(mask!.PolygonPoints);
        Assert.Equal(4, mask.PolygonPoints!.Count);
        Assert.Equal(10.0, mask.PolygonPoints[0][0]);
        Assert.Equal(20.0, mask.PolygonPoints[0][1]);
    }

    [Fact]
    public void SamMaskResult_AllExistingFieldsPreserved()
    {
        // Sicherstellen dass die 10 Pflicht-Felder noch da sind (kein Plan-Replace-Unfall).
        var mask = new SamMaskResult(
            Label: "x",
            Confidence: 0.5,
            Bbox: new[] { 0.0, 0.0, 1.0, 1.0 },
            MaskRle: "r",
            MaskAreaPixels: 1,
            ImageAreaPixels: 1,
            HeightPixels: 1,
            WidthPixels: 1,
            CentroidX: 0.5,
            CentroidY: 0.5);

        Assert.Equal(1, mask.HeightPixels);
        Assert.Equal(1, mask.WidthPixels);
        Assert.Equal(0.5, mask.CentroidX);
        Assert.Equal(0.5, mask.CentroidY);
        Assert.Null(mask.PolygonPoints);
    }
}
