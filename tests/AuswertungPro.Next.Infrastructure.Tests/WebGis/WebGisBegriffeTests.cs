using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>WebGIS-Begriffe, Schritt A (Entscheid Pascal 23.09.2026): gespeichert wird die WebGIS-Beschriftung.</summary>
public sealed class WebGisBegriffeTests
{
    [Theory]
    [InlineData(false, FieldKeys.OperatingStatus, "in_Betrieb", "In Betrieb")]
    [InlineData(false, FieldKeys.OperatingStatus, "ausser_Betrieb", "Ausser Betrieb")]
    [InlineData(false, FieldKeys.OperatingStatus, "tot", "Tot/Aufgehoben, verfüllt")]
    [InlineData(false, FieldKeys.OperatingStatus, "unbekannt", "Unbekannt")]
    [InlineData(false, FieldKeys.PositionAccuracy, "genau", "Genau")]
    [InlineData(false, FieldKeys.RehabilitationNeed, "mittelfristig", "Mittelfristig")]
    [InlineData(false, FieldKeys.RehabilitationNeed, "Saniert", "Saniert")]
    [InlineData(false, FieldKeys.UsageType, "Niederschlagsabwasser", "Regenabwasser")]
    [InlineData(false, FieldKeys.UsageType, "Regenwasser", "Regenabwasser")]
    [InlineData(false, FieldKeys.UsageType, "Bachwasser", "Bachabwasser")]
    [InlineData(false, FieldKeys.UsageType, "entlastetes Mischabwasser", "Entlastetes Mischabwasser")]
    [InlineData(false, FieldKeys.UsageType, "Schmutzwasser", "Schmutzabwasser")]
    [InlineData(false, FieldKeys.ProfileType, "Kreisprofil", "Kreisprofil (K)")]
    [InlineData(false, FieldKeys.ProfileType, "offenes_Profil", "Offenes Profil (OP)")]
    [InlineData(false, FieldKeys.ProfileType, "Spezialprofil", "Spezialprofil (S)")]
    [InlineData(false, FieldKeys.ProfileType, "Maulprofil", "Maulprofil (E)")]
    [InlineData(false, FieldKeys.ProfileType, "K", "Kreisprofil (K)")]
    [InlineData(false, FieldKeys.ProfileType, "Anderes (A)", "Andere (A)")]
    [InlineData(false, FieldKeys.HydraulicFunction, "Duekerleitung", "Dükerleitung")]
    [InlineData(false, FieldKeys.HydraulicFunction, "Spuelleitung", "Spülleitung")]
    [InlineData(false, FieldKeys.ConnectionType, "spiegelgeschweisst", "Spiegelgeschweisst")]
    [InlineData(false, FieldKeys.ConnectionType, "Ueberschiebmuffen", "Überschiebmuffen")]
    [InlineData(false, FieldKeys.BeddingEncasement, "in_Kanal_aufgehaengt", "In Kanal aufgehängt")]
    [InlineData(false, FieldKeys.BeddingEncasement, "SIA_Typ1", "SIA Typ1")]
    [InlineData(true, "Funktion", "Kontroll_Einsteigschacht", "Kontrollschacht")]
    [InlineData(true, "Funktion", "Pumpwerk", "Pumpenschacht")]
    [InlineData(true, "Funktion", "Oelabscheider", "Ölabscheider")]
    [InlineData(true, FieldKeys.PositionAccuracy, "ungenau", "Ungenau")]
    public void Bisherige_schreibweise_wird_zum_webgis_begriff(bool schacht, string feld, string bisher, string webgis)
        => Assert.Equal(webgis, WebGisBegriffe.Normalisieren(schacht, feld, bisher));

    [Theory]
    [InlineData(false, FieldKeys.OperatingStatus, "weitere")]
    [InlineData(false, FieldKeys.HydraulicFunction, "Versickerungsleitung")]
    [InlineData(true, "Funktion", "Absturzbauwerk")]
    [InlineData(true, "Funktion", "Trennbauwerk")]
    [InlineData(true, "Funktion", "Be-/Entlüftung")]
    [InlineData(true, "Funktion", "Sickerschacht")]
    [InlineData(true, "Funktion", "Spezialbauwerk")]
    public void Wert_ohne_webgis_gegenstueck_bleibt_unveraendert(bool schacht, string feld, string wert)
    {
        Assert.Equal(wert, WebGisBegriffe.Normalisieren(schacht, feld, wert));
        Assert.False(WebGisBegriffe.Fuer(schacht, feld)!.Kennt(wert));
    }

    [Fact]
    public void Webgis_begriff_bleibt_zeichengenau_und_leer_bleibt_leer()
    {
        var status = WebGisBegriffe.Fuer(false, FieldKeys.OperatingStatus)!;
        Assert.Equal("Tot/Aufgehoben, verfüllt", status.Normalisieren("Tot/Aufgehoben, verfüllt"));
        Assert.Equal("", status.Normalisieren("   "));
        Assert.True(status.Kennt(""));
        Assert.True(status.Kennt("In Betrieb"));
        Assert.False(status.Kennt("in_Betrieb"));
    }

    [Fact]
    public void Listen_kommen_aus_dem_webgis_katalog_ohne_leereintrag()
    {
        Assert.Equal(["Unbekannt", "In Betrieb", "Ausser Betrieb", "Tot/Aufgehoben, verfüllt"],
            WebGisBegriffe.Fuer(false, FieldKeys.OperatingStatus)!.Werte);
        Assert.Equal(["", "Unbekannt", "Ungenau", "Genau"], WebGisBegriffe.Fuer(true, FieldKeys.PositionAccuracy)!.Auswahl);
        Assert.Contains("Pumpenschacht", WebGisBegriffe.Fuer(true, "Funktion")!.Werte);
        Assert.DoesNotContain("", WebGisBegriffe.Fuer(false, FieldKeys.ConnectionType)!.Werte);
        Assert.Null(WebGisBegriffe.Fuer(false, FieldKeys.PipeMaterial)); // Material: Schritt B
    }

    [Fact]
    public void Keine_zwei_webgis_begriffe_einer_liste_falten_gleich()
    {
        foreach (var schacht in new[] { false, true })
        foreach (var feld in schacht ? WebGisBegriffe.SchachtFelder.Append(WebGisBegriffe.SchachtFunktion) : WebGisBegriffe.HaltungFelder)
        {
            var werte = WebGisBegriffe.Fuer(schacht, feld)!.Werte;
            var doppelt = werte.GroupBy(WebGisBegriffe.Falte).Where(g => g.Count() > 1).Select(g => string.Join("/", g)).ToList();
            Assert.True(doppelt.Count == 0, $"{feld}: {string.Join(", ", doppelt)}");
        }
    }
}
