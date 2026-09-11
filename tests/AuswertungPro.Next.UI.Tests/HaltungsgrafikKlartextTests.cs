using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Tests;

public sealed class HaltungsgrafikKlartextTests
{
    [Fact]
    public void Symbol_und_Klartext_zeigen_Ereignisfotos_ohne_externen_Oeffner()
    {
        StaTestRunner.Run(() =>
        {
            var record = Haltung();
            var entries = record.Protocol!.Current.Entries;
            entries.Clear();
            entries.Add(new ProtocolEntry { Code = "BAB", MeterStart = 8, Beschreibung = "Zweiter", FotoPaths = ["zwei.jpg"] });
            entries.Add(new ProtocolEntry { Code = "BAB", MeterStart = 2, Beschreibung = "Erster", FotoPaths = ["eins.jpg"] });
            entries.Add(new ProtocolEntry { Code = "BAB", MeterStart = 8, Beschreibung = "Dritter", FotoPaths = ["drei.jpg"] });
            entries.Add(new ProtocolEntry { Code = "BAB", MeterStart = 9, Beschreibung = "Ohne Foto" });
            entries.Add(new ProtocolEntry { Code = "BAB", Beschreibung = "Ohne Meter", FotoPaths = ["niemals.jpg"] });
            entries.Add(new ProtocolEntry { Code = "BAB", MeterStart = 1, IsDeleted = true, FotoPaths = ["geloescht.jpg"] });
            var vorher = JsonSerializer.Serialize(record);
            var geoeffnet = new List<string>();
            var bild = HaltungsgrafikKlartextZeichner.Zeichne(record, null, new Grid(), 280, 700, null,
                paths => geoeffnet.AddRange(paths))!.Value.Flaeche;
            foreach (var symbol in bild.Children.OfType<Rectangle>())
            {
                if (PhotoHoverPreviewBehavior.GetIsEnabled(symbol))
                    geoeffnet.AddRange(PhotoHoverPreviewSelectors.ExtractPhotoPaths(symbol.DataContext,
                        PhotoHoverPreviewBehavior.GetPhotoPathsSelector(symbol))!);
                symbol.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            }
            Assert.Equal(new[] { "eins.jpg", "zwei.jpg", "drei.jpg" }, geoeffnet);
            geoeffnet.Clear();
            foreach (var text in bild.Children.OfType<TextBlock>().Where(t => t.Text.Contains("BAB")))
            {
                if (PhotoHoverPreviewBehavior.GetIsEnabled(text))
                    geoeffnet.AddRange(PhotoHoverPreviewSelectors.ExtractPhotoPaths(text.DataContext,
                        PhotoHoverPreviewBehavior.GetPhotoPathsSelector(text))!);
                text.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            }
            Assert.Equal(new[] { "eins.jpg", "zwei.jpg", "drei.jpg" }, geoeffnet);
            Assert.Equal(vorher, JsonSerializer.Serialize(record));
        });
    }

    [Theory]
    [InlineData("Schmutzwasser", "#7A6242")]
    [InlineData("Regenwasser", "#4A7FA5")]
    [InlineData("Reinabwasser", "#4A7FA5")]
    [InlineData("Mischabwasser", "#8E4A6E")]
    [InlineData("unbekannt", "#7A8A94")]
    public void Rohrfarbe_entspricht_der_Nutzungsart_und_dem_Bericht(string nutzung, string hex)
    {
        StaTestRunner.Run(() =>
        {
            var record = Haltung();
            record.SetFieldValue(FieldKeys.UsageType, nutzung, FieldSource.Manual, false);
            var bild = HaltungsgrafikKlartextZeichner.Zeichne(record, null, new Grid(), 280, 700, true)!.Value.Flaeche;
            var farbe = (Color)ColorConverter.ConvertFromString(hex);
            var rohr = Alle(bild).OfType<Rectangle>().First(r => r.Fill is LinearGradientBrush);
            Assert.All(((LinearGradientBrush)rohr.Fill).GradientStops, stop => Assert.Equal(farbe.R, stop.Color.R));
            var mitte = ((LinearGradientBrush)rohr.Fill).GradientStops.Single(s => s.Offset == 0.5).Color;
            Assert.Equal(farbe, mitte);
            Assert.Equal(hex, NutzungsartReportColors.Resolve(nutzung).Accent);
        });
    }

    [Fact]
    public void Ganze_Laenge_Klartext_und_Bezugslinie_bleiben_auch_beim_Vergroessern_masshaltig()
    {
        StaTestRunner.Run(() =>
        {
            var record = Haltung();
            var vorher = JsonSerializer.Serialize(record);
            foreach (var hoehe in new[] { 400, 800 })
            {
                var bild = HaltungsgrafikKlartextZeichner.Zeichne(record, null, new Grid(), 280, hoehe, null)!.Value;
                Assert.Equal(3, bild.Anzahl);
                Assert.Equal(hoehe, bild.Flaeche.Height);
                var linien = bild.Flaeche.Children.OfType<Polyline>().ToArray();
                Assert.Equal(3, linien.Length);
                Assert.Equal(56, linien[0].Points[0].Y, 3);
                Assert.Equal(hoehe - 44, linien[2].Points[0].Y, 3);
                Assert.Equal((56 + hoehe - 44) / 2d, linien[1].Points[0].Y, 3);
                Assert.Contains(Alle(bild.Flaeche).OfType<TextBlock>(), t => t.Text.Contains("Riss"));
                var ticks = Alle(bild.Flaeche).OfType<TextBlock>().Select(t => t.Text).ToArray();
                Assert.Contains(ticks, t => t is "5.00" or "5,00");
                Assert.Contains(ticks, t => t is "16.85" or "16,85");
            }
            Assert.Equal(vorher, JsonSerializer.Serialize(record));
        });
    }

    [Fact]
    public void Dichte_Beobachtungen_behalten_lesbare_Texte_ohne_Ueberlappung()
    {
        StaTestRunner.Run(() =>
        {
            var record = Haltung();
            record.Protocol!.Current.Entries.Clear();
            for (var i = 0; i < 30; i++)
                record.Protocol.Current.Entries.Add(new ProtocolEntry { Code = "BAB", MeterStart = 8,
                    Beschreibung = "Langer Klartext mit mehreren Worten fuer einen Riss in der Rohrwand" });
            var bild = HaltungsgrafikKlartextZeichner.Zeichne(record, null, new Grid(), 260, 500, null)!.Value;
            Assert.True(bild.Flaeche.Height > 1400);
            var texte = bild.Flaeche.Children.OfType<TextBlock>().Where(t => t.Text.Contains("BAB")).ToArray();
            Assert.Equal(30, texte.Length);
            for (var i = 1; i < texte.Length; i++)
                Assert.True(Canvas.GetTop(texte[i]) >= Canvas.GetTop(texte[i - 1]) + texte[i - 1].DesiredSize.Height + 7.9);
            Assert.All(texte, t => Assert.Equal(12, t.FontSize));
            Assert.True(Canvas.GetTop(texte[0]) >= 56);
            Assert.True(Canvas.GetTop(texte[^1]) + texte[^1].DesiredSize.Height <= bild.Flaeche.Height - 44);
        });
    }

    private static HaltungRecord Haltung()
    {
        var r = new HaltungRecord();
        r.SetFieldValue(FieldKeys.HoldingLengthMeters, "16.85", FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.HoldingName, "10392-9117", FieldSource.Manual, false);
        r.Protocol = new ProtocolDocument { Current = new ProtocolRevision() };
        foreach (var meter in new[] { 0, 8.425, 16.85 })
            r.Protocol.Current.Entries.Add(new ProtocolEntry { Code = "BAB", Beschreibung = "Riss", MeterStart = meter });
        return r;
    }

    private static IEnumerable<FrameworkElement> Alle(Canvas canvas)
    {
        foreach (var child in canvas.Children.OfType<FrameworkElement>())
        {
            yield return child;
            if (child is Canvas sub)
                foreach (var nested in Alle(sub)) yield return nested;
        }
    }
}
