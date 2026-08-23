using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Common;

using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>Ein Bild, das an die Stelle eines Platzhalters gehoert.</summary>
/// <param name="PlaceholderName">Name ohne Klammern, z.B. "Logo".</param>
/// <param name="ImagePath">Pfad zur Bilddatei.</param>
/// <param name="MaxWidthCm">Breite im Dokument in Zentimetern.</param>
public sealed record DocxImagePlacement(
    string PlaceholderName,
    string ImagePath,
    double MaxWidthCm);

/// <summary>
/// Ersetzt Platzhalter der Form <c>{{@Name}}</c> durch ein eingebettetes Bild.
///
/// Dasselbe Problem wie beim Textfueller: Word zerlegt den Platzhalter in
/// mehrere Textstuecke. Deshalb wird der Absatztext zuerst zusammengesetzt.
///
/// Wichtig ist die Reihenfolge im Aufrufer: dieser Fueller laeuft VOR
/// <see cref="DocxPlaceholderFiller"/>. Sonst wuerde der Textfueller den
/// Bildplatzhalter als unbekannten Textplatzhalter leeren, und das Bild fehlte
/// im fertigen Dossier ohne jede Meldung.
///
/// Fehlt die Bilddatei oder ist sie unlesbar, wird der Platzhalter trotzdem
/// entfernt: eine geschweifte Klammer darf der Eigentuemer nie zu sehen
/// bekommen.
/// </summary>
public static class DocxImagePlaceholderFiller
{
    /// <summary>Kennzeichnet einen Bildplatzhalter.</summary>
    public const string MarkerPrefix = "{{@";

    private const long EmuPerCm = 360_000L;

    public static void Fill(
        WordprocessingDocument document,
        IReadOnlyList<DocxImagePlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(placements);

        var mainPart = document.MainDocumentPart
            ?? throw new InvalidOperationException("Die Word-Vorlage hat keinen Hauptteil.");

        var body = mainPart.Document?.Body;
        if (body is null)
            return;

        var drawingId = 1U;

        foreach (var placement in placements)
        {
            if (string.IsNullOrWhiteSpace(placement.PlaceholderName))
                continue;

            var marker = MarkerPrefix + placement.PlaceholderName.Trim() + "}}";

            foreach (var paragraph in body.Descendants<Paragraph>().ToList())
            {
                var texts = paragraph.Descendants<Text>().ToList();
                if (texts.Count == 0)
                    continue;

                var combined = string.Concat(texts.Select(t => t.Text));
                if (!combined.Contains(marker, StringComparison.Ordinal))
                    continue;

                // Den Marker immer entfernen, auch wenn kein Bild folgt.
                for (var i = 1; i < texts.Count; i++)
                    texts[i].Text = string.Empty;

                texts[0].Text = combined.Replace(marker, string.Empty, StringComparison.Ordinal);
                texts[0].Space = SpaceProcessingModeValues.Preserve;

                var run = texts[0].Ancestors<Run>().FirstOrDefault();
                if (run is null)
                    continue;

                var drawing = TryCreateDrawing(mainPart, placement, drawingId);
                if (drawing is null)
                    continue;

                run.AppendChild(drawing);
                drawingId++;
            }
        }
    }

    /// <summary>
    /// Legt den Bildteil an und baut das Zeichnungselement. Liefert null, wenn
    /// die Datei fehlt, unlesbar ist oder ihre Masse nicht erkannt werden.
    /// </summary>
    private static Drawing? TryCreateDrawing(
        MainDocumentPart mainPart,
        DocxImagePlacement placement,
        uint drawingId)
    {
        byte[] bytes;
        try
        {
            if (string.IsNullOrWhiteSpace(placement.ImagePath) || !File.Exists(placement.ImagePath))
                return null;

            bytes = File.ReadAllBytes(placement.ImagePath);
        }
        catch (Exception ex)
        {
            // Ein unlesbares Logo darf das ganze Dossier nicht verhindern.
            BestEffort.ReportWarning(
                $"[Dossiers] Bild '{placement.ImagePath}' nicht lesbar: {ex.Message}");
            return null;
        }

        if (!ImageSizeReader.TryRead(bytes, out var pixelWidth, out var pixelHeight))
        {
            BestEffort.ReportWarning(
                $"[Dossiers] Bildmasse von '{placement.ImagePath}' nicht erkannt.");
            return null;
        }

        var partType = ResolvePartType(placement.ImagePath);
        if (partType is null)
            return null;

        // ABWEICHUNG vom Brief: ImagePartType ist in OpenXml 3.1.1 keine Enum
        // mehr, sondern eine statische Klasse mit PartTypeInfo-Feldern
        // (ImagePartType.Png ist vom Typ PartTypeInfo). AddImagePart nimmt
        // deshalb PartTypeInfo statt ImagePartType entgegen.
        var imagePart = mainPart.AddImagePart(partType.Value);
        using (var source = new MemoryStream(bytes))
            imagePart.FeedData(source);

        var relationshipId = mainPart.GetIdOfPart(imagePart);

        var widthEmu = (long)Math.Round(placement.MaxWidthCm * EmuPerCm);
        var heightEmu = (long)Math.Round(widthEmu * (double)pixelHeight / pixelWidth);

        return BuildDrawing(relationshipId, widthEmu, heightEmu, drawingId, placement.PlaceholderName);
    }

    private static PartTypeInfo? ResolvePartType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ImagePartType.Png,
            ".jpg" or ".jpeg" => ImagePartType.Jpeg,
            _ => null
        };

    private static Drawing BuildDrawing(
        string relationshipId,
        long widthEmu,
        long heightEmu,
        uint drawingId,
        string name) => new(
        new DW.Inline(
            new DW.Extent { Cx = widthEmu, Cy = heightEmu },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = drawingId, Name = name },
            new DW.NonVisualGraphicFrameDrawingProperties(
                new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = name },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relationshipId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                            new A.PresetGeometry(new A.AdjustValueList())
                            {
                                Preset = A.ShapeTypeValues.Rectangle
                            })))
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                }))
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        });
}
