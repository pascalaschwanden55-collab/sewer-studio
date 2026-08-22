using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.Import.Kataster;

/// <summary>Eine Haltung aus dem amtlichen Abwasserkataster.</summary>
public sealed record KatasterHaltung(
    string SchachtOben,
    string SchachtUnten,
    string AmtlicheBezeichnung);

/// <summary>
/// Nachschlagewerk fuer amtliche Haltungsbezeichnungen, gebildet aus einer
/// SIA405-Katasterdatei.
///
/// Hintergrund: Eine Haltungsnummer ist "Schacht oben - Schacht unten". Diese Nummer laesst
/// sich aus jedem Inspektionsprotokoll bilden. Im Kataster kann die amtliche Bezeichnung
/// davon aber abweichen, wenn ein Schacht spaeter umnummeriert wurde und der Haltungsname
/// nicht mitgezogen ist.
///
/// Gemessen am Bestand Andermatt (2026-08-21): 13 von 15 Haltungen tragen exakt die aus dem
/// Protokoll gebildete Nummer. Bei zwei Haltungen fuehrt der Kataster eine andere
/// Bezeichnung — dort heisst der obere Schacht heute "955509", die amtliche Haltung aber
/// weiterhin "7.4790-4789"; "7.4790" existiert im Kataster gar nicht mehr als Schacht.
/// </summary>
public interface IKatasterHaltungsverzeichnis
{
    /// <summary>Amtliche Bezeichnung zum Schachtpaar. Null, wenn nicht eindeutig gefunden.</summary>
    string? FindeBezeichnung(string? schachtOben, string? schachtUnten);

    /// <summary>Anzahl eindeutig aufloesbarer Haltungen.</summary>
    int Anzahl { get; }
}

/// <summary>
/// Reine Nachschlage-Umsetzung ohne Dateizugriff. Das Lesen der XTF liegt in Infrastructure.
/// </summary>
public sealed class KatasterHaltungsverzeichnis : IKatasterHaltungsverzeichnis
{
    private readonly Dictionary<string, string> _nachSchachtpaar;

    public static IKatasterHaltungsverzeichnis Leer { get; } =
        new KatasterHaltungsverzeichnis(Array.Empty<KatasterHaltung>());

    public KatasterHaltungsverzeichnis(IEnumerable<KatasterHaltung> haltungen)
    {
        ArgumentNullException.ThrowIfNull(haltungen);

        // Ein Schachtpaar, das im Kataster mehrfach mit VERSCHIEDENEN Bezeichnungen
        // vorkommt, ist nicht eindeutig und wird bewusst verworfen — lieber keine
        // Korrektur als eine geratene.
        var kandidaten = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in haltungen)
        {
            if (h is null) continue;
            var schluessel = Schluessel(h.SchachtOben, h.SchachtUnten);
            if (schluessel is null || string.IsNullOrWhiteSpace(h.AmtlicheBezeichnung))
                continue;

            if (!kandidaten.TryGetValue(schluessel, out var menge))
            {
                menge = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                kandidaten[schluessel] = menge;
            }

            menge.Add(h.AmtlicheBezeichnung.Trim());
        }

        _nachSchachtpaar = kandidaten
            .Where(p => p.Value.Count == 1)
            .ToDictionary(p => p.Key, p => p.Value.First(), StringComparer.OrdinalIgnoreCase);
    }

    public int Anzahl => _nachSchachtpaar.Count;

    public string? FindeBezeichnung(string? schachtOben, string? schachtUnten)
    {
        var schluessel = Schluessel(schachtOben, schachtUnten);
        return schluessel is not null && _nachSchachtpaar.TryGetValue(schluessel, out var b) ? b : null;
    }

    private static string? Schluessel(string? oben, string? unten)
    {
        var o = oben?.Trim();
        var u = unten?.Trim();
        return string.IsNullOrWhiteSpace(o) || string.IsNullOrWhiteSpace(u) ? null : $"{o}{u}";
    }
}
