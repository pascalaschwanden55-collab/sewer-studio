namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Ein Schacht (Abwasserknoten) als Punkt in LV95: Bezeichnung + Koordinate.
/// </summary>
public sealed record ManholeGeometry(string Bezeichnung, double X, double Y);
