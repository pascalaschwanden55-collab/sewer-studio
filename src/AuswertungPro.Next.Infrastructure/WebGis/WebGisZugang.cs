using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.WebGis;

/// <summary>Ein Browser-Cookie der WebOffice-Sitzung (nur Name/Wert/Domain/Pfad, kein Ablauf).</summary>
public sealed record WebGisCookie(string Name, string Wert, string Domain, string Pfad);

/// <summary>
/// Die Sitzungsdaten, mit denen der Client den GEONIS-Attributeditor anspricht.
/// Sie entstehen nach der WebOffice-Anmeldung (siehe <see cref="GeonisWebOfficeLogin"/>)
/// und tragen genau die Werte, die die beobachteten Aufrufe als Query-Parameter
/// mitschicken: die JSESSIONID sowie den WebOffice-Benutzerkontext.
/// </summary>
public sealed class WebGisZugang
{
    /// <summary>Basishost, z. B. https://www.geohost.ch</summary>
    public required string BasisUrl { get; init; }

    /// <summary>WebOffice-Projekt, z. B. awu_abw_edit</summary>
    public required string Projekt { get; init; }

    /// <summary>GEONIS-Datenquelle/Projekt der Editor-Aufrufe, z. B. awu_abw</summary>
    public required string Datenquelle { get; init; }

    /// <summary>JSESSIONID der angemeldeten WebOffice-Sitzung.</summary>
    public required string JSessionId { get; init; }

    /// <summary>Interne synserver-Sitzungs-ID (session_id der Suche).</summary>
    public string? SynSessionId { get; init; }

    /// <summary>WebOffice-Login (X-syn-login), z. B. pascal.aschwanden</summary>
    public required string SynLogin { get; init; }

    /// <summary>X-syn-application-roles, woertlich "WebOffice+-+Editing" (Plus gehoert zum Wert, geht als %2B).</summary>
    public string SynRoles { get; init; } = "WebOffice+-+Editing";

    /// <summary>X-syn-groups, kommagetrennt, z. B. "G_awu_rw,G_awu_ro,G_awu_rw"</summary>
    public string SynGroups { get; init; } = "";

    /// <summary>
    /// Cookies der angemeldeten Browser-Sitzung (JSESSIONID, ADFS-Token …). Der HttpClient
    /// uebernimmt sie, damit die REST-Erweiterung dieselbe Sitzung sieht wie der Browser.
    /// </summary>
    public IReadOnlyList<WebGisCookie> Cookies { get; init; } = Array.Empty<WebGisCookie>();

    /// <summary>Endpunkt-Basis des GEONIS-Editors (REST-Erweiterung).</summary>
    public string EditorBasis =>
        BasisUrl.TrimEnd('/') +
        "/svc/rest/services/tn_system/gnsvc2023/MapServer/exts/GEONISserver2023/attributeeditor/";

    /// <summary>synserver-Endpunkt (Suche) inkl. jsessionid.</summary>
    public string SynServerUrl =>
        BasisUrl.TrimEnd('/') + "/divum/synserver;jsessionid=" + JSessionId;

    /// <summary>Die gemeinsamen Auth-Query-Parameter der Editor-Aufrufe.</summary>
    public string AuthQuery()
    {
        string enc(string s) => Uri.EscapeDataString(s);
        return "synergis_jsessionid=" + enc(JSessionId)
             + "&X-syn-login=" + enc(SynLogin)
             + "&X-syn-application-roles=" + enc(SynRoles)
             + "&X-syn-groups=" + enc(SynGroups);
    }
}
