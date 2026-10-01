using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Fixwelle 2b (P3): Startbreiten der drei Textspalten, die im Prototyp breiter sind als
/// ihr Kopf.
///
/// Anlass ist das Abnahmebild: Alle Spalten stehen auf <c>SizeToHeader</c>, deshalb ist
/// "STRASSE" 72 px breit und "Gotthardstrasse" (98 px) wird abgeschnitten. Die Werte stammen
/// aus dem freigegebenen Prototyp v2.
///
/// Es ist eine STARTbreite, keine Mindestbreite: Der Benutzer kann jede Spalte weiterhin
/// beliebig schmal ziehen, und ein gespeichertes Spaltenlayout gewinnt, weil es erst danach
/// wiederhergestellt wird. Reine Daten, kein WPF.
/// </summary>
public static class NovaSpaltenbreiten
{
    /// <summary>Haltungsname beziehungsweise Schachtnummer.</summary>
    public const double Name = 150;

    public const double Strasse = 120;

    /// <summary>Rohrmaterial beziehungsweise Material des Schachts.</summary>
    public const double Material = 100;

    private static readonly Dictionary<string, double> Breiten = new(StringComparer.Ordinal)
    {
        [FieldKeys.HoldingName] = Name,
        ["Schachtnummer"] = Name,
        [FieldKeys.Street] = Strasse,
        [FieldKeys.PipeMaterial] = Material,
        ["Material"] = Material
    };

    /// <summary>
    /// Die Startbreite eines Feldes, sonst <c>null</c>. <paramref name="falte"/> ist der
    /// Namensvergleich der Liste (bei Schaechten <c>SchachtFeldnamen.Falte</c>), weil
    /// Schachtfelder nach der Kopfzeile der Excel-Vorlage heissen.
    /// </summary>
    public static double? Startbreite(string? feld, Func<string, string>? falte = null)
    {
        if (string.IsNullOrWhiteSpace(feld))
            return null;

        if (falte is null)
            return Breiten.TryGetValue(feld, out var breite) ? breite : null;

        var gesucht = falte(feld);
        foreach (var (bekannt, breite) in Breiten)
        {
            if (string.Equals(falte(bekannt), gesucht, StringComparison.Ordinal))
                return breite;
        }
        return null;
    }
}
