namespace AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Mappt deutsche Klartext-Beobachtungen (wie in alten Fretz-2017-Protokollen, die Befunde ohne
/// VSA-Code als Text fuehren) auf VSA-Codes. Bewusst KONSERVATIV: nur eindeutige Grundgeruest-/
/// Verbindungs-Begriffe werden zugeordnet. Alles Mehrdeutige (Untersuchungs-Marker, Rohrmaterial-
/// wechsel) bleibt null, damit keine falschen Codes als Trainingsdaten entstehen.
///
/// Zentrales Domaenenwissen - nutzbar von allen PDF-/Protokoll-Pfaden (Training, Import, Verteilung),
/// statt in einem einzelnen Parser vergraben zu sein.
/// </summary>
public static class VsaObservationMap
{
    /// <summary>
    /// Liefert den VSA-Code zu einer deutschen Beobachtung oder null, wenn nicht eindeutig zuordenbar.
    /// </summary>
    public static string? MapGermanObservationToCode(string? observation)
    {
        if (string.IsNullOrWhiteSpace(observation))
            return null;

        var t = observation.Trim().ToLowerInvariant();
        if (t.Contains("rohranfang")) return "BCD";                  // Rohranfang
        if (t.Contains("rohrende")) return "BCE";                    // Rohrende
        if (t.Contains("bogen")) return "BCC";                       // Bogen (Richtungsaenderung)
        if (t.Contains("verschobene rohrverbindung")) return "BAJ";  // Verschobene Rohrverbindung (Knick)
        if (t.Contains("breite rohrverbindung")) return "BAJ";       // Breite Rohrverbindung
        return null;                                                 // bewusst nicht zugeordnet
    }
}
