using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Unzulässige optionale Quellcodes separat erhalten, niemals als Normwerte ausgeben.</summary>
internal static class DssQuellabweichungen
{
    internal static bool IstAbweichung(string klasse, string feld, string text)
    {
        if (DssExportSchema.Felder(klasse)?.GetValueOrDefault(feld) is not { Kind: "Enum", Required: false }) return false;
        try { DssExportSchema.Normalisiere(klasse, feld, text); return false; }
        catch (InvalidOperationException) { return true; }
    }

    internal static void Trenne(DssExportObjekt o, List<string> hinweise)
    {
        foreach (var (key, text) in o.Werte.ToArray())
            if (IstAbweichung(o.Klasse, key, text))
            {
                o.Werte.Remove(key);
                hinweise.Add($"{o.Klasse} {o.Tid}: Quellwert {key} = «{text}» ist kein gültiger DSS-Auswahlcode. Unverändert als Quellabweichung in Erfasste_Angaben mitgeliefert; kein Normwert geraten.");
            }
    }
}
