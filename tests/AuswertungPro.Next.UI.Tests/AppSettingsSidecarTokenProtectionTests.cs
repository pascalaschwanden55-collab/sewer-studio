using System.Text.Json;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AppSettingsSidecarTokenProtectionTests
{
    [Fact]
    public void Json_roundtrip_protects_sidecar_token_and_restores_runtime_value()
    {
        const string secret = "sidecar-token-darf-nicht-im-klartext-stehen";
        var settings = new AppSettings
        {
            PipelineSidecarToken = secret
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        Assert.Contains("dpapi:v1:", json, StringComparison.Ordinal);
        Assert.Equal(secret, restored?.PipelineSidecarToken);
    }

    [Fact]
    public void Legacy_plaintext_token_is_read_and_protected_on_next_write()
    {
        const string legacySecret = "alter-klartext-token";
        const string legacyJson = """
            {
              "EnableDiagnostics": false,
              "PipelineSidecarToken": "alter-klartext-token"
            }
            """;

        var settings = JsonSerializer.Deserialize<AppSettings>(legacyJson);
        var migratedJson = JsonSerializer.Serialize(settings);

        Assert.NotNull(settings);
        Assert.False(settings.EnableDiagnostics);
        Assert.Equal(legacySecret, settings.PipelineSidecarToken);
        Assert.DoesNotContain(legacySecret, migratedJson, StringComparison.Ordinal);
        Assert.Contains("dpapi:v1:", migratedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_protected_token_does_not_discard_other_settings()
    {
        const string json = """
            {
              "EnableDiagnostics": false,
              "PipelineSidecarToken": "dpapi:v1:kein-gueltiges-base64"
            }
            """;

        var settings = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(settings);
        Assert.False(settings.EnableDiagnostics);
        Assert.Null(settings.PipelineSidecarToken);
    }
}
