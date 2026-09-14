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

    /// <summary>
    /// Ein ungueltiger optionaler Quellcode darf nie in die Ausgabe und darf sie auch nie
    /// sperren: Im Bestand des Kantons tragen zum Beispiel Einstiegshilfen die Art «1».
    /// Mit Zusatzmodell geht der Originalwert dort unveraendert mit, ohne bleibt er im Projekt.
    /// </summary>
    internal static void Trenne(DssExportObjekt o, List<string> hinweise, bool mitZusatzangaben = true)
    {
        foreach (var (key, text) in o.Werte.ToArray())
            if (IstAbweichung(o.Klasse, key, text))
            {
                o.Werte.Remove(key);
                hinweise.Add($"{o.Klasse} {o.Tid}: Quellwert {key} = «{text}» ist kein gültiger DSS-Auswahlcode. " + (mitZusatzangaben
                    ? "Unverändert als Quellabweichung in Erfasste_Angaben mitgeliefert; kein Normwert geraten."
                    : "Nicht in die Datei übernommen; der Originalwert bleibt in der Quelle. Kein Normwert geraten."));
            }
    }
}
