using AuswertungPro.Next.Application.Reports;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Die Anzahl Fotos je Seite ist einstellbar. Die Anordnung stammt aus einer festen
/// Tabelle statt aus einer Formel, damit kein Wert das A4-Blatt still sprengt.
/// </summary>
public sealed class ProtocolPdfPhotoLayoutTests
{
    [Fact]
    public void Zwei_Fotos_je_Seite_behalten_die_bisherigen_Masse()
    {
        // Sicherheitsanker: der Standard darf das bestehende PDF nicht veraendern.
        var layout = ProtocolPdfPhotoLayout.Resolve(2);

        Assert.Equal(2, layout.PhotosPerPage);
        Assert.Equal(1, layout.Columns);
        Assert.Equal(2, layout.Rows);
        Assert.Equal(500f, layout.PhotoWidth);
        Assert.Equal(255f, layout.PhotoHeight);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(4, 2, 2)]
    [InlineData(6, 2, 3)]
    public void Jede_erlaubte_Anzahl_hat_ihre_eigene_Anordnung(int perPage, int columns, int rows)
    {
        var layout = ProtocolPdfPhotoLayout.Resolve(perPage);

        Assert.Equal(perPage, layout.PhotosPerPage);
        Assert.Equal(columns, layout.Columns);
        Assert.Equal(rows, layout.Rows);
        Assert.Equal(perPage, layout.Columns * layout.Rows);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(99)]
    public void Unbekannte_Werte_fallen_auf_zwei_zurueck(int? perPage)
    {
        var layout = ProtocolPdfPhotoLayout.Resolve(perPage);

        Assert.Equal(ProtocolPdfPhotoLayout.Resolve(2), layout);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Jede_Anordnung_passt_in_die_nutzbare_Blatthoehe(int perPage)
    {
        var layout = ProtocolPdfPhotoLayout.Resolve(perPage);

        var belegt = layout.Rows * (layout.PhotoHeight + ProtocolPdfPhotoLayout.CaptionHeight);

        Assert.True(
            belegt <= ProtocolPdfPhotoLayout.AvailableHeight,
            $"{perPage} Fotos je Seite belegen {belegt} von {ProtocolPdfPhotoLayout.AvailableHeight} Punkten.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Jede_Anordnung_passt_in_die_nutzbare_Blattbreite(int perPage)
    {
        var layout = ProtocolPdfPhotoLayout.Resolve(perPage);

        var belegt = layout.Columns * layout.PhotoWidth;

        Assert.True(
            belegt <= ProtocolPdfPhotoLayout.AvailableWidth,
            $"{perPage} Fotos je Seite belegen {belegt} von {ProtocolPdfPhotoLayout.AvailableWidth} Punkten Breite.");
    }

    [Fact]
    public void Die_erlaubten_Werte_stehen_zentral_und_sind_aufsteigend()
    {
        Assert.Equal(new[] { 1, 2, 4, 6 }, ProtocolPdfPhotoLayout.AllowedValues);
    }
}
