using System;
using System.IO;

namespace AuswertungPro.Next.Application.UseCases.Verteilung;

/// <summary>Ergebnis der Vorpruefung eines Verteilziels. Meldung ist nur bei Fehlern gesetzt.</summary>
public sealed record VerteilzielErgebnis(bool Erreichbar, string? Meldung);

/// <summary>
/// Reine Regel: Ist die eingestellte Verteilwurzel ueberhaupt erreichbar?
///
/// Anlass 18.09.2026: In den Einstellungen stand "I:\" - ein Laufwerk, das es nicht mehr gab.
/// Jede Datei meldete daraufhin nur "Could not find a part of the path 'I:\...'".
///
/// Geprueft wird der Laufwerks- beziehungsweise Freigabestamm, NICHT der ganze Pfad:
/// Ein noch nicht vorhandener Unterordner ist normal und wird beim Verteilen angelegt.
/// Eine leere Wurzel bedeutet "nicht konfiguriert" und ist kein Fehler - der Aufrufer
/// weicht dann auf den Projektordner aus.
/// </summary>
public static class VerteilzielPruefung
{
    public static VerteilzielErgebnis Pruefe(string? wurzel, Func<string, bool> ordnerVorhanden)
    {
        ArgumentNullException.ThrowIfNull(ordnerVorhanden);

        if (string.IsNullOrWhiteSpace(wurzel))
            return new VerteilzielErgebnis(true, null);

        var pfad = wurzel.Trim();

        string? stamm;
        try
        {
            stamm = Path.GetPathRoot(pfad);
        }
        catch (ArgumentException)
        {
            // Unzulaessige Zeichen: das faengt der bestehende Schreibschutz beim Verteilen ab.
            return new VerteilzielErgebnis(true, null);
        }

        // Relativer Pfad ohne Stamm - hier nicht beurteilbar, bestehendes Verhalten bleibt.
        if (string.IsNullOrEmpty(stamm))
            return new VerteilzielErgebnis(true, null);

        if (ordnerVorhanden(stamm))
            return new VerteilzielErgebnis(true, null);

        var meldung =
            $"Der eingestellte Verteilordner \"{pfad}\" ist nicht erreichbar: {stamm} ist nicht verfügbar.\n\n"
            + "Möglicherweise ist das Laufwerk nicht angeschlossen oder die Netzfreigabe nicht verbunden. "
            + "Bitte den Zielordner auf der Exportseite prüfen und den Lauf erneut starten.";

        return new VerteilzielErgebnis(false, meldung);
    }
}
