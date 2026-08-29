using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class SchachtFunktionVokabularTests
{
    // Die 22 Werte der Modelldatei SIA405_Abwasser_2020_2_d_LV95.
    // Fettabscheider fehlt hier bewusst - siehe eigener Test weiter unten.
    [Theory]
    [InlineData("Absturzbauwerk")]
    [InlineData("andere")]
    [InlineData("Be_Entlueftung")]
    [InlineData("Behandlungsanlage")]
    [InlineData("Bodenablauf")]
    [InlineData("Dachwasserschacht")]
    [InlineData("Einlaufschacht")]
    [InlineData("Entwaesserungsrinne")]
    [InlineData("Entwaesserungsrinne_mit_Schlammsack")]
    [InlineData("Geleiseschacht")]
    [InlineData("Kombischacht")]
    [InlineData("Kontroll_Einsteigschacht")]
    [InlineData("Oelabscheider")]
    [InlineData("Pumpwerk")]
    [InlineData("Regenueberlauf")]
    [InlineData("Schlammsammler")]
    [InlineData("Schwimmstoffabscheider")]
    [InlineData("Spuelschacht")]
    [InlineData("Trennbauwerk")]
    [InlineData("unbekannt")]
    [InlineData("Vorbehandlungsanlage")]
    public void Jeder_Modellwert_kommt_unveraendert_zurueck(string norm)
    {
        Assert.Equal(norm, SchachtFunktionVokabular.NachNorm(SchachtFunktionVokabular.Normalisieren(norm)));
    }

    [Theory]
    [InlineData("Kontrollschacht", "Kontroll_Einsteigschacht")]
    [InlineData("Schlammsammler", "Schlammsammler")]
    [InlineData("Einlaufschacht mit Schlammsammler", "Einlaufschacht")]
    [InlineData("Einlaufschacht Schluck", "Einlaufschacht")]
    [InlineData("Dachwasserschacht", "Dachwasserschacht")]
    [InlineData("Pumpenschacht", "Pumpwerk")]
    [InlineData("Ölabscheider", "Oelabscheider")]
    public void Die_SchachtPro_Begriffe_finden_ihren_Normwert(string schachtPro, string norm)
    {
        Assert.Equal(norm, SchachtFunktionVokabular.NachNorm(SchachtFunktionVokabular.Normalisieren(schachtPro)));
    }

    [Theory]
    [InlineData("Einlaufschacht mit Schlammsammler")]
    [InlineData("Einlaufschacht Schluck")]
    public void Beide_Einlaufschaechte_heissen_im_Programm_gleich(string schachtPro)
    {
        // Entscheid Pascal: beide gehen auf Einlaufschacht. Der Schlammsammler wird
        // dadurch nicht doppelt gezaehlt.
        Assert.Equal("Einlaufschacht", SchachtFunktionVokabular.Normalisieren(schachtPro));
    }

    [Theory]
    [InlineData("Sickerschacht")]
    [InlineData("Spezialbauwerk")]
    [InlineData("Fettabscheider")]
    public void Ohne_passenden_Normwert_bleibt_der_Begriff_im_Programm_erhalten(string schachtPro)
    {
        // Wichtig: "andere" ist der Wert fuer die XTF, nicht fuer die Anzeige.
        // In SewerStudio soll weiterhin stehen, was erfasst wurde.
        Assert.Equal(schachtPro, SchachtFunktionVokabular.Normalisieren(schachtPro));
        Assert.Equal("andere", SchachtFunktionVokabular.NachNorm(schachtPro));
    }

    [Fact]
    public void Fettabscheider_geht_bewusst_auf_andere_obwohl_es_ihn_im_Modell_gibt()
    {
        // Entscheid Pascal 2026-08-29. Das Modell kennt "Fettabscheider", der
        // AWU-Bestand benutzt ihn aber in 64420 Schaechten kein einziges Mal.
        // Deshalb wie AWU: andere. Folge - ein aus einer XTF gelesener
        // "Fettabscheider" kaeme beim Schreiben als "andere" zurueck. Praktisch
        // folgenlos, weil der Export nur handgeaenderte Felder schreibt und der
        // Wert im Bestand nicht vorkommt.
        Assert.Equal("andere", SchachtFunktionVokabular.NachNorm("Fettabscheider"));
    }

    [Fact]
    public void Ein_unbekannter_Begriff_bleibt_stehen_und_liefert_keinen_Normwert()
    {
        Assert.Equal("Wasserschloss", SchachtFunktionVokabular.Normalisieren("Wasserschloss"));
        Assert.Null(SchachtFunktionVokabular.NachNorm("Wasserschloss"));
    }

    [Theory]
    [InlineData("Kontrollschacht")]      // 85x im Projekt Zone 1.15
    [InlineData("Einstiegschacht")]      // 1x - mit ie geschrieben
    public void Werte_aus_einem_echten_Projekt_werden_erkannt(string gespeichert)
    {
        Assert.Equal("Kontroll_Einsteigschacht", SchachtFunktionVokabular.NachNorm(gespeichert));
    }
}
