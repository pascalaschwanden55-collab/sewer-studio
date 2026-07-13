using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Schuetzt ein einzelnes JSON-Geheimnis mit dem aktuellen Windows-Benutzerkonto.
/// Klartext aus aelteren settings.json bleibt lesbar und wird beim naechsten Speichern migriert.
/// </summary>
internal sealed class DpapiProtectedStringJsonConverter : JsonConverter<string?>
{
    internal const string ProtectedPrefix = "dpapi:v1:";
    private static readonly byte[] OptionalEntropy =
        Encoding.UTF8.GetBytes("SewerStudio.AppSettings.PipelineSidecarToken.v1");

    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Das geschuetzte Einstellungsfeld muss Text enthalten.");

        var persistedValue = reader.GetString();
        if (persistedValue is null
            || !persistedValue.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return persistedValue;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(persistedValue[ProtectedPrefix.Length..]);
            var clearBytes = ProtectedData.Unprotect(
                protectedBytes,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch (Exception ex) when (ex is FormatException
                                   or CryptographicException
                                   or PlatformNotSupportedException)
        {
            BestEffort.ReportWarning(
                "[Einstellungen] Der geschuetzte Sidecar-Token konnte nicht gelesen werden. " +
                "Die uebrigen Einstellungen bleiben erhalten; den Token bitte neu eingeben.");
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var clearBytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(
            clearBytes,
            OptionalEntropy,
            DataProtectionScope.CurrentUser);
        writer.WriteStringValue(ProtectedPrefix + Convert.ToBase64String(protectedBytes));
    }
}
