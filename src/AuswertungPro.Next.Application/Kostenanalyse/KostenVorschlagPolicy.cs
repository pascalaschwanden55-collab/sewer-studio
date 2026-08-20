using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Entscheidet, ob ueberhaupt vorgeschlagen wird — und schweigt sonst mit Begruendung.
///
/// Das ist das wichtigste Bauteil der Kostenanalyse: Eine erfundene Zahl in einer Offerte
/// richtet mehr Schaden an als eine fehlende. Die Schwellen sind begruendete Startwerte
/// und werden mit der Rueckblick-Messung ueberprueft — sie duerfen nie gesenkt werden,
/// nur damit eine Kennzahl besser aussieht.
/// </summary>
public static class KostenVorschlagPolicy
{
    public const int MindestNachbarn = 3;
    public const int MaximalNachbarn = 7;
    public const int MindestBogenFaelle = 10;

    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    public static KostenVorschlag Schlage(KostenfallMerkmale ziel, IReadOnlyList<Kostenfall> faelle)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(faelle);

        // Eine Nennweite ausserhalb des Katalogs waere reine Hochrechnung ins Blaue.
        if (ziel.DnMm is not > 0
            || KostenfallAehnlichkeit.DnStufenAbstand(ziel.DnMm.Value, ziel.DnMm.Value) is null)
        {
            return KostenVorschlag.Enthaltung(
                EnthaltungsGrund.DurchmesserUnbekannt,
                $"Durchmesser {ziel.DnMm?.ToString(Ch) ?? "unbekannt"} ist keine bekannte Nennweite.");
        }

        if (ziel.HatBogen)
        {
            var bogenfaelle = faelle.Count(f => f.Merkmale.HatBogen);
            if (bogenfaelle < MindestBogenFaelle)
            {
                return KostenVorschlag.Enthaltung(
                    EnthaltungsGrund.BogenNichtGelernt,
                    $"Haltung hat einen Bogen, gelernt sind erst {bogenfaelle} Bogenfaelle "
                    + $"(noetig: {MindestBogenFaelle}).");
            }
        }

        var nachbarn = KostenfallAehnlichkeit.FindeNachbarn(ziel, faelle, MaximalNachbarn);
        if (nachbarn.Count < MindestNachbarn)
        {
            return KostenVorschlag.Enthaltung(
                EnthaltungsGrund.ZuWenigeFaelle,
                $"Zu wenig Erfahrung: nur {nachbarn.Count} aehnliche Faelle "
                + $"(noetig: {MindestNachbarn}).");
        }

        var positionen = KostenVorschlagRechner.Rechne(ziel, nachbarn);
        if (positionen.Count == 0)
        {
            return KostenVorschlag.Enthaltung(
                EnthaltungsGrund.NachbarnUneinig,
                $"Die {nachbarn.Count} aehnlichen Faelle haben keine gemeinsame Massnahme.");
        }

        return new KostenVorschlag
        {
            Positionen = positionen,
            HerangezogeneFaelle = nachbarn.Count,
            Grund = EnthaltungsGrund.Kein
        };
    }
}
