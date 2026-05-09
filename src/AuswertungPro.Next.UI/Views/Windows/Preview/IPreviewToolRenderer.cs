using System;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows.Preview;

// Strategy-Pattern fuer das Vorschau-Rendering pro OverlayToolType
// (ADR-Punkt 2 / Slice 8a.2.11). Ersetzt schrittweise den ~196 Zeilen
// switch in CodingModeWindow.RenderPreview. Renderer leben in
// CodingModeWindow.xaml.cs (UI-Layer) — sie muessen WPF-Shapes auf
// einer Canvas erzeugen und kennen den OverlayService nur lesend.

/// <summary>
/// Renderer fuer die Vorschau eines bestimmten OverlayToolType-Werkzeugs
/// waehrend der Benutzer mit der Maus zieht.
/// </summary>
internal interface IPreviewToolRenderer
{
    /// <summary>Welches Werkzeug bedient dieser Renderer.</summary>
    OverlayToolType ToolType { get; }

    /// <summary>Zeichnet die Vorschau-Shapes mit Tag <see cref="PreviewRenderContext.PreviewTag"/>
    /// auf <see cref="PreviewRenderContext.Canvas"/>. Cleanup laeuft separat ueber Tag-Lookup.</summary>
    void Render(PreviewRenderContext ctx);
}

/// <summary>
/// Kontext fuer einen einzelnen Render-Pass. Enthaelt alles, was die
/// Renderer brauchen — keine Window-Class-Referenz.
/// </summary>
internal sealed record PreviewRenderContext(
    Canvas Canvas,
    NormalizedPoint Start,
    NormalizedPoint Current,
    Func<NormalizedPoint, Point> ToPixel,
    IOverlayToolService OverlayService,
    OverlayGeometry? CurrentOverlay,
    string PreviewTag);
