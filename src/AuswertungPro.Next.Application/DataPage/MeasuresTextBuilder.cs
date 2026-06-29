using System;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Baut den Massnahmen-Empfehlungstext aus einer <see cref="HoldingCost"/> auf.
/// Aus <c>UI.DataPage.DataPageSanierungCostMapper</c> extrahiert, damit unit-testbar
/// (verhaltensneutral; die UI-Klasse delegiert ihre puren Methoden hierher).
/// </summary>
public static class MeasuresTextBuilder
{
    /// <summary>
    /// Baut den kanonischen Massnahmen-Text aus einer Kostenkalkulation.
    /// Nur Hauptarbeit-Zeilen werden beruecksichtigt — Nebenarbeiten wuerden das
    /// MeasureRecommendationService-Learning vergiften.
    /// </summary>
    public static string BuildMeasuresText(HoldingCost cost)
    {
        // Nur kanonische Massnahmen-Namen (Template-Level) schreiben,
        // KEINE einzelnen Kostenzeilen wie Verkehrsdienst oder Nebenarbeiten.
        var measureNames = cost.Measures
            .Where(m => m.Lines.Any(l => l.Selected && MeasureClassification.IsHauptarbeitLine(l)))
            .Select(m => m.MeasureName ?? m.MeasureId ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (measureNames.Count > 0)
            return string.Join(Environment.NewLine, measureNames);

        // Fallback: Falls keine Hauptarbeit erkannt, Transfer-markierte Zeilen
        // (legacy/manuelles Verhalten), aber nur Hauptarbeit-Zeilen, keine Nebenarbeiten.
        var markedHauptarbeit = cost.Measures
            .SelectMany(m => m.Lines)
            .Where(l => l.Selected && l.TransferMarked && MeasureClassification.IsHauptarbeitLine(l))
            .Select(l => FormatRecommendationBullet(l.Text))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (markedHauptarbeit.Count > 0)
            return string.Join(Environment.NewLine, markedHauptarbeit);

        return "";
    }

    /// <summary>
    /// Entfernt fuehrende Bullet-Zeichen (- / *) und Leerraum eines Empfehlungs-Eintrags.
    /// Oeffentlich, weil das ViewModel (ParseRecommendedTemplates) es ebenfalls nutzt.
    /// </summary>
    public static string NormalizeRecommendationEntry(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        while (text.Length > 0 && (text[0] == '-' || text[0] == '*'))
            text = text[1..].TrimStart();
        return text;
    }

    /// <summary>Formatiert einen Dezimalwert als String; leer wenn &lt;= 0.</summary>
    public static string FormatDecimal(decimal value)
        => value <= 0m ? "" : value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Formatiert eine Ganzzahl als String; leer wenn &lt;= 0.</summary>
    public static string FormatInt(int value)
        => value <= 0 ? "" : value.ToString(CultureInfo.InvariantCulture);

    private static string FormatRecommendationBullet(string? value)
    {
        var normalized = NormalizeRecommendationEntry(value);
        return normalized.Length == 0 ? string.Empty : "- " + normalized;
    }
}
