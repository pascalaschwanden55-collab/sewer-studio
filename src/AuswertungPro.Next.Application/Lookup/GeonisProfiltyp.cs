using System.Globalization;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Der Profiltyp-Code, wie GEONIS ihn am Rohrprofil fuehrt (<c>PROFILTYP</c>), in
/// der Normschreibweise von SIA405.
///
/// Quelle: Zuordnungstabelle <c>SIA405_Abwasser_2015_export.xlsx</c> der
/// GEONIS-Konfiguration (Blatt Rohrprofil.Profiltyp). Ein unbekannter Code liefert
/// <c>null</c> — dann darf der Export die Profilkennung nicht wiederverwenden, weil
/// nicht sicher ist, dass es dasselbe Profil ist.
/// </summary>
public static class GeonisProfiltyp
{
    public static string? NachNorm(string? code)
    {
        if (!int.TryParse((code ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var zahl))
            return null;

        return zahl switch
        {
            0 => "unbekannt",
            2 => "Kreisprofil",
            101 => "Eiprofil",
            103 => "Maulprofil",
            104 => "offenes_Profil",
            105 => "Rechteckprofil",
            106 => "Spezialprofil",
            _ => null
        };
    }
}
