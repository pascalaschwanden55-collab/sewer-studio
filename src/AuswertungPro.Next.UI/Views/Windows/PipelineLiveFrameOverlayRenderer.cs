using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Bewahrt die Lebenszyklusregeln des eingebetteten Live-Overlays und delegiert
/// die eigentliche Ring-Zeichnung an den gemeinsamen Renderer.
/// </summary>
internal static class PipelineLiveFrameOverlayRenderer
{
    internal static void Render(
        Canvas canvas,
        bool hasFrame,
        IReadOnlyList<LiveFrameFinding> findings,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(findings);

        // Vorhandenes Bild waehrend einer kurzen Layoutphase nicht wegloeschen.
        if (width < 60 || height < 60)
            return;

        canvas.Children.Clear();
        if (!hasFrame)
            return;

        var visibleFindings = findings.Count <= 8
            ? findings
            : findings.Take(8).ToArray();

        LiveFrameRingOverlayRenderer.Draw(
            canvas,
            visibleFindings,
            LiveFrameRingOverlayMode.Compact,
            width,
            height);
    }
}
