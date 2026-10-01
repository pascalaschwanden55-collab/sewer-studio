using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Welches Bauteil eine Druckcenter-Zeile beschreibt.</summary>
public enum DruckcenterRowKind
{
    Haltung,
    Schacht
}

public sealed class DruckcenterRowVm
{
    /// <summary>
    /// Nur bei Haltungszeilen gesetzt. Schaechte tragen bewusst keine Haltung, damit der
    /// Dossier-Druck nicht versehentlich eine leere Haltung ausgibt.
    /// </summary>
    public HaltungRecord? Record { get; init; }

    public DruckcenterRowKind Kind { get; init; } = DruckcenterRowKind.Haltung;

    /// <summary>Ein volles Dossier gibt es nur fuer Haltungen — Schaechte haben keines.</summary>
    public bool CanPrintDossier => Kind == DruckcenterRowKind.Haltung && Record is not null;

    /// <summary>Anzeigename der Zeile: Haltungsname bzw. Schachtnummer.</summary>
    public string Holding { get; init; } = "";
    public string Street { get; init; } = "";
    public string Owner { get; init; } = "";
    public string Sanieren { get; init; } = "";
    public string ExecutedBy { get; init; } = "";
    public string Material { get; init; } = "";
    public string Status { get; init; } = "";
    public string Year { get; init; } = "";
    public string Zustand { get; init; } = "";
    public decimal NetCost { get; init; }
    public HoldingCost? StoredCost { get; init; }
    public bool HasDetailedCost { get; init; }
    public bool HasMeasures { get; init; }
    public string CostSource { get; init; } = "";
    public string MeasuresRaw { get; init; } = "";
    public string MeasuresPreview { get; init; } = "";
    public string NetCostText => ChfFormat.Money(NetCost);
}

public sealed class SpecialPositionStatVm
{
    public string Category { get; init; } = "";
    public string Position { get; init; } = "";
    public decimal Qty { get; init; }
    public string Unit { get; init; } = "";
    public int HoldingCount { get; init; }
    public string QtyText => $"{Qty:0.##} {Unit}";
    public string HoldingCountText => HoldingCount.ToString(CultureInfo.InvariantCulture);
}

public sealed class ChartBarVm
{
    public ChartBarVm(string label, int value, int total)
    {
        Label = label;
        var safeTotal = Math.Max(total, 0);
        var safeValue = Math.Max(value, 0);
        Percent = safeTotal > 0 ? (safeValue * 100.0) / safeTotal : 0.0;
        ValueText = $"{safeValue}/{safeTotal} ({Percent:0.#}%)";
    }

    public ChartBarVm(string label, decimal amount, decimal totalAmount)
    {
        Label = label;
        var safeAmount = amount < 0m ? 0m : amount;
        var safeTotal = totalAmount < 0m ? 0m : totalAmount;
        Percent = safeTotal > 0m ? (double)(safeAmount * 100m / safeTotal) : 0.0;
        ValueText = $"{ChfFormat.Money(safeAmount)} ({Percent:0.#}%)";
    }

    public string Label { get; }
    public double Percent { get; }
    public string ValueText { get; }
}
