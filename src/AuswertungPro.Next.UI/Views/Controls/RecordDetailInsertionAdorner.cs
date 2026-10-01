using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>
/// Zeigt beim Ziehen an, wo das Gezogene landet: eine Linie ueber oder unter einer Karte,
/// beziehungsweise links oder rechts einer Spalte. Als Adorner gezeichnet, damit dabei
/// nichts verrutscht oder seine Groesse aendert.
/// </summary>
internal sealed class RecordDetailInsertionAdorner : Adorner
{
    private const double LineThickness = 3d;
    private readonly Pen _pen;

    internal RecordDetailInsertionAdorner(UIElement adornedElement, bool insertAfter, bool isVertical, Brush brush)
        : base(adornedElement)
    {
        InsertAfter = insertAfter;
        IsVertical = isVertical;
        IsHitTestVisible = false;

        var lineBrush = brush.CloneCurrentValue();
        lineBrush.Freeze();
        _pen = new Pen(lineBrush, LineThickness);
        _pen.Freeze();
    }

    /// <summary>Linie dahinter (true) oder davor (false).</summary>
    internal bool InsertAfter { get; }

    /// <summary>Senkrechte Linie fuer Spalten, waagerechte fuer Karten.</summary>
    internal bool IsVertical { get; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var size = AdornedElement.RenderSize;
        if (size.Width <= 0 || size.Height <= 0)
            return;

        // Halbe Linienstaerke nach innen, damit die Linie vollstaendig sichtbar bleibt.
        var offset = LineThickness / 2d;

        if (IsVertical)
        {
            var x = InsertAfter ? size.Width - offset : offset;
            drawingContext.DrawLine(_pen, new Point(x, 0), new Point(x, size.Height));
            return;
        }

        var y = InsertAfter ? size.Height - offset : offset;
        drawingContext.DrawLine(_pen, new Point(0, y), new Point(size.Width, y));
    }
}
