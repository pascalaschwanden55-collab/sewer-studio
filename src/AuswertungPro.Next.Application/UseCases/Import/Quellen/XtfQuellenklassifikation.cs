using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.Import.Quellen;

/// <summary>Welche INTERLIS-Modellfamilie eine XTF-Datei fuehrt.</summary>
public enum XtfModellfamilie
{
    /// <summary>Weder VSA_KEK noch SIA405 im Kopf — fuer den Import unbrauchbar.</summary>
    Unbekannt = 0,

    /// <summary>Nur VSA_KEK (Zustandserfassung).</summary>
    VsaKek = 1,

    /// <summary>Nur SIA405 (Kataster/Stammdaten).</summary>
    Sia405 = 2,

    /// <summary>Beide. Der Normalfall bei WinCan: VSA_KEK referenziert SIA405 als Abhaengigkeit.</summary>
    VsaKekUndSia405 = 3
}

/// <summary>Was in der Datei fachlich wirklich drinsteht.</summary>
public enum XtfQuellenart
{
    /// <summary>Kein verwertbarer Fachinhalt gefunden.</summary>
    Unbekannt = 0,

    /// <summary>Untersuchungen und/oder Schaeden — eine Inspektionsdatei.</summary>
    Inspektion = 1,

    /// <summary>Nur Bauwerke ohne Untersuchung — eine reine Katasterdatei.</summary>
    Kataster = 2,

    /// <summary>Beides in einer Datei.</summary>
    InspektionUndKataster = 3
}

/// <summary>
/// Was eine XTF-Datei enthaelt. Bewusst reine Zahlen: Die Regel darunter entscheidet,
/// das Lesen der Datei bleibt in der Infrastruktur.
/// </summary>
/// <param name="Modellnamen">MODEL/@NAME aus der HEADERSECTION, in Reihenfolge.</param>
/// <param name="Untersuchungen">Anzahl <c>*.Untersuchung</c>-Elemente.</param>
/// <param name="Kanalschaeden">Anzahl <c>*.Kanalschaden</c>-Elemente.</param>
/// <param name="Normschachtschaeden">Anzahl <c>*.Normschachtschaden</c>-Elemente.</param>
/// <param name="Stammdatenobjekte">Bauwerke: Kanal, Haltung, Normschacht, Abwasserknoten, Rohrprofil.</param>
/// <param name="Lesefehler">Gesetzt, wenn die Datei nicht als XML gelesen werden konnte.</param>
public sealed record XtfQuellenmerkmale(
    IReadOnlyList<string> Modellnamen,
    int Untersuchungen,
    int Kanalschaeden,
    int Normschachtschaeden,
    int Stammdatenobjekte,
    string? Lesefehler = null)
{
    /// <summary>Anzahl <c>*.Datei</c>-Elemente — die Foto- und Videoverweise des Exports.</summary>
    public int Dateiverweise { get; init; }

    /// <summary>
    /// Fingerabdruck ueber die enthaltenen Untersuchungs-Kennungen. Zwei Exporte
    /// derselben Zone tragen denselben Wert, auch wenn ihre Dateigroesse abweicht.
    /// Leer, wenn die Datei keine Untersuchung enthaelt.
    /// </summary>
    public string Untersuchungsfingerabdruck { get; init; } = "";

    /// <summary>
    /// SHA-256-Mehrfachmenge der vollständigen XML-Objekte samt TIDs/Verweisen und
    /// Modell-/Basket-Kontext. Nur diese Belege erlauben einen Teilmengenvergleich.
    /// Null bedeutet ungeprüft; reine Zählwerte beweisen keine Inhaltsgleichheit.
    /// </summary>
    public IReadOnlyDictionary<string, int>? Inhaltsbelege { get; init; }

    public static XtfQuellenmerkmale NichtLesbar(string grund)
        => new(Array.Empty<string>(), 0, 0, 0, 0, grund);
}

/// <summary>
/// Ordnet eine XTF-Datei nach Modellfamilie UND Fachinhalt ein.
///
/// Anlass (Audit 2026-09-05): Die bisherige Erkennung suchte im Textkopf nach der
/// Zeichenfolge <c>VSA_KEK_2020_LV95</c>. Damit fielen alle aelteren WinCan-Exporte
/// durch — sie fuehren <c>VSA_KEK</c> in der Fassung 15.07.2008. Und weil im selben
/// Kopf auch <c>SIA405_Abwasser</c> steht, galt so eine Datei zusaetzlich als
/// SIA405-Datei: Die SIA-Abhaengigkeit versteckte die Untersuchungen.
///
/// Zwei Regeln daraus:
/// 1. Die Modellfamilie kommt aus dem NAMEN, nicht aus der Version.
/// 2. Was die Datei IST, entscheidet ihr Inhalt — nicht die Reihenfolge im Kopf.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class XtfQuellenklassifikation
{
    public static XtfModellfamilie Familie(XtfQuellenmerkmale merkmale)
    {
        ArgumentNullException.ThrowIfNull(merkmale);

        var vsa = merkmale.Modellnamen.Any(IstVsaKek);
        var sia = merkmale.Modellnamen.Any(IstSia405);

        return (vsa, sia) switch
        {
            (true, true) => XtfModellfamilie.VsaKekUndSia405,
            (true, false) => XtfModellfamilie.VsaKek,
            (false, true) => XtfModellfamilie.Sia405,
            _ => XtfModellfamilie.Unbekannt
        };
    }

    public static XtfQuellenart Art(XtfQuellenmerkmale merkmale)
    {
        ArgumentNullException.ThrowIfNull(merkmale);
        if (merkmale.Lesefehler is not null)
            return XtfQuellenart.Unbekannt;

        var inspektion = merkmale.Untersuchungen > 0
                         || merkmale.Kanalschaeden > 0
                         || merkmale.Normschachtschaeden > 0;
        var kataster = merkmale.Stammdatenobjekte > 0;

        return (inspektion, kataster) switch
        {
            (true, true) => XtfQuellenart.InspektionUndKataster,
            (true, false) => XtfQuellenart.Inspektion,
            (false, true) => XtfQuellenart.Kataster,
            _ => XtfQuellenart.Unbekannt
        };
    }

    /// <summary>Enthaelt die Datei Inspektionsdaten, die der Import verarbeiten kann?</summary>
    public static bool IstInspektionsquelle(XtfQuellenmerkmale merkmale)
        => Art(merkmale) is XtfQuellenart.Inspektion or XtfQuellenart.InspektionUndKataster;

    /// <summary>Eine Zeile fuer den Importbericht: was ist das, und warum.</summary>
    public static string Begruendung(XtfQuellenmerkmale merkmale)
    {
        ArgumentNullException.ThrowIfNull(merkmale);
        if (merkmale.Lesefehler is not null)
            return $"nicht lesbar — {merkmale.Lesefehler}";

        var modelle = merkmale.Modellnamen.Count > 0
            ? string.Join(", ", merkmale.Modellnamen)
            : "kein Modell im Kopf";

        return Art(merkmale) switch
        {
            XtfQuellenart.Inspektion =>
                $"Inspektionsdaten ({merkmale.Untersuchungen} Untersuchungen, "
                + $"{merkmale.Kanalschaeden} Kanalschaeden, {merkmale.Normschachtschaeden} Schachtschaeden) — {modelle}",
            XtfQuellenart.InspektionUndKataster =>
                $"Inspektions- und Katasterdaten ({merkmale.Untersuchungen} Untersuchungen, "
                + $"{merkmale.Stammdatenobjekte} Bauwerke) — {modelle}",
            XtfQuellenart.Kataster =>
                $"reine Katasterdaten ({merkmale.Stammdatenobjekte} Bauwerke, keine Untersuchung) — {modelle}",
            _ => $"kein verwertbarer Fachinhalt — {modelle}"
        };
    }

    private static bool IstVsaKek(string modellname)
        => modellname.StartsWith("VSA_KEK", StringComparison.OrdinalIgnoreCase);

    private static bool IstSia405(string modellname)
        => modellname.StartsWith("SIA405", StringComparison.OrdinalIgnoreCase);
}
