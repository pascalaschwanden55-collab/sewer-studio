using System;

namespace AuswertungPro.Next.Application.Ai.PhotoAssistant;

/// <summary>
/// 3D-zu-2D Lochkamera-Projektion fuer den Foto-Assistenten (Bogen/Knick-Werkzeug).
///
/// Modell:
///   - Kamera bei (0, camYOffset, -camDist)
///   - Schaut entlang +Z
///   - Brennweite focal in Pixeln
///   - Bildmitte (cx, cy) plus optionaler Drag-Offset (offsetX, offsetY)
///
/// Punkte mit (Z + camDist) &lt; 0.05 liegen hinter / zu nah an der Kamera und werden verworfen.
/// </summary>
public static class PinholeProjection
{
    /// <summary>
    /// Projiziert einen 3D-Welt-Punkt auf den Bildschirm. Liefert null wenn der Punkt
    /// hinter der Kamera oder zu nahe liegt.
    /// </summary>
    /// <returns>(sx, sy, depth) - depth = Z + camDist fuer Tiefen-Sortierung.</returns>
    public static (double sx, double sy, double depth)? Project(
        double xWorld, double yWorld, double zWorld,
        double focal, double camDist, double camYOffset,
        double cx, double cy,
        double offsetX = 0, double offsetY = 0)
    {
        var depth = zWorld + camDist;
        if (depth < 0.05)
            return null;

        var sx = cx + (focal * xWorld) / depth + offsetX;
        var sy = cy + (focal * (yWorld - camYOffset)) / depth + offsetY;
        return (sx, sy, depth);
    }
}
