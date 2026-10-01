using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Eigentuemer und Betreiber fuehrt das WebGIS. Das bedeutet je Richtung etwas anderes, deshalb stehen hier zwei
/// getrennt benannte Regeln (Wartbarkeitsaudit 30.09.2026, WG-B). Vorher hiessen drei Methoden fast gleich (je
/// «WebGIS fuehrt» beim Senden, beim Holen und im Holen-Plan) und bedeuteten Gegenlaeufiges.
///
/// Die Feldmengen der beiden Richtungen sind heute VERSCHIEDEN und bleiben es (keine Vereinheitlichung ohne
/// Entscheid Pascal): Senden vergleicht gefaltet «Eigentuemer», «Eigentümer» und «Betreiber»; Holen erkennt als
/// Tabellenfeld nur <see cref="FieldKeys.Owner"/> zeichengenau, der Betreiber laeuft beim Holen ueber die Wurzelakte.
/// </summary>
public static class WebGisFuehrungsfelder
{
    private static readonly HashSet<string> NieSendenGefaltet = new(StringComparer.Ordinal)
    {
        WebGisHandwertKarte.Falte("Eigentuemer"), WebGisHandwertKarte.Falte("Eigentümer"), WebGisHandwertKarte.Falte("Betreiber"),
    };

    /// <summary>
    /// SENDEN: Dieses Feld geht nie aus SewerStudio ins WebGIS — weder als Handwert noch als Wert der Kanalfirma; der
    /// Bericht nennt einen Handwert mit «im WebGIS führend». Entscheid Pascal 21.09.2026, bestaetigt 23.09.2026 abends
    /// («Eigentum/Betreiber aendert das Programm im WebGIS NIE», docs/architektur/webgis.md). Gefaltet verglichen:
    /// «Eigentuemer», «Eigentümer», «Betreiber». Die letzte Sperre gegen die refIds steht in
    /// <see cref="WebGisGeschuetzteFelder"/>.
    /// </summary>
    public static bool NieSenden(string? feld) => NieSendenGefaltet.Contains(WebGisHandwertKarte.Falte(feld));

    /// <summary>
    /// HOLEN: Der WebGIS-Wert ersetzt beim Uebernehmen auch eine Handeingabe (bewusst leer eingeschlossen); die
    /// Handmarke weicht. Entscheid Pascal 24.09.2026 abends («Eigentuemer und Betreiber duerfen vom WebGIS
    /// ueberschrieben werden», docs/architektur/webgis.md). Als Tabellenfeld gilt das nur fuer den Eigentuemer
    /// (<see cref="FieldKeys.Owner"/>, zeichengenau). Der Betreiber ist beim Holen kein Tabellenfeld: Er geht in die
    /// Wurzelakte (<see cref="WebGisImportPlanBuilder.BetreiberFeld"/>, «haltung.operator»/«schacht.betreiber») und
    /// ersetzt dort eine Handeingabe in <c>WebGisImportUseCase.SchreibeAkteGruppe</c>.
    /// </summary>
    public static bool HolenUeberschreibtHand(string feld) => feld == FieldKeys.Owner;
}
