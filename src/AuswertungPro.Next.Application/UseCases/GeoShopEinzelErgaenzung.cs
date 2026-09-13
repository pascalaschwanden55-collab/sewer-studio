using System;
using System.Linq;
using System.Text;
using System.Threading;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>«Fehlende Felder aus GeoShop-XTF» fuer EIN Bauteil (11.09.2026). Dieselben Regeln wie der
/// Abgleich der ganzen Seite - Planer und Anwender laufen mit genau einem Ziel -, aber die XTF wird nur
/// nach diesem einen Namen durchsucht, und die Vorschau ist ein kurzer Text statt eines Fensters.</summary>
public static class GeoShopEinzelErgaenzung
{
    public sealed record Ergebnis(GeoShopPlan Plan, GeoShopPosition? Position, string Text)
    {
        public bool HatAenderungen => Position is not null;
    }

    /// <summary>Liest und plant, schreibt nichts.</summary>
    public static Ergebnis Plane(GeoShopZiel ziel, IGeoShopLeser leser, string datei, CancellationToken cancellationToken = default)
    {
        var bestand = leser.Lies(datei, ziel.Art, [ziel.Name], cancellationToken);
        var plan = GeoShopAbgleichPlanBuilder.Baue([ziel], bestand);
        var position = plan.Positionen.SingleOrDefault();
        return new Ergebnis(plan, position, Beschreibe(ziel, plan, position));
    }

    /// <summary>Schreibt mit derselben Stand-Pruefung wie der grosse Abgleich; 1 bei Erfolg.</summary>
    public static int WendeAn(Ergebnis ergebnis, GeoShopZiel aktuellesZiel)
        => GeoShopAbgleichAnwender.WendeAn(ergebnis.Plan, [aktuellesZiel]);

    public static string Beschreibe(GeoShopZiel ziel, GeoShopPlan plan, GeoShopPosition? position)
    {
        var art = ziel.Art == BauteilArt.Haltung ? "Haltung" : "Schacht";
        var text = new StringBuilder();
        if (position is null)
        {
            text.AppendLine($"{art} «{ziel.Name}»: nichts zu übernehmen.");
            foreach (var hinweis in plan.Hinweise) text.AppendLine(hinweis);
            text.Append($"Quelle: {plan.Quelle}");
            return text.ToString();
        }
        var fach = position.Felder.Where(f => !f.IstKennung).ToArray();
        text.AppendLine($"{art} «{ziel.Name}»{(position.Gedreht ? " (Gegenrichtung)" : "")}: "
            + $"{fach.Count(f => !f.Ersetzen)} leere Felder ergänzen, {fach.Count(f => f.Ersetzen)} ersetzen"
            + (position.KennungenAendern ? ", Kennungen übernehmen" : "")
            + (position.NeueAktenwerte ? ", Objektakte ergänzen" : "") + ".");
        foreach (var f in fach)
            text.AppendLine($"  {FieldCatalog.Get(f.Feld).Label}: {(string.IsNullOrWhiteSpace(f.Vorher) ? "(leer)" : f.Vorher)} → {f.Nachher}"
                + (f.Ersetzen ? "  (kommt immer aus der XTF)" : ""));
        foreach (var hinweis in plan.Hinweise) text.AppendLine(hinweis);
        text.AppendLine($"Quelle: {plan.Quelle}");
        text.Append("Gefüllte Felder bleiben erhalten. Danach das Projekt speichern.");
        return text.ToString();
    }
}
