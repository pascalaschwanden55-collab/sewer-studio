using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record PhotoMeasurementToolPresentation(
    LevelMode LevelMode,
    bool ShowLevelControls,
    bool ShowAngleControls,
    bool ShowUndo,
    bool ShowDelete,
    bool IsOkEnabled,
    bool UseCrossCursor,
    bool ResetLevelSliders,
    bool ResetAngleSliders,
    string StatusText);

/// <summary>
/// Liefert den sichtbaren Fensterzustand für das gewählte Foto-Messwerkzeug.
/// </summary>
internal static class PhotoMeasurementToolPresentationPolicy
{
    internal static PhotoMeasurementToolPresentation Build(
        PhotoTool tool,
        LevelMode currentLevelMode,
        bool isCalibrated)
    {
        var isLevel = tool is PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle;
        var isAngle = tool is PhotoTool.Lateral or PhotoTool.Bend;
        // Der Querschnitt gehoert dazu, obwohl er Prozente liefert: Sein Wert
        // bezieht sich auf die ROHRFLAECHE, und die ist ohne Referenzlinie
        // unbekannt. Die Verformung dagegen NICHT — sie vergleicht zwei
        // gemessene Achsen miteinander und braucht keinen Massstab.
        var needsCalibration = tool is PhotoTool.Ruler
            or PhotoTool.Connection
            or PhotoTool.CrossSection;

        var levelMode = tool switch
        {
            PhotoTool.LevelWater => LevelMode.Water,
            PhotoTool.LevelDeposit => LevelMode.Deposit,
            PhotoTool.LevelObstacle => LevelMode.Obstacle,
            _ => currentLevelMode
        };

        return new PhotoMeasurementToolPresentation(
            levelMode,
            ShowLevelControls: isLevel,
            ShowAngleControls: isAngle,
            ShowUndo: tool is PhotoTool.Deformation or PhotoTool.CrossSection,
            ShowDelete: tool != PhotoTool.None,
            IsOkEnabled: !needsCalibration || isCalibrated,
            UseCrossCursor: tool != PhotoTool.None,
            ResetLevelSliders: isLevel,
            ResetAngleSliders: isAngle,
            StatusText: StatusText(tool));
    }

    private static string StatusText(PhotoTool tool) => tool switch
    {
        PhotoTool.None => "Werkzeug wählen, um mit der Messung zu beginnen.",
        PhotoTool.Calibration => "Referenzlinie über sichtbaren Rohrdurchmesser ziehen.",
        PhotoTool.MarkRect => "Rechteck um Schaden/Beobachtung ziehen (für KI-Training).",
        PhotoTool.LevelWater => "Wasserstand: Slider links | Mausrad: Kreis-Größe | Drag: Position",
        PhotoTool.LevelDeposit => "Ablagerung: Slider links | Mausrad: Kreis-Größe | Drag: Position",
        PhotoTool.LevelObstacle => "Hindernis: Slider links | Mausrad: Kreis-Größe | Drag: Position",
        PhotoTool.Deformation => "4 Punkte auf Rohrwand klicken: Oben → Unten → Links → Rechts",
        PhotoTool.Ruler => "Linie ziehen für Distanzmessung (Kalibrierung nötig).",
        PhotoTool.CrossSection => "Polygon-Punkte klicken, Doppelklick = schließen.",
        PhotoTool.Lateral => "Position + Winkel per Slider einstellen.",
        PhotoTool.Bend => "Position + Winkel per Slider einstellen.",
        PhotoTool.Connection => "Massstab-Linie auf Rohroberfläche ziehen.",
        _ => ""
    };
}
