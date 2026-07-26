using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Mapping des additiven detector_qualification-Feldes aus /health: vorhanden -> gemappt;
/// fehlend (aelterer Sidecar) -> null (fail-open, Rueckwaertskompatibilitaet).
/// </summary>
public sealed class SidecarHealthQualificationDtoTests
{
    [Fact]
    public void DetectorQualification_wird_aus_der_Health_Antwort_gemappt()
    {
        const string json = """
            {
              "status": "ok",
              "version": "1.2.0",
              "gpu": {},
              "detector_qualification": {
                "qualified": false,
                "reason": "Altmodell: BBox-Kollaps."
              }
            }
            """;

        var response = JsonSerializer.Deserialize<SidecarHealthResponse>(json);

        Assert.NotNull(response);
        Assert.NotNull(response.DetectorQualification);
        Assert.False(response.DetectorQualification.Qualified);
        Assert.Equal("Altmodell: BBox-Kollaps.", response.DetectorQualification.Reason);
    }

    [Fact]
    public void Fehlendes_Qualifikationsfeld_bleibt_null()
    {
        const string json = """
            {
              "status": "ok",
              "version": "1.2.0",
              "gpu": {}
            }
            """;

        var response = JsonSerializer.Deserialize<SidecarHealthResponse>(json);

        Assert.NotNull(response);
        Assert.Null(response.DetectorQualification);
    }
}
