using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.Infrastructure.Vsa.Classification;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Eine beschaedigte Regeltabelle darf nie zu einer stillen leeren Klassifizierung
/// fuehren (Audit 2026-08-14, Befund Q-B1). Vorher schluckte
/// <see cref="VsaClassificationTable.LoadFromFile"/> jeden Fehler und lieferte eine
/// leere Tabelle zurueck. Der Aufrufer meldete dann "erfolgreich" mit null Regeln —
/// die Zustandsklassen im Protokoll waren damit ohne Fehlermeldung wertlos. Der dafuer
/// vorgesehene Fehlerpfad VSA_TABLE_PARSE_FAILED war unerreichbarer Code.
/// </summary>
public sealed class VsaClassificationTableLoadTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "sewer-vsa-table-" + Guid.NewGuid().ToString("N"));

    public VsaClassificationTableLoadTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* Aufraeumen ist Nebensache */ }
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void LoadFromFile_KaputtesJson_WirftStattLeererTabelle()
    {
        var path = WriteFile("classification_channels.json", "{ das ist kein JSON ");

        Assert.ThrowsAny<Exception>(() => VsaClassificationTable.LoadFromFile(path));
    }

    [Fact]
    public void LoadFromFile_JsonNull_WirftStattLeererTabelle()
    {
        // "null" deserialisiert erfolgreich zu null — auch das ist keine gueltige Tabelle.
        var path = WriteFile("classification_channels.json", "null");

        Assert.ThrowsAny<Exception>(() => VsaClassificationTable.LoadFromFile(path));
    }

    [Fact]
    public void LoadFromFile_FehlendeDatei_Wirft()
    {
        var path = Path.Combine(_dir, "gibt-es-nicht.json");

        Assert.ThrowsAny<Exception>(() => VsaClassificationTable.LoadFromFile(path));
    }

    [Fact]
    public void LoadFromFile_GueltigeTabelle_LiestRegeln()
    {
        var path = WriteFile(
            "classification_channels.json",
            """{"rules":[{"code":"BAB","ezd":3,"ezs":2,"ezb":1}]}""");

        var table = VsaClassificationTable.LoadFromFile(path);

        Assert.NotNull(table.Find("BAB"));
    }

    [Fact]
    public void EvaluateRecord_KaputteTabelle_MeldetParseFehlerStattErfolg()
    {
        // Der eigentliche Schadensfall: vorher lief die Bewertung mit null Regeln
        // durch und meldete Erfolg.
        var kaputt = WriteFile("classification_channels.json", "{ kaputt ");
        var service = new VsaEvaluationService(kaputt, kaputt, useV2Engine: false);

        var result = service.EvaluateRecord(new HaltungRecord());

        Assert.False(result.Ok);
        Assert.Equal("VSA_TABLE_PARSE_FAILED", result.ErrorCode);
    }
}
