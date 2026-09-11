using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>Welche WebGIS-Materialschreibweisen das Vokabular auf einen Normbegriff bringt.
///
/// Gemessen, nicht geraten: Die Zahl der unzugeordneten Eintraege ist festgehalten und darf
/// nur sinken. Wer einen Eintrag zuordnet, senkt sie hier - wer einen verliert, sieht es.
/// Die fachliche Zuordnung selbst ist Entscheidung der Fachperson (Stufe 2, XTF-Rueckweg).
/// </summary>
public sealed class ObjektaktenMaterialVokabularTests
{
    [Fact]
    public void Haltung_Materialdetail_aus_dem_WebGIS_gegen_das_Vokabular()
    {
        var katalog = FieldCatalog.Objektfelder;
        var voll = katalog.Auswahl(katalog.Feld("haltung.material").KatalogIdJeEltern)!;
        var ohneNorm = voll.Eintraege.Where(e => MaterialVokabular.NachNorm(e.Label) is null)
            .Select(e => $"{e.Eltern}:{e.OriginalCode} {e.Label}").ToArray();

        // Stand 11.09.2026 - siehe Doku WEBGIS-DETAILMASKEN.md, Abschnitt Vokabular.
        const int festgehalten = 40;
        Assert.True(ohneNorm.Length <= festgehalten,
            $"{ohneNorm.Length} von {voll.Eintraege.Count} Haltungs-Materialeintraegen ohne Normbegriff "
            + $"(festgehalten: {festgehalten}):\n" + string.Join("\n", ohneNorm));
    }

    [Fact]
    public void Schacht_Materialdetail_aus_dem_WebGIS_gegen_das_Vokabular()
    {
        var katalog = FieldCatalog.Objektfelder;
        var voll = katalog.Auswahl(katalog.Feld("schacht.materialdetail").KatalogIdJeEltern)!;
        var ohneNorm = voll.Eintraege.Where(e => SchachtMaterialVokabular.NachNorm(e.Label) is null)
            .Select(e => $"{e.Eltern}:{e.OriginalCode} {e.Label}").ToArray();

        const int festgehalten = 23;
        Assert.True(ohneNorm.Length <= festgehalten,
            $"{ohneNorm.Length} von {voll.Eintraege.Count} Schacht-Materialeintraegen ohne Normbegriff "
            + $"(festgehalten: {festgehalten}):\n" + string.Join("\n", ohneNorm));
    }
}
