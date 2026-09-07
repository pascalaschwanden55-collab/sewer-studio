using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>Fertige Anzeige-Zeile fuer einen Bogen im Rohrring: Pfad, Farbe und Tooltip.</summary>
public sealed record BogenAnzeige(Geometry Pfad, Brush Pinsel, string Tooltip);

/// <summary>
/// Rohrquerschnitt mit bis zu drei Uhrlagen-Boegen (Inventar 5.1). Reine Darstellung: Die
/// Geometrie kommt aus <see cref="RohrringGeometrie"/>, die Farbe je Stufe aus den
/// Severity-Themebrushes. 0 Grad = 12 Uhr, im Uhrzeigersinn.
/// </summary>
public partial class RohrringControl : UserControl
{
    private const double MittelpunktX = 100;
    private const double MittelpunktY = 60;
    private const double Radius = 50;

    public RohrringControl() => InitializeComponent();

    public static readonly DependencyProperty EntriesProperty = DependencyProperty.Register(
        nameof(Entries), typeof(IReadOnlyList<ProtocolEntry>), typeof(RohrringControl),
        new PropertyMetadata(null, OnEntriesChanged));

    /// <summary>Primaere Schaeden der gewaehlten Haltung; bestimmt Anzahl und Lage der Boegen.</summary>
    public IReadOnlyList<ProtocolEntry>? Entries
    {
        get => (IReadOnlyList<ProtocolEntry>?)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    private static readonly DependencyPropertyKey BoegenPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Boegen), typeof(IReadOnlyList<BogenAnzeige>), typeof(RohrringControl),
        new PropertyMetadata(Array.Empty<BogenAnzeige>()));

    public static readonly DependencyProperty BoegenProperty = BoegenPropertyKey.DependencyProperty;

    /// <summary>Fertig berechnete Anzeige-Boegen fuer das ItemsControl im XAML.</summary>
    public IReadOnlyList<BogenAnzeige> Boegen => (IReadOnlyList<BogenAnzeige>)GetValue(BoegenProperty);

    private static void OnEntriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RohrringControl)d;
        var entries = e.NewValue as IReadOnlyList<ProtocolEntry> ?? Array.Empty<ProtocolEntry>();
        var boegen = RohrringGeometrie.Boegen(entries)
            .Select(b => new BogenAnzeige(control.PfadFuer(b), control.PinselFuer(b.Stufe), b.Tooltip))
            .ToList();
        control.SetValue(BoegenPropertyKey, boegen);
    }

    private Geometry PfadFuer(RohrringBogen b)
    {
        var startRad = b.StartGrad * Math.PI / 180.0;
        var endRad = (b.StartGrad + b.SweepGrad) * Math.PI / 180.0;
        var start = new Point(MittelpunktX + Radius * Math.Sin(startRad), MittelpunktY - Radius * Math.Cos(startRad));
        var ende = new Point(MittelpunktX + Radius * Math.Sin(endRad), MittelpunktY - Radius * Math.Cos(endRad));
        var figur = new PathFigure { StartPoint = start, IsClosed = false };
        figur.Segments.Add(new ArcSegment(ende, new Size(Radius, Radius), 0, b.SweepGrad > 180,
            SweepDirection.Clockwise, isStroked: true));
        var geometrie = new PathGeometry();
        geometrie.Figures.Add(figur);
        geometrie.Freeze();
        return geometrie;
    }

    private Brush PinselFuer(int stufe)
        => TryFindResource($"Severity{stufe}Brush") as Brush ?? Brushes.Gray;
}
