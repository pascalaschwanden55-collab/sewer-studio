using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Projects;

public sealed class ObjektaktenPaket
{
    public string Format { get; set; } = "SewerStudio.Objektakten";
    public int Version { get; set; } = 1;
    public Guid ProjektId { get; set; }
    public string Katalogstand { get; set; } = "";
    public List<ObjektAkte> Akten { get; set; } = [];
    public Dictionary<Guid, Dictionary<string, string>> Haltungsfelder { get; set; } = new();
    public Dictionary<Guid, Dictionary<string, string>> Schachtfelder { get; set; } = new();
    public Dictionary<Guid, Dictionary<string, FieldMetadata>> Haltungsmetadaten { get; set; } = new();
    public Dictionary<Guid, Dictionary<string, FieldMetadata>> Schachtmetadaten { get; set; } = new();
    public string Uebertragungsbericht { get; set; } = "";
}

public interface IObjektaktenPaketService
{
    /// <summary>Neue Datei, nie ein vorhandenes Ziel ersetzen. Keine GEONIS-Importzusage.</summary>
    void Exportiere(Project projekt, string ziel);
    ObjektaktenPaket Lies(string datei);
}
