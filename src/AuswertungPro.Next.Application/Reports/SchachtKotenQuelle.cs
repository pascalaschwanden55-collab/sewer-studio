using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>Koten in m ue. M. aus dem Kataster: Deckel, Sohle und die Rohrsohle je Haltung am Schacht.</summary>
public sealed record SchachtKoten(decimal? Deckel, decimal? Sohle, IReadOnlyDictionary<string, decimal> JeHaltung)
{
    /// <summary>Deckel minus Sohle, wenn beide bekannt sind und der Deckel oben liegt.</summary>
    public decimal? Tiefe => Deckel is { } d && Sohle is { } s && d >= s ? d - s : null;

    public bool IstLeer => Deckel is null && Sohle is null && JeHaltung.Count == 0;
}

/// <summary>
/// Liest die Koten eines Schachts aus den Objektakten des GeoShop-Abgleichs: die Deckelkote am
/// eindeutigen Hauptdeckel (<see cref="SchachtDeckelAnzeige.Waehle"/>), die Sohlenkote an der
/// Schachtakte und je angeschlossener Haltung die Kote ihres Haltungspunkts an diesem Schacht
/// (<c>haltung.fromlevel</c>, wenn der Schacht oben liegt, sonst <c>haltung.tolevel</c>).
///
/// Rein lesend, WPF-frei. Ohne Objektakten (Projekte ohne GeoShop-Abgleich) gibt es
/// <c>null</c>; die Schachtgrafik zeigt dann keinen Kotenkasten und erfindet keinen.
/// </summary>
public static class SchachtKotenQuelle
{
    public static SchachtKoten? Lies(Project? projekt, SchachtRecord? schacht, IReadOnlyList<HaltungRecord>? haltungen)
    {
        if (projekt is null || schacht is null || projekt.Objektakten.Count == 0)
            return null;

        decimal? deckel = null;
        decimal? sohle = null;
        var wurzel = projekt.Objektakten.FirstOrDefault(a => a.Id == schacht.Id && a.Art == "schacht");
        if (wurzel is not null)
        {
            var deckelAkte = SchachtDeckelAnzeige.Waehle(projekt, wurzel);
            deckel = Zahl(deckelAkte?.Werte.GetValueOrDefault("deckel.hoehe")?.Text)
                     ?? Zahl(wurzel.Werte.GetValueOrDefault("schacht.deckelhoehe")?.Text);
            sohle = Zahl(wurzel.Werte.GetValueOrDefault("schacht.sohlenhoehe")?.Text);
        }

        var nummer = schacht.GetFieldValue(SchachtFeldnamen.Feld(schacht, "Schachtnummer")).Trim();
        var jeHaltung = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var haltung in haltungen ?? [])
        {
            var name = (haltung.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            if (name.Length == 0)
                continue;

            var feld = SchachtHaltungsseite.Bestimme(haltung, nummer) switch
            {
                Haltungsseite.Oben => "haltung.fromlevel",
                Haltungsseite.Unten => "haltung.tolevel",
                _ => null
            };
            if (feld is null)
                continue;

            var akte = projekt.Objektakten.FirstOrDefault(a => a.Id == haltung.Id && a.Art == "haltung");
            if (Zahl(akte?.Werte.GetValueOrDefault(feld)?.Text) is { } kote)
                jeHaltung[name] = kote;
        }

        var koten = new SchachtKoten(deckel, sohle, jeHaltung);
        return koten.IstLeer ? null : koten;
    }

    private static decimal? Zahl(string? text)
        => FachzahlParser.TryParseMeasurement(text, out var wert) && wert > 0 ? wert : null;
}
