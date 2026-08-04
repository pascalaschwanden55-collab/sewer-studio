using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>
/// DTOs fuer das .spro-Austauschformat der Android-App "SchachtPro" (ZIP mit JSON).
/// Spiegelt 1:1 die Kotlin-Modelle aus ProjectArchive.kt (formatVersion=1,
/// dbSchemaVersion=21). JSON-Namen sind exakt die Gson-Feldnamen der DTOs.
/// </summary>
internal static class SchachtProArchiveJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

internal sealed class ArchiveManifestDto
{
    [JsonPropertyName("formatVersion")] public int FormatVersion { get; set; } = 1;
    [JsonPropertyName("dbSchemaVersion")] public int DbSchemaVersion { get; set; } = 21;
    [JsonPropertyName("appVersionName")] public string? AppVersionName { get; set; }
    [JsonPropertyName("appVersionCode")] public int AppVersionCode { get; set; }
    [JsonPropertyName("exportedAtMillis")] public long ExportedAtMillis { get; set; }
    [JsonPropertyName("projectCount")] public int ProjectCount { get; set; }
    [JsonPropertyName("projects")] public List<ManifestProjectEntryDto>? Projects { get; set; }
}

internal sealed class ManifestProjectEntryDto
{
    [JsonPropertyName("exportId")] public string? ExportId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("protocolCount")] public int ProtocolCount { get; set; }
    [JsonPropertyName("photoCount")] public int PhotoCount { get; set; }
    [JsonPropertyName("hasLogo")] public bool HasLogo { get; set; }
}

internal sealed class ProjectDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("auftraggeberName")] public string? AuftraggeberName { get; set; }
    [JsonPropertyName("auftraggeberAddress")] public string? AuftraggeberAddress { get; set; }
    [JsonPropertyName("auftraggeberPhone")] public string? AuftraggeberPhone { get; set; }
    [JsonPropertyName("auftraggeberEmail")] public string? AuftraggeberEmail { get; set; }
    [JsonPropertyName("auftraggeberLogoArchivePath")] public string? AuftraggeberLogoArchivePath { get; set; }
    /// <summary>"PRO" oder "LITE". Default PRO wie in der App (aeltere Archive ohne Feld).</summary>
    [JsonPropertyName("mode")] public string? Mode { get; set; }
}

internal sealed class PhotoDto
{
    [JsonPropertyName("archivePath")] public string? ArchivePath { get; set; }
    [JsonPropertyName("rotation")] public double Rotation { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

internal sealed class AnschlussDto
{
    [JsonPropertyName("nr")] public int Nr { get; set; }
    [JsonPropertyName("typ")] public string? Typ { get; set; }
    [JsonPropertyName("medium")] public string? Medium { get; set; }
    [JsonPropertyName("dn")] public string? Dn { get; set; }
    [JsonPropertyName("tiefe")] public string? Tiefe { get; set; }
    [JsonPropertyName("material")] public string? Material { get; set; }
    [JsonPropertyName("uhr")] public string? Uhr { get; set; }
    [JsonPropertyName("richtung")] public string? Richtung { get; set; }
    [JsonPropertyName("rohrform")] public string? Rohrform { get; set; }
    [JsonPropertyName("breite")] public string? Breite { get; set; }
    [JsonPropertyName("hoehe")] public string? Hoehe { get; set; }
    [JsonPropertyName("zustand")] public Dictionary<string, bool>? Zustand { get; set; }
}

internal sealed class ProtocolDto
{
    [JsonPropertyName("schachtNr")] public string? SchachtNr { get; set; }
    [JsonPropertyName("datum")] public string? Datum { get; set; }
    [JsonPropertyName("wetter")] public string? Wetter { get; set; }
    [JsonPropertyName("schachtFunktion")] public string? SchachtFunktion { get; set; }
    [JsonPropertyName("dimension")] public string? Dimension { get; set; }
    [JsonPropertyName("laenge")] public string? Laenge { get; set; }
    [JsonPropertyName("breite")] public string? Breite { get; set; }
    [JsonPropertyName("tiefe")] public string? Tiefe { get; set; }
    [JsonPropertyName("materialSchacht")] public string? MaterialSchacht { get; set; }
    [JsonPropertyName("deckelMaterial")] public string? DeckelMaterial { get; set; }
    [JsonPropertyName("medium")] public string? Medium { get; set; }
    [JsonPropertyName("schachtform")] public string? Schachtform { get; set; }
    [JsonPropertyName("schachtRotation")] public double SchachtRotation { get; set; }
    [JsonPropertyName("bemerkungen")] public string? Bemerkungen { get; set; }
    [JsonPropertyName("include3dDiagram")] public bool Include3dDiagram { get; set; }
    [JsonPropertyName("doppelschacht")] public bool Doppelschacht { get; set; }
    [JsonPropertyName("doppelschachtRotation")] public double DoppelschachtRotation { get; set; }

    [JsonPropertyName("rahmenDeckelHoehe")] public string? RahmenDeckelHoehe { get; set; }
    [JsonPropertyName("deckelform")] public string? Deckelform { get; set; }
    [JsonPropertyName("deckelTyp")] public string? DeckelTyp { get; set; }
    [JsonPropertyName("belastungsklasse")] public string? Belastungsklasse { get; set; }
    [JsonPropertyName("deckelDurchmesser")] public string? DeckelDurchmesser { get; set; }

    [JsonPropertyName("schachthalsForm")] public string? SchachthalsForm { get; set; }
    [JsonPropertyName("schachthalsDimension")] public string? SchachthalsDimension { get; set; }
    [JsonPropertyName("schachthalsZwischenKonusHoehe")] public string? SchachthalsZwischenKonusHoehe { get; set; }

    [JsonPropertyName("konus")] public bool Konus { get; set; }
    [JsonPropertyName("konusExzentrisch")] public bool KonusExzentrisch { get; set; }
    [JsonPropertyName("konusHoehe")] public string? KonusHoehe { get; set; }
    [JsonPropertyName("konusForm")] public string? KonusForm { get; set; }
    [JsonPropertyName("konusDimension")] public string? KonusDimension { get; set; }

    [JsonPropertyName("schachtOberteilForm")] public string? SchachtOberteilForm { get; set; }
    [JsonPropertyName("schachthalsHoehe")] public string? SchachthalsHoehe { get; set; }
    [JsonPropertyName("schachtOberteilDimension")] public string? SchachtOberteilDimension { get; set; }

    [JsonPropertyName("schachtUnterteilForm")] public string? SchachtUnterteilForm { get; set; }
    [JsonPropertyName("schachtrohrHoehe")] public string? SchachtrohrHoehe { get; set; }
    [JsonPropertyName("schachtUnterteilDimension")] public string? SchachtUnterteilDimension { get; set; }

    [JsonPropertyName("schachtZustand")] public Dictionary<string, bool>? SchachtZustand { get; set; }
    [JsonPropertyName("deckelZustand")] public Dictionary<string, bool>? DeckelZustand { get; set; }
    [JsonPropertyName("deckelrahmenZustand")] public Dictionary<string, bool>? DeckelrahmenZustand { get; set; }
    [JsonPropertyName("schachthalsZustand")] public Dictionary<string, bool>? SchachthalsZustand { get; set; }
    [JsonPropertyName("konusZustand")] public Dictionary<string, bool>? KonusZustand { get; set; }
    [JsonPropertyName("schachtrohrZustand")] public Dictionary<string, bool>? SchachtrohrZustand { get; set; }
    [JsonPropertyName("bankettZustand")] public Dictionary<string, bool>? BankettZustand { get; set; }
    [JsonPropertyName("durchlaufrinneZustand")] public Dictionary<string, bool>? DurchlaufrinneZustand { get; set; }
    [JsonPropertyName("leiterSteigeisen")] public Dictionary<string, bool>? LeiterSteigeisen { get; set; }
    [JsonPropertyName("tauchbogen")] public Dictionary<string, bool>? Tauchbogen { get; set; }
    [JsonPropertyName("anschluesse")] public List<AnschlussDto>? Anschluesse { get; set; }
    [JsonPropertyName("zustandBemerkungen")] public Dictionary<string, string>? ZustandBemerkungen { get; set; }

    // ACHTUNG: LV95 Ost/Nord (CH1903+), KEIN WGS84 — so in der App dokumentiert.
    [JsonPropertyName("lv95East")] public double? Lv95East { get; set; }
    [JsonPropertyName("lv95North")] public double? Lv95North { get; set; }

    [JsonPropertyName("konusDurchmesserOben")] public string? KonusDurchmesserOben { get; set; }
    [JsonPropertyName("konusDurchmesserUnten")] public string? KonusDurchmesserUnten { get; set; }
    [JsonPropertyName("schachthalsDurchmesser")] public string? SchachthalsDurchmesser { get; set; }
    [JsonPropertyName("schachthalsZwischenKonusDurchmesser")] public string? SchachthalsZwischenKonusDurchmesser { get; set; }
    [JsonPropertyName("skizzeNotiz")] public string? SkizzeNotiz { get; set; }

    [JsonPropertyName("isShared")] public bool IsShared { get; set; }
    [JsonPropertyName("lastUpdated")] public long LastUpdated { get; set; }
    [JsonPropertyName("photos")] public List<PhotoDto>? Photos { get; set; }
}
