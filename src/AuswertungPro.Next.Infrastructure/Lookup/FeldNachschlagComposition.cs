using System;
using System.IO;
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
    /// Erzeugt den UseCase mit dem lokalen Kataster als Quelle. Der
    /// Grundbuch-Anbieter ist noch nicht angeschlossen und meldet das ehrlich,
    /// statt stillschweigend nichts zu finden.
    /// </summary>
    /// <param name="katasterXtfPfad">
    /// Pfad zur Abwasserkataster-XTF. Leer oder nicht vorhanden ist erlaubt —
    /// der Nachschlag nennt dann den Grund.
    /// </param>
    public static FeldNachschlagUseCase Erzeuge(string katasterXtfPfad)
    {
        var kataster = ErzeugeKatasterAnbieter(katasterXtfPfad);

        return new FeldNachschlagUseCase(
            kataster,
            new NochNichtAngeschlossenerNachschlag(
                "Die Grundbuchauskunft ist noch nicht angeschlossen."));
    }

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
