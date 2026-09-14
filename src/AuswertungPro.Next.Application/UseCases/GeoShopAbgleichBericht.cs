using System.Linq;
using System.Text;

namespace AuswertungPro.Next.Application.UseCases;

public static class GeoShopAbgleichBericht
{
    public static string Schreibe(GeoShopPlan plan)
    {
        var text = new StringBuilder();
        text.AppendLine($"Quelle: {plan.Quelle}");
        var vergleich = plan.Positionen.Any(p => p.Vergleich is not null);
        text.AppendLine($"{plan.Positionen.Count} Bauteile ändern; " +
            $"{plan.Positionen.Sum(p => p.Felder.Count(f => !f.IstKennung && !f.Ersetzen))} leere Fachfelder ergänzen; " +
            $"{plan.Positionen.Sum(p => p.Felder.Count(f => f.Ersetzen))} Haltungslängen aus der XTF ersetzen.");
        if (vergleich) text.AppendLine($"Zusätzlich {plan.Positionen.Sum(p => p.Vergleich?.Felder.Count ?? 0)} Ergänzungen und Abweichungen zur Auswahl in der Tabelle.");
        text.AppendLine(vergleich
            ? "Die Tabelle zeigt Ergänzungen und Abweichungen. Nur angehakte GeoShop-Werte werden übernommen. Handwerte (auch bewusst leere) bleiben geschützt. Die Haltungslänge kommt weiterhin immer aus der XTF."
            : "Gefüllte Fachfelder bleiben erhalten; nur die Haltungslänge kommt immer aus der XTF. Es werden keine neuen Haltungen oder Schächte angelegt.");
        if (vergleich) text.AppendLine("Vor der Übernahme wird der vollständige aktuelle Projektstand unter SewerStudio/GeoShop-Sicherungen gesichert. Ein Fehler stoppt die Übernahme. Nicht gelieferte Werte werden niemals aus einer anderen Angabe geraten.");
        text.AppendLine("Kennungen werden für den XTF-Export übernommen. Das ist noch keine Freigabe für den GEONIS-Rückimport.");
        text.AppendLine("Die XTF liefert kein bestätigtes GN_LAST_EDITED_DATE. Der Konfliktabgleich mit Trigonet bleibt offen.");
        foreach (var p in plan.Positionen)
        {
            text.AppendLine();
            text.AppendLine($"--- {p.Ziel.Name}{(p.Gedreht ? " (Gegenrichtung, Punkte werden getauscht)" : "")} ---");
            if (p.KennungenAendern)
            {
                text.AppendLine("Bisherige Verknüpfungen:"); text.AppendLine(p.AlteKennungen);
                text.AppendLine("Verknüpfungen aus GeoShop:"); text.AppendLine(p.NeueKennungen);
            }
            foreach (var f in p.Felder)
                text.AppendLine($"{f.Feld}: {(string.IsNullOrWhiteSpace(f.Vorher) ? "(leer)" : f.Vorher)} → {f.Nachher}"
                    + (f.Ersetzen ? " (ersetzt – kommt immer aus der XTF)" : ""));
            if (p.Vergleich is { } v)
            {
                text.AppendLine($"{v.Felder.Count} Felder in der Vergleichstabelle. Bei einem einzigen zugehörigen Deckel wird dessen Höhe angezeigt; bei mehreren bitte den Hauptdeckel in der Objektakte wählen. Aus zwei vorhandenen Angaben (Deckelhöhe, Sohlenhöhe, Tiefe) wird die dritte im leeren Feld berechnet. Handwerte bleiben geschützt.");
                foreach (var h in v.Hinweise) text.AppendLine(h);
            }
            if (p.NeueAktenwerte)
            {
                text.AppendLine("Objektakte: Originalwerte und verknüpfte Deckel/Ereignisse ergänzen (vorhandene Handwerte bleiben).");
                foreach (var q in p.Quelle.Quellen ?? [])
                    text.AppendLine($"  {q.Klasse} {q.Kennung}: " + string.Join("; ", q.Werte.Select(w => w.Key + "=" + w.Value)));
            }
        }
        if (plan.Hinweise.Count > 0)
        {
            text.AppendLine(); text.AppendLine("Ohne Übernahme / zur Prüfung:");
            foreach (var hinweis in plan.Hinweise) text.AppendLine(hinweis);
        }
        text.AppendLine();
        text.AppendLine("Nach der Übernahme das Projekt speichern. Die Quelldatei bleibt unverändert.");
        return text.ToString();
    }
}
