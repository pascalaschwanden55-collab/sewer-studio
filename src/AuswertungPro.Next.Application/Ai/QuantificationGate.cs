namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Entscheidet codeabhaengig, WELCHE Quantifizierungs-Werte fuer einen VSA-Code geschrieben werden
/// duerfen. Kombiniert drei Quellen, ohne selbst zu messen:
///
///   1. Manifest (Single Source of Truth, ADR-006): OB ueberhaupt Q1/Q2/Uhrlage erlaubt sind
///      — vom Aufrufer aus IVsaCodeSelectionCatalog ermittelt und als <see cref="ManifestQuantRule"/> uebergeben.
///   2. Wissens-Tabelle <see cref="QuantificationUnitPolicy"/>: WELCHE physikalische Groesse Q1/Q2 traegt.
///   3. Die tatsaechlich verfuegbaren SAM-Messwerte (welche Felder hat die Quantifizierung geliefert).
///
/// Ergebnis: pro VSA-Feld ein bool, ob es geschrieben werden darf. So bekommt z.B. eine Infiltration
/// (BBF: Manifest ohne Q1) KEINE mm/%-Werte mehr, ein Riss (BAB) nur die Breite (mm), ein Haarriss
/// gar keine. Reine, testbare Logik (keine UI/Infrastructure-Abhaengigkeit).
/// </summary>
public static class QuantificationGate
{
    /// <summary>Was das Manifest fuer einen Code an Quantifizierung vorsieht (vom Katalog ermittelt).</summary>
    public readonly record struct ManifestQuantRule(bool HasQ1, bool HasQ2, bool AllowClock);

    /// <summary>Welche SAM-Messwerte tatsaechlich vorliegen.</summary>
    public readonly record struct AvailableValues(
        bool HasHeightMm,
        bool HasWidthMm,
        bool HasExtentPercent,
        bool HasCrossSectionPercent,
        bool HasClock);

    /// <summary>Welche vsa.*-Felder geschrieben werden duerfen.</summary>
    public readonly record struct WriteDecision(
        bool WriteHeightMm,
        bool WriteWidthMm,
        bool WriteExtentPercent,
        bool WriteCrossSectionPercent,
        bool WriteClock)
    {
        public bool WritesAnything =>
            WriteHeightMm || WriteWidthMm || WriteExtentPercent || WriteCrossSectionPercent || WriteClock;
    }

    /// <summary>
    /// Bestimmt, welche Quantifizierungs-Felder fuer den Code geschrieben werden duerfen.
    /// </summary>
    public static WriteDecision Decide(string code, ManifestQuantRule manifest, AvailableValues available)
    {
        // Haarriss (BABAx): VSA verbietet jede Quantifizierung, auch wenn Manifest Q1 vorsieht.
        if (QuantificationUnitPolicy.IsHaarriss(code))
            return new WriteDecision(false, false, false, false,
                WriteClock: manifest.AllowClock && available.HasClock);

        var units = QuantificationUnitPolicy.GetUnits(code);

        // Pro vorhandenem Manifest-Slot (Q1, Q2) die zugeordnete Einheit pruefen und gegen die
        // verfuegbaren SAM-Werte abgleichen. Ein Slot, dessen Einheit Unknown ist, wird nicht befuellt.
        bool wHeight = false, wWidth = false, wExtent = false, wCross = false;

        if (manifest.HasQ1)
            Allow(units.Q1, available, ref wHeight, ref wWidth, ref wExtent, ref wCross);
        if (manifest.HasQ2)
            Allow(units.Q2, available, ref wHeight, ref wWidth, ref wExtent, ref wCross);

        return new WriteDecision(
            WriteHeightMm: wHeight,
            WriteWidthMm: wWidth,
            WriteExtentPercent: wExtent,
            WriteCrossSectionPercent: wCross,
            WriteClock: manifest.AllowClock && available.HasClock);
    }

    private static void Allow(
        QuantificationUnitPolicy.QuantUnit unit,
        AvailableValues available,
        ref bool wHeight, ref bool wWidth, ref bool wExtent, ref bool wCross)
    {
        switch (unit)
        {
            case QuantificationUnitPolicy.QuantUnit.HeightMm when available.HasHeightMm:
                wHeight = true; break;
            case QuantificationUnitPolicy.QuantUnit.WidthMm when available.HasWidthMm:
                wWidth = true; break;
            // Laenge/Versatz mm haben in der SAM-Quantifizierung kein eigenes Feld -> auf Hoehe abbilden,
            // da SAM die groesste Ausdehnung als HeightMm liefert (konservativ; nur wenn vorhanden).
            case QuantificationUnitPolicy.QuantUnit.LengthMm when available.HasHeightMm:
            case QuantificationUnitPolicy.QuantUnit.OffsetMm when available.HasHeightMm:
                wHeight = true; break;
            case QuantificationUnitPolicy.QuantUnit.CrossSectionPercent when available.HasCrossSectionPercent:
                wCross = true; break;
            case QuantificationUnitPolicy.QuantUnit.ExtentPercent when available.HasExtentPercent:
                wExtent = true; break;
            // AngleDegrees: SAM liefert keinen Winkel -> nicht automatisch befuellbar.
            // Unknown: bewusst nichts schreiben.
            default:
                break;
        }
    }
}
