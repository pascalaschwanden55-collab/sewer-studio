using System;
using System.IO;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Baut das Nachschlag-Subsystem einmalig zusammen. Der ServiceProvider
/// delegiert nur — dieselbe Trennung wie bei den uebrigen Subsystemen.
/// </summary>
public static class FeldNachschlagComposition
{
    /// <summary>
    /// Standardablage der Schacht-Katastertabelle, neben der bereits
    /// vorhandenen Haltungstabelle.
    /// </summary>
    public static string StandardTabellenPfad => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SewerStudio", "map", "abwasserkataster_schaechte.tsv");

    /// <summary>
    /// Erzeugt den UseCase mit beiden Quellen. Der Grundbuchweg setzt auf der
    /// Lage auf, die der Kataster liefert — Projektdatensaetze fuehren keine
    /// Koordinaten.
    /// </summary>
    /// <param name="katasterXtfPfad">
    /// Pfad zur Abwasserkataster-XTF. Leer oder nicht vorhanden ist erlaubt —
    /// der Nachschlag nennt dann den Grund.
    /// </param>
    /// <param name="parzellen">Raeumliche Parzellensuche des Kantons.</param>
    /// <param name="grundbuch">Grundbuchauskunft des Kantons.</param>
    /// <param name="log">
    /// Optionales Protokoll. Es erhaelt nur Status und Fehlerklasse — nie
    /// einen Namen, nie eine Adresse.
    /// </param>
    public static FeldNachschlagUseCase Erzeuge(
        string katasterXtfPfad,
        IParcelLookup parzellen,
        ILandRegistryLookup grundbuch,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(parzellen);
        ArgumentNullException.ThrowIfNull(grundbuch);

        var kataster = ErzeugeKatasterAnbieter(katasterXtfPfad);

        return new FeldNachschlagUseCase(
            kataster,
            new GrundbuchFeldNachschlag(kataster.LiesLage, parzellen, grundbuch, log),
            ErzeugeHaltungsAnbieter(katasterXtfPfad));
    }

    /// <summary>
    /// Nur der lokale Kataster, ohne Netzzugriff. Fuer Aufrufer ohne
    /// Auskunftsdienste — der Grundbuchweg meldet dann ehrlich, dass er nicht
    /// angeschlossen ist, statt stillschweigend nichts zu finden.
    /// </summary>
    public static FeldNachschlagUseCase ErzeugeNurKataster(string katasterXtfPfad)
        => new(
            ErzeugeKatasterAnbieter(katasterXtfPfad),
            new NochNichtAngeschlossenerNachschlag(
                "Die Grundbuchauskunft ist hier nicht angeschlossen."),
            ErzeugeHaltungsAnbieter(katasterXtfPfad));

    /// <summary>
    /// Der Anbieter fuer Haltungen. Er nutzt dieselbe Katastertabelle wie der
    /// Verteil-Abgleich und veraendert sie nicht.
    /// </summary>
    public static KatasterHaltungFeldNachschlag ErzeugeHaltungsAnbieter(string katasterXtfPfad)
        => new(
            new HaltungCadastreTableFileStore(),
            HaltungCadastreIndex.DefaultTablePath,
            katasterXtfPfad ?? string.Empty);

    /// <summary>
    /// Der Kataster-Anbieter allein. Er liefert ausserdem die Lage eines
    /// Schachts — Grundlage fuer den spaeteren Grundbuchweg.
    /// </summary>
    public static KatasterFeldNachschlag ErzeugeKatasterAnbieter(string katasterXtfPfad)
        => new(
            new SchachtCadastreTableFileStore(),
            StandardTabellenPfad,
            katasterXtfPfad ?? string.Empty);
}
