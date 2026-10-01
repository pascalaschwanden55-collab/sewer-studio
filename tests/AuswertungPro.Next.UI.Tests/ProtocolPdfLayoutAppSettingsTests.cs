using System.IO;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die Einstellung "Fotos pro Seite" kommt aus den Programmeinstellungen. Ein fehlender,
/// unbekannter oder unlesbarer Wert darf den PDF-Export nie stoppen, sondern faellt still
/// auf den bisherigen Stand mit zwei Fotos je Seite zurueck.
/// </summary>
public sealed class ProtocolPdfLayoutAppSettingsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(6, 6)]
    public void Ein_erlaubter_Wert_wird_durchgereicht(int gespeichert, int erwartet)
    {
        var settings = new AppSettingsProtocolPdfLayoutSettings(() => gespeichert);

        Assert.Equal(erwartet, settings.PhotosPerPage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-5)]
    [InlineData(500)]
    public void Ein_unbekannter_Wert_faellt_auf_zwei_zurueck(int? gespeichert)
    {
        var settings = new AppSettingsProtocolPdfLayoutSettings(() => gespeichert);

        Assert.Equal(2, settings.PhotosPerPage);
    }

    [Fact]
    public void Unlesbare_Einstellungen_stoppen_den_Export_nicht()
    {
        var settings = new AppSettingsProtocolPdfLayoutSettings(
            () => throw new IOException("settings.json nicht lesbar"));

        Assert.Equal(2, settings.PhotosPerPage);
    }

    [Fact]
    public void Die_Einstellung_wird_bei_jedem_Zugriff_neu_gelesen()
    {
        // Der Erzeuger lebt so lange wie das Programm - eine Aenderung muss ohne
        // Neustart beim naechsten PDF wirken.
        var wert = 2;
        var settings = new AppSettingsProtocolPdfLayoutSettings(() => wert);

        Assert.Equal(2, settings.PhotosPerPage);
        wert = 6;
        Assert.Equal(6, settings.PhotosPerPage);
    }

    [Fact]
    public void Das_laufende_AppSettings_Objekt_wird_ohne_neues_Laden_gelesen()
    {
        var appSettings = new AppSettings { ProtocolPhotosPerPage = 2 };
        var settings = new AppSettingsProtocolPdfLayoutSettings(appSettings);

        Assert.Equal(2, settings.PhotosPerPage);

        appSettings.ProtocolPhotosPerPage = 4;

        Assert.Equal(4, settings.PhotosPerPage);
    }
}
