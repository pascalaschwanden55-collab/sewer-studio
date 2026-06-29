using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Cost;

/// <summary>
/// Schlankes Read-Model fuer einen Massnahmen-Block,
/// das vom CostConsistencyChecker benoetigt wird.
/// UI-ViewModels implementieren dieses Interface nicht direkt —
/// der CostConsistencyCheckService (UI) konvertiert MeasureBlockVm in ein
/// MeasureBlockView-Datenobjekt und uebergibt es dem Checker.
/// </summary>
public interface IMeasureBlockView
{
    string MeasureId { get; }
    string MeasureName { get; }

    /// <summary>DN-Text wie vom Benutzer eingegeben (z.B. "300").</summary>
    string? DnText { get; }

    /// <summary>Laengentext wie vom Benutzer eingegeben (z.B. "45.00").</summary>
    string? LengthText { get; }

    /// <summary>Anschlusstext wie vom Benutzer eingegeben (z.B. "2").</summary>
    string? ConnectionsText { get; }

    /// <summary>Gesamtsumme des Blocks (nur aktivierte Zeilen).</summary>
    decimal Total { get; }

    IReadOnlyList<ICostLineView> Lines { get; }
}

/// <summary>
/// Schlankes Read-Model fuer eine Kostenzeile.
/// </summary>
public interface ICostLineView
{
    string? ItemKey { get; }
    string? Text { get; }
    string? Unit { get; }
    decimal Qty { get; }
    decimal UnitPrice { get; }
    bool Selected { get; }
    bool PriceMissing { get; }
    bool IsPriceOverridden { get; }
}
