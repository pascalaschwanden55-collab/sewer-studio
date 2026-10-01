using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>Eine Schadensart der Haltung mit Anzahl und Streckenkennzeichen.</summary>
public sealed record SchadensMerkmal(string Hauptcode, int Anzahl, bool HatStrecke);

/// <summary>
/// Die Frage eines Falls: Was zeichnet diese Haltung aus?
/// Bewusst schmal — jedes weitere Merkmal muss sich in einer Messung beweisen.
/// </summary>
public sealed record KostenfallMerkmale
{
    public int? DnMm { get; init; }
    public double LaengeM { get; init; }
    public int BogenAnzahl { get; init; }

    /// <summary>Seitliche Anschluesse (BCA). Kein Schaden, aber Mengentreiber.</summary>
    public int AnschlussAnzahl { get; init; }

    public IReadOnlyList<SchadensMerkmal> Schaeden { get; init; } = [];

    public IReadOnlyList<string> Schadensarten =>
        Schaeden.Select(s => s.Hauptcode).OrderBy(c => c, StringComparer.Ordinal).ToList();

    public bool HatBogen => BogenAnzahl > 0;
}

/// <summary>Eine Position des Massnahmenpakets — Menge ohne Preis.</summary>
public sealed record MassnahmePosition(string ItemKey, decimal Menge, string Einheit);

/// <summary>Woher ein Fall stammt — entscheidet, ob er gemessen werden darf.</summary>
public enum KostenfallHerkunft
{
    /// <summary>Der Vorschlag war verdeckt. Zaehlt zum Lernen UND zur Messung.</summary>
    Unbeeinflusst = 0,

    /// <summary>Der Vorschlag war vorher sichtbar. Zaehlt nur zum Lernen.</summary>
    VorschlagGesehen = 1
}

/// <summary>Ein gelernter Fall: Merkmale und das vom Menschen bestaetigte Paket.</summary>
public sealed record Kostenfall
{
    public string Haltung { get; init; } = "";
    public string Projekt { get; init; } = "";
    public DateTime ErfasstUtc { get; init; }
    public KostenfallHerkunft Herkunft { get; init; }
    public KostenfallMerkmale Merkmale { get; init; } = new();
    public IReadOnlyList<MassnahmePosition> Positionen { get; init; } = [];
}

/// <summary>Warum kein Vorschlag moeglich war.</summary>
public enum EnthaltungsGrund
{
    Kein = 0,
    ZuWenigeFaelle,
    DurchmesserUnbekannt,
    BogenNichtGelernt,
    NachbarnUneinig
}

/// <summary>Das Ergebnis fuer eine Haltung — entweder Positionen oder ein Grund.</summary>
public sealed record KostenVorschlag
{
    public IReadOnlyList<MassnahmePosition> Positionen { get; init; } = [];
    public int HerangezogeneFaelle { get; init; }
    public EnthaltungsGrund Grund { get; init; }
    public string GrundText { get; init; } = "";

    public bool IstEnthaltung => Grund != EnthaltungsGrund.Kein;

    public static KostenVorschlag Enthaltung(EnthaltungsGrund grund, string text)
        => new() { Grund = grund, GrundText = text };
}
