using System.Reflection;
using AuswertungPro.Next.Infrastructure.Import.SchachtPro;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Drei Importwege suchen denselben Schacht ueber dieselben Felder. Driftet eine der
/// Listen, legt derselbe Schacht plötzlich zwei Datensaetze an — und niemand merkt es,
/// weil jeder Weg fuer sich funktioniert.
///
/// Genau dieses Muster hat beim WinCan-Sammelordner-Import schon einmal zugeschlagen:
/// Die Erkennung schloss <c>_Meta.db3</c> aus, der Importer nicht, und beide Regeln
/// standen an verschiedenen Stellen. Ergebnis waren null importierte Haltungen.
/// </summary>
public sealed class XtfSchachtSchluesselfelderTests
{
    private static string[] Lies(Type typ, string feldname)
    {
        var feld = typ.GetField(feldname, BindingFlags.NonPublic | BindingFlags.Static)
                   ?? typ.GetField(feldname, BindingFlags.Public | BindingFlags.Static);

        Assert.True(feld is not null,
            $"{typ.Name}.{feldname} gibt es nicht mehr. Wurde die Liste umbenannt oder " +
            "entfernt, muss dieser Waechter mitgezogen werden — sonst bewacht er nichts.");

        var wert = feld!.GetValue(null) as string[];
        Assert.True(wert is not null, $"{typ.Name}.{feldname} ist kein string[].");
        return wert!;
    }

    [Fact]
    public void Der_XTF_Weg_sucht_ueber_dieselben_Felder_wie_WinCan_und_SchachtPro()
    {
        var xtf = LegacyXtfImportService.SchachtSchluesselfelder;
        var winCan = Lies(typeof(WinCanDbImportService), "SchachtKeyFields");
        var schachtPro = Lies(typeof(SchachtProImportService), "SchachtKeyFields");

        // Jedes Feld der beiden bestehenden Wege muss der XTF-Weg auch kennen. Sonst
        // findet er einen bereits importierten Schacht nicht wieder und legt ihn neu an.
        foreach (var feld in winCan)
            Assert.Contains(feld, xtf);

        foreach (var feld in schachtPro)
            Assert.Contains(feld, xtf);
    }

    [Fact]
    public void Die_Nummernfelder_des_Schreibwegs_stehen_auch_im_Suchweg()
    {
        // Der XTF-Import schreibt die Nummer in drei Felder. Stuende eines davon nicht
        // in der Suchliste, faende ein zweiter Lauf seinen eigenen Schacht nicht wieder.
        foreach (var feld in Next.Application.Xtf.XtfNormschachtStammdaten.Nummernfelder)
            Assert.Contains(feld, LegacyXtfImportService.SchachtSchluesselfelder);
    }
}
