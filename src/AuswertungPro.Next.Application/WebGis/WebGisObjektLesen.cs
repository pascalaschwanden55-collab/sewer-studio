using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Die EINE Regel, wie ein Objekt im WebGIS gelesen wird (Entscheid Pascal 23.09.2026):
/// Ist seine GlobalID gespeichert, wird NUR ueber sie gelesen — nie ueber den Namen, auch nicht
/// als Rueckfall, wenn sie nicht lesbar ist. Nur ein Objekt ohne GlobalID wird ueber den Namen
/// gesucht. Anlass Zone 1.15: Ab 14:32 fand die Namenssuche nichts mehr, und alle 182 Objekte
/// waren gesperrt, obwohl ihre GlobalID laengst bekannt war.
/// </summary>
public static class WebGisObjektLesen
{
    public static Task<WebGisLesestand?> LiesAsync(
        IGeonisWebGisClient client, WebGisObjektart art, string bezeichnung, string? gespeicherteGlobalId,
        CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(gespeicherteGlobalId)
            ? client.LeseAsync(art, bezeichnung, ct)
            : client.LeseUeberGlobalIdAsync(art, gespeicherteGlobalId.Trim(), ct);

    /// <summary>Sperrgrund, wenn das Objekt nicht gelesen werden konnte.</summary>
    public static string NichtGefunden(string? gespeicherteGlobalId)
        => string.IsNullOrWhiteSpace(gespeicherteGlobalId)
            ? "Im WebGIS nicht eindeutig gefunden (kein oder mehrdeutiger Treffer)."
            : $"Objekt mit der gespeicherten GlobalID {gespeicherteGlobalId.Trim()} im WebGIS nicht lesbar — "
              + "nicht über den Namen ausgewichen. GlobalID prüfen.";

    /// <summary>Sperrgrund, wenn der Name im WebGIS nicht exakt dem Projektnamen entspricht; sonst null.</summary>
    public static string? NamensAbweichung(string projektName, WebGisLesestand stand)
        => string.Equals(projektName, stand.Bezeichnung, System.StringComparison.Ordinal)
            ? null
            : string.IsNullOrWhiteSpace(stand.Bezeichnung)
                ? $"Name im WebGIS nicht lesbar — Zuordnung zu «{projektName}» nicht bestätigt, nicht geschrieben."
                : $"Name im WebGIS «{stand.Bezeichnung}» stimmt nicht exakt mit dem Projektnamen «{projektName}» überein.";
}
