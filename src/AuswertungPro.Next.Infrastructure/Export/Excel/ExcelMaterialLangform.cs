using System;
using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>
/// Schreibt Rohrmaterial fuer den Excel-Bericht in der VSA-Form aus.
///
/// Die Kanal-TV-Software liefert Kurzcodes ("PP", "STZ", "Z"), im Bericht soll die
/// ausgeschriebene Bezeichnung stehen. Umgeschrieben wird NUR die Anzeige - die
/// gespeicherten Projektdaten bleiben unveraendert, sonst wuerde ein Bericht die
/// Datenbasis umschreiben.
///
/// Die Schreibweisen selbst kommen aus <see cref="XtfValueNormalizer.NormalizeSiaMaterial"/>,
/// damit es dafuer nur eine Wahrheit gibt. Hier stehen ausschliesslich die Kurzcodes,
/// die jene Stelle nicht kennt.
/// </summary>
public static class ExcelMaterialLangform
{
    /// <summary>
    /// Kurzcodes aus Kanal-TV-Exporten. Bewusst knapp gehalten: Nur Codes, deren
    /// Bedeutung eindeutig ist. Alles Unsichere bleibt unveraendert stehen - ein
    /// Kurzcode im Bericht ist besser als eine erfundene Bezeichnung, die nach Norm
    /// aussieht.
    /// </summary>
    private static readonly Dictionary<string, string> Kurzcodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PP"] = "Polypropylen",
            ["PE"] = "Polyethylen",
            ["HDPE"] = "Hartpolyethylen",
            ["PEHD"] = "Hartpolyethylen",
            ["PVC"] = "Polyvinylchlorid",
            ["STZ"] = "Steinzeug",
            ["SZ"] = "Steinzeug",
            ["Z"] = "Zement",
            ["ZM"] = "Zement",
            ["FZ"] = "Faserzement",
            ["GUS"] = "Guss",
            ["GG"] = "Guss",
            ["GFK"] = "GFK",
            ["B"] = "Beton",
            ["BET"] = "Beton"
        };

    /// <summary>
    /// Liefert die ausgeschriebene Form. Unbekannte Werte kommen unveraendert zurueck.
    /// </summary>
    public static string Auflösen(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return string.Empty;

        var roh = wert.Trim();
        if (Kurzcodes.TryGetValue(roh, out var langform))
            return langform;

        // Die bekannten Langformen und Sonderfaelle wie "Beton_u" loest der zentrale
        // SIA-Normalisierer auf. Erkennt er nichts, gibt er den Wert leicht bereinigt
        // zurueck - dann bleibt es beim Original.
        var normalisiert = XtfValueNormalizer.NormalizeSiaMaterial(roh);
        return string.IsNullOrWhiteSpace(normalisiert) ? roh : normalisiert;
    }
}
