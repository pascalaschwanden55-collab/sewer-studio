namespace AuswertungPro.Next.Application.Ai.PhotoAssistant;

/// <summary>
/// WPF-freier 2D-Punkt fuer PhotoAssistant-Geometry-Services.
///
/// Phase 1.5b (2026-05-10, Standortbestimmung Item A): die PhotoAssistant-
/// Math-Services (BendAngleToolService, DeformationToolService,
/// LateralToolService) leben heute in UI/Ai/PhotoAssistant/ nur weil sie
/// `System.Windows.Point` nutzen. Mit diesem Record koennen sie in
/// Application/Ai/PhotoAssistant/ migrieren — das ist ARCH-H5-konsistent
/// (keine WPF-Dependency in Domain/Application).
///
/// UI-Layer konvertiert per Extension-Methods (Point2DExtensions) zwischen
/// <see cref="Point2D"/> und <c>System.Windows.Point</c>.
/// </summary>
/// <param name="X">X-Koordinate.</param>
/// <param name="Y">Y-Koordinate.</param>
public readonly record struct Point2D(double X, double Y);
