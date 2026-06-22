namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Zentrale Tag-Konstanten fuer Elemente auf dem CodingOverlayCanvas.
/// Geteilter Vertrag: der Render-Pfad SETZT diese Tags, das Cleanup (ClearTransientCodingCanvas,
/// RenderReferenceDn, RenderAiOverlays) ENTFERNT sie wieder. Magic-Strings hier zentralisiert,
/// damit Render und Cleanup nicht stillschweigend auseinanderlaufen ("stille Render-Leichen").
/// </summary>
public static class OverlayTags
{
    /// <summary>Vorschau-/Schema-Elemente (werden bei jedem Redraw neu gezeichnet).</summary>
    public const string Preview = "overlay_preview";

    /// <summary>Manuell gezeichnetes Overlay des Operateurs.</summary>
    public const string Manual = "overlay_manual";

    /// <summary>Mess-Hilfslinien/-punkte.</summary>
    public const string Measure = "overlay_measure";

    /// <summary>Referenz-DN-Kreis.</summary>
    public const string RefDn = "ref_dn";

    /// <summary>Konkretes KI-Overlay-Element (Tag-Wert; alle KI-Tags beginnen mit <see cref="AiPrefix"/>).</summary>
    public const string AiOverlay = "ai_overlay";

    /// <summary>Praefix aller KI-Overlay-Tags (Entfernen via StartsWith).</summary>
    public const string AiPrefix = "ai_";

    /// <summary>Werkzeug-Badge (aktives Codier-Werkzeug oben im Overlay).</summary>
    public const string ToolBadge = "tool_badge";

    /// <summary>Bogen-Erkennungsmarker im Markierungsfluss.</summary>
    public const string BendMarker = "bend_marker";
}
