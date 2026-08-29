namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die Funktion eines Schachts — mit den Begriffen der Norm, an einer Stelle.
///
/// Massgebend ist die Modelldatei SIA405_Abwasser_2020_2_d_LV95 (22 Werte).
/// Der AWU-Bestand belegt davon 13; die neun uebrigen sind gueltig, kommen in
/// Uri aber nicht vor. Siehe docs/SIA405-2020-Wertelisten.md.
///
/// Aufbau wie <see cref="MaterialVokabular"/>: gelesene Schreibweisen, Begriff im
/// Programm, Schreibweise fuer die Datei. Der Begriff im Programm bleibt der
/// fachlich genaue — nur die Datei vergroebert, wo die Norm nichts Genaueres kennt.
/// Ein Sickerschacht heisst in SewerStudio also weiterhin Sickerschacht.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class SchachtFunktionVokabular
{
    private sealed record Konzept(string[] Gelesen, string App, string Norm);

    private static readonly Konzept[] Konzepte =
    [
        // --- im AWU-Bestand belegt ---
        new(["kontroll_einsteigschacht", "kontrollschacht", "einsteigschacht",
             "kontroll-/einsteigschacht", "kontroll/einsteigschacht",
             // "Einstiegschacht" mit ie - so steht es im Projekt Zone 1.15.
             "einstiegschacht"],
            "Kontrollschacht", "Kontroll_Einsteigschacht"),
        new(["schlammsammler"], "Schlammsammler", "Schlammsammler"),
        // Entscheid Pascal: beide SchachtPro-Einlaufschaechte gehen auf Einlaufschacht.
        // Der Schlammsammler wird dadurch nicht doppelt gezaehlt, "Schluck" entfaellt.
        new(["einlaufschacht", "einlaufschacht mit schlammsammler", "einlaufschacht schluck"],
            "Einlaufschacht", "Einlaufschacht"),
        new(["dachwasserschacht"], "Dachwasserschacht", "Dachwasserschacht"),
        new(["spuelschacht", "spülschacht"], "Spülschacht", "Spuelschacht"),
        new(["oelabscheider", "ölabscheider"], "Ölabscheider", "Oelabscheider"),
        new(["pumpwerk", "pumpenschacht"], "Pumpwerk", "Pumpwerk"),
        new(["trennbauwerk"], "Trennbauwerk", "Trennbauwerk"),
        new(["geleiseschacht"], "Geleiseschacht", "Geleiseschacht"),
        new(["absturzbauwerk"], "Absturzbauwerk", "Absturzbauwerk"),
        new(["entwaesserungsrinne", "entwässerungsrinne"],
            "Entwässerungsrinne", "Entwaesserungsrinne"),
        new(["unbekannt"], "unbekannt", "unbekannt"),
        new(["andere"], "andere", "andere"),

        // --- im Modell vorhanden, im AWU-Bestand nicht benutzt ---
        new(["be_entlueftung", "be-/entlüftung", "be/entlueftung"],
            "Be-/Entlüftung", "Be_Entlueftung"),
        new(["behandlungsanlage"], "Behandlungsanlage", "Behandlungsanlage"),
        new(["bodenablauf"], "Bodenablauf", "Bodenablauf"),
        new(["entwaesserungsrinne_mit_schlammsack", "entwässerungsrinne mit schlammsack"],
            "Entwässerungsrinne mit Schlammsack", "Entwaesserungsrinne_mit_Schlammsack"),
        new(["kombischacht"], "Kombischacht", "Kombischacht"),
        new(["regenueberlauf", "regenüberlauf"], "Regenüberlauf", "Regenueberlauf"),
        new(["schwimmstoffabscheider"], "Schwimmstoffabscheider", "Schwimmstoffabscheider"),
        new(["vorbehandlungsanlage"], "Vorbehandlungsanlage", "Vorbehandlungsanlage"),

        // --- SchachtPro-Begriffe ohne genauen Normwert ---
        // Entscheid Pascal 2026-08-29: alle drei gehen in der Datei auf "andere".
        // Im Programm bleibt der genaue Begriff stehen.
        //
        // Fettabscheider ist eine bewusste Ausnahme: das Modell kennt ihn, der
        // AWU-Bestand benutzt ihn in 64420 Schaechten aber kein einziges Mal.
        // Folge: ein aus einer XTF gelesener "Fettabscheider" kaeme beim Schreiben
        // als "andere" zurueck. Praktisch folgenlos - der Export schreibt nur
        // handgeaenderte Felder, und der Wert kommt im Bestand nicht vor.
        new(["fettabscheider"], "Fettabscheider", "andere"),
        // Versickerung ist in SIA405 eine eigene Objektklasse, keine Schachtfunktion.
        new(["sickerschacht"], "Sickerschacht", "andere"),
        new(["spezialbauwerk"], "Spezialbauwerk", "andere")
    ];

    /// <summary>
    /// Bringt eine beliebige gelesene Schreibweise auf den Begriff des Programms.
    /// Ein unbekannter Wert bleibt unveraendert stehen.
    /// </summary>
    public static string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        return text.Length == 0 ? "" : Finde(text)?.App ?? text;
    }

    /// <summary>
    /// Die in SIA405 gueltige Schreibweise, oder <c>null</c>, wenn der Wert dort zu
    /// keinem Begriff gehoert. Dann wird nichts geschrieben statt geraten.
    /// </summary>
    public static string? NachNorm(string? wert) => Finde((wert ?? "").Trim())?.Norm;

    private static Konzept? Finde(string text)
    {
        if (text.Length == 0)
            return null;

        var klein = text.ToLowerInvariant();
        var mitUnterstrich = klein.Replace(' ', '_');

        return Konzepte.FirstOrDefault(k =>
            k.Gelesen.Contains(klein)
            || k.Gelesen.Contains(mitUnterstrich)
            || string.Equals(k.App, text, StringComparison.OrdinalIgnoreCase));
    }
}
