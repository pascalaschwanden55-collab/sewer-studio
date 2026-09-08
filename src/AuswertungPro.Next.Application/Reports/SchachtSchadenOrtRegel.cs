using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Die vier Zeichenzonen der Schachtgrafik (senkrechter Schnitt, von oben nach unten):
/// Konus, Schachtwand, Sohle und der Sonderfall Anschluss (Zulauf/Ablauf-Stutzen in der
/// Wand). <see cref="SchachtgrafikSvgBuilder"/> zeichnet Anschluss-Schaeden im selben
/// Wandbereich wie Schachtwand-Schaeden — physisch sitzt ein Anschluss in der Wand —,
/// behaelt die eigene Zone aber als eigenen Wert, weil ein spaeterer Aufrufer (z.B. eine
/// Statistik je Bauteil) sonst nicht mehr unterscheiden koennte.
/// </summary>
public enum SchachtZone
{
    Konus,
    Schachtwand,
    Sohle,
    Anschluss
}

/// <summary>
/// Ordnet einen Protokolleintrag des Schachts einer Zeichenzone der Schachtgrafik zu.
/// WPF-frei, wie <see cref="DamageSymbolClassifier"/>.
///
/// Drei Quellen liefern heute einen Ort, unter DREI verschiedenen Parameter-Schluesseln fuer
/// dieselbe fachliche Angabe: Der PDF-Schachtprotokollimport (<c>SchachtProtocolApplier</c>)
/// schreibt den Bauteilnamen direkt als <see cref="ProtocolEntry.Code"/> (z.B. "Konus",
/// "Bankett", "Schachtrohr"); der VSA-KEK-XTF-Import traegt ihn strukturiert als
/// <c>CodeMeta.Parameters["Schachtbereich"]</c> (Grossschreibung, ohne Punkt); das manuelle
/// VSA-Codierfenster (<c>ObservationCatalogWindow</c>/<c>VsaParameterMerger</c>) speichert
/// dieselbe Angabe dagegen unter <c>CodeMeta.Parameters["vsa.schachtbereich"]</c> (klein, mit
/// Punkt — die allgemeine Parameter-Konvention der Haltungs-Codierung). Beide Schluessel tragen
/// ein VSA-Kuerzel A/B/D/F/H/I/J. Fuer diese Kuerzel gibt es in dieser Codebasis keinen
/// belegten fachlichen Klartext — sie werden deshalb nicht gedeutet, sondern wie jeder
/// unbekannte Wert auf die Schachtwand gelegt. Keine erfundene Zuordnung.
/// </summary>
public static class SchachtSchadenOrtRegel
{
    /// <summary>Bestimmt die Zone eines Eintrags. Unbekannt oder leer ergibt Schachtwand.</summary>
    public static SchachtZone Bestimme(ProtocolEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var parameter = entry.CodeMeta?.Parameters;
        if (parameter is { Count: > 0 })
        {
            // "Ort" ist ein moeglicher freier Zielschluessel; "Schachtbereich" (VSA-KEK-Import)
            // und "vsa.schachtbereich" (manuelles VSA-Codierfenster) sind dieselbe fachliche
            // Angabe unter zwei verschiedenen Schreibweisen — siehe Klassendokumentation.
            foreach (var schluessel in new[] { "Ort", "Schachtbereich", "vsa.schachtbereich" })
            {
                if (parameter.TryGetValue(schluessel, out var wert) && AusText(wert) is { } ausParameter)
                    return ausParameter;
            }
        }

        return AusText(entry.Code) ?? AusText(entry.Beschreibung) ?? SchachtZone.Schachtwand;
    }

    private static SchachtZone? AusText(string? text)
    {
        var wert = (text ?? string.Empty).Trim();
        if (wert.Length == 0)
            return null;

        if (Enthaelt(wert, "konus") || Enthaelt(wert, "schachthals"))
            return SchachtZone.Konus;
        if (Enthaelt(wert, "sohle") || Enthaelt(wert, "bankett") || Enthaelt(wert, "durchlaufrinne") || Enthaelt(wert, "tauchbogen"))
            return SchachtZone.Sohle;
        if (Enthaelt(wert, "anschluss") || Enthaelt(wert, "zulauf") || Enthaelt(wert, "einlauf"))
            return SchachtZone.Anschluss;
        if (Enthaelt(wert, "schachtwand") || Enthaelt(wert, "schachtrohr") || Enthaelt(wert, "steigeisen") || Enthaelt(wert, "leiter"))
            return SchachtZone.Schachtwand;

        return null;
    }

    private static bool Enthaelt(string text, string suchbegriff)
        => text.Contains(suchbegriff, StringComparison.OrdinalIgnoreCase);
}
