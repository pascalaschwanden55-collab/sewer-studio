namespace AuswertungPro.Next.Application.Ai.Diagnostics;

/// <summary>
/// Standardisierte Begruendungen fuer <see cref="AiDiagnosticEvent.DroppedReason"/>.
/// Nur ergaenzen wenn ein neuer Filter eingebaut wird — niemals Freitext-Strings
/// im Aufruf-Code verwenden, sonst ist die Diagnose-Spur nicht filterbar.
/// </summary>
public static class AiDiagnosticDropReason
{
    /// <summary>ResolveFindingCodeForCoding gibt null zurueck (kein VSA-Code ableitbar).</summary>
    public const string CodeResolverNull    = "code-resolver-null";

    /// <summary>BCD bereits vorhanden (Heuristik oder Eingabemarker setzte ihn schon).</summary>
    public const string DedupBcd            = "dedup-bcd";

    /// <summary>BCE bereits vorhanden.</summary>
    public const string DedupBce            = "dedup-bce";

    /// <summary>Generisches Dedup: Code+Position bereits in der Liste.</summary>
    public const string DedupExisting       = "dedup-existing";

    /// <summary>Vom User explizit abgelehnt (Sperrliste).</summary>
    public const string RejectedByUser      = "rejected-by-user";

    /// <summary>view_type=nahaufnahme/schwenk → Severity auf 1 reduziert / Finding unterdrueckt.</summary>
    public const string ViewTypeSuppressed  = "viewtype-suppressed";

    /// <summary>QualityGate hat das Finding als Rot/unsicher bewertet.</summary>
    public const string QualityGateRed      = "quality-gate-red";

    /// <summary>Detektion liegt in der Rohr-Tiefe, nicht im Nahbereich.</summary>
    public const string ZoneDepth           = "zone-depth";

    /// <summary>Kunststoffrohr-Regel: BBF/BBD ohne Begleitschaden → physikalisch unmoeglich.</summary>
    public const string KunststoffFilter    = "kunststoff-filter";

    /// <summary>Frame ist noch nicht analysierbar (Warmup / OSD-Maskierung).</summary>
    public const string FrameNotReady       = "frame-not-ready";

    /// <summary>Qwen meldete is_empty_frame=true.</summary>
    public const string EmptyFrame          = "empty-frame";

    /// <summary>Frame konnte nicht aus dem Video extrahiert werden.</summary>
    public const string FrameExtractFailed  = "frame-extract-failed";

    /// <summary>Gezielte Code-Verifikation hat keinen sichtbaren Treffer gemeldet.</summary>
    public const string VerificationNotVisible = "verification-not-visible";

    /// <summary>Gezielte Code-Verifikation war sichtbar, aber unterhalb der Schwellwerte.</summary>
    public const string VerificationLowConfidence = "verification-low-confidence";

    /// <summary>Gezielte Code-Verifikation ist technisch fehlgeschlagen.</summary>
    public const string VerificationError = "verification-error";
}
