using System;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Nagelt die Textausgabe von ExtractPrimaryDamages fest.
///
/// Das Feld "Primaere_Schaeden" geht in SchattenCodierungsHash ein: Aendert sich
/// sein Format, gilt die Schattenauswertung jeder neu eingelesenen Haltung als
/// veraltet. Die Ausgabe muss deshalb byteidentisch bleiben, auch wenn der
/// Parser darunter umgebaut wird.
/// </summary>
public sealed class PrimaryDamageRowParserCharacterizationTests
{
    [Fact]
    public void Standardformat_bleibt_unveraendert()
    {
        string[] zeilen =
        [
            "0.00 BCD Rohranfang",
            "1.20 BDB A Beginn TV-Untersuch",
            "12.30 BAB Riss laengs",
            "27.70 BCE Rohrende",
        ];

        Assert.Equal(
            "BCD @0.00m (Rohranfang)\n"
            + "BDB A @1.20m (Beginn TV-Untersuch)\n"
            + "BAB @12.30m (Riss laengs)\n"
            + "BCE @27.70m (Rohrende)",
            PrimaryDamageRowParser.ExtractPrimaryDamages(zeilen));
    }

    /// <summary>Fretz-Format: Fotonummer und Zeitstempel stehen VOR dem Meterwert.</summary>
    [Fact]
    public void Fretzformat_bleibt_unveraendert()
    {
        string[] zeilen =
        [
            "1777 00:00:09 0.00 BCD Rohranfang",
            "00:01:31  4.60  BCC.Y.B  Bogen nach rechts",
        ];

        Assert.Equal(
            "BCD @0.00m (Rohranfang)\n"
            + "BCC.Y.B @4.60m (Bogen nach rechts)",
            PrimaryDamageRowParser.ExtractPrimaryDamages(zeilen));
    }

    [Fact]
    public void Fortsetzungszeilen_werden_angehaengt_Rauschen_nicht()
    {
        string[] zeilen =
        [
            "5.50 BAB Riss im Scheitel",
            "ueber zwei Rohrverbindungen",
            "Seite 3",
            "foto_0042.jpg",
            "",
            "9.10 BBC Ablagerung",
        ];

        Assert.Equal(
            "BAB @5.50m (Riss im Scheitel ueber zwei Rohrverbindungen)\n"
            + "BBC @9.10m (Ablagerung)",
            PrimaryDamageRowParser.ExtractPrimaryDamages(zeilen));
    }

    [Fact]
    public void Ein_nachgestellter_Zeitstempel_bleibt_aus_der_Beschreibung_draussen()
    {
        string[] zeilen = ["12.30 BAB Riss laengs 00:05:09 weiteres"];

        Assert.Equal(
            "BAB @12.30m (Riss laengs)",
            PrimaryDamageRowParser.ExtractPrimaryDamages(zeilen));
    }

    [Fact]
    public void Ohne_Schaeden_bleibt_der_Text_leer()
        => Assert.Equal("", PrimaryDamageRowParser.ExtractPrimaryDamages(["Irgendein Kopftext", "Seite 1"]));
}
