using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Wirksamer Hauptordner einer Verteilart, wie ihn die Export-Seite bestimmt.</summary>
/// <param name="Ordner">Ordner; null, wenn er erst beim Verteilen abgefragt wird.</param>
/// <param name="Anzeige">Klartext für das Fenster («Projektordner \ Haltungen_Verteilt»).</param>
/// <param name="Baum">Eingefrorene Ordnerbausteine, wie sie auch das Verteilen verwendet.</param>
public sealed record VerteilZiel(string? Ordner, string Anzeige, DistributionTargetConfig? Baum);

/// <summary>Was das Fenster beim Öffnen vorgewählt zeigt.</summary>
public sealed record VerteilenVorgabe(
    VerteilArt Art,
    DistributionVariant Ablage,
    string? FilmOrdner,
    Project? Projekt,
    Func<VerteilArt, VerteilZiel> Ziel);

/// <summary>Antwort des Fensters: ein Auftrag, nichts, oder der Wunsch, die Ordnerbausteine zu öffnen.</summary>
public sealed record VerteilenErgebnis(VerteilAuftrag? Auftrag, bool EinstellungenOeffnen)
{
    public static VerteilenErgebnis Abgebrochen { get; } = new(null, false);
}

/// <summary>
/// Öffnet das Fenster «Verteilen»: Art, Ablage, Quelle, Filme und Ziel wählen, die
/// schreibfreie Vorschau prüfen, dann bestätigen. Verteilt wird danach von der Export-Seite.
/// </summary>
public interface IVerteilenDialog
{
    VerteilenErgebnis Zeige(VerteilenVorgabe vorgabe);
}
