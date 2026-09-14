using AuswertungPro.Next.Application.UseCases.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Liesmich-Datei ist das, was der Empfaenger zuerst liest. Sie muss die Zahlen genau
/// dieses Laufs nennen und darf nichts behaupten, was der Bericht nicht hergibt.
/// </summary>
public sealed class KatasterPaketLiesmichTests
{
    private const string AenderungsBericht = """
        Projekt: Beispiel
        Lieferart: nur Handaenderungen mit Feldauftraegen.
        Im Projekt: 19 Haltungen, 33 Schaechte.
        In die Datei: 19 Haltungen, 33 Schaechte (991 Objekte insgesamt).
        Hinweise:
          Deckel chA: GeoShop liefert keine Pflichtbezeichnung; Originalkennung als technische Bezeichnung verwendet.
          Deckel chB: GeoShop liefert keine Pflichtbezeichnung; Originalkennung als technische Bezeichnung verwendet.
        """;

    private const string VollBericht = """
        Projekt: Beispiel
        In die Datei: 19 Haltungen, 33 Schaechte (239 Objekte insgesamt).
        """;

    private static XtfPaketKennzahlen Kennzahlen() => new(
        "Beispiel", "Beispiel_Aenderungen_20260914.xtf", "Beispiel_20260914.xtf", 447,
        AenderungsBericht, VollBericht);

    [Fact]
    public void Nennt_Projekt_Zahlen_und_beide_Dateien()
    {
        var text = KatasterPaketLiesmich.Baue(Kennzahlen(), new DateTime(2026, 9, 14, 21, 30, 0, DateTimeKind.Local));

        Assert.Contains("Beispiel", text, StringComparison.Ordinal);
        Assert.Contains("14.09.2026", text, StringComparison.Ordinal);
        Assert.Contains("19 Haltungen", text, StringComparison.Ordinal);
        Assert.Contains("33 Schächte", text, StringComparison.Ordinal);
        Assert.Contains("447", text, StringComparison.Ordinal);
        Assert.Contains("Beispiel_Aenderungen_20260914.xtf", text, StringComparison.Ordinal);
        Assert.Contains("Beispiel_20260914.xtf", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sagt_dass_nur_die_bezeichneten_Aenderungen_Auftraege_sind()
    {
        var text = KatasterPaketLiesmich.Baue(Kennzahlen(), DateTime.Now);
        Assert.Contains("Aenderung", text, StringComparison.Ordinal);
        Assert.Contains("SewerStudio_Zusatz_2026.ili", text, StringComparison.Ordinal);
        // Der Empfaenger muss wissen, dass ohne diese Datei gar nichts lesbar ist.
        Assert.Contains("model(s) not found", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sagt_wo_der_neue_Wert_steht_und_verleitet_zu_keiner_Loeschung()
    {
        var text = KatasterPaketLiesmich.Baue(Kennzahlen(), DateTime.Now);
        // Ein Auftrag traegt NIE einen Wert; er steht am genannten Objekt. Die fruehere
        // Formulierung «fehlt bei einem Auftrag der Wert, Feld leeren» haette zum Leeren
        // aller beauftragten Felder verleitet — auch des Materials «Zement».
        Assert.DoesNotContain("Fehlt bei einem Auftrag der Wert", text, StringComparison.Ordinal);
        Assert.Contains("Der neue Wert steht", text, StringComparison.Ordinal);
        Assert.Contains("nicht im Auftrag", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Nennt_die_Deckel_ohne_Bezeichnung_mit_ihrer_echten_Anzahl()
    {
        var text = KatasterPaketLiesmich.Baue(Kennzahlen(), DateTime.Now);
        Assert.Contains("2 Deckel", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Zaehlt_dieselben_Deckel_nicht_doppelt()
    {
        // Beide Fassungen melden dieselben zwei Deckel.
        var beide = Kennzahlen() with { VollstaendigerBericht = AenderungsBericht };
        Assert.Contains("2 Deckel", KatasterPaketLiesmich.Baue(beide, DateTime.Now), StringComparison.Ordinal);
        Assert.DoesNotContain("4 Deckel", KatasterPaketLiesmich.Baue(beide, DateTime.Now), StringComparison.Ordinal);
    }

    [Fact]
    public void Laesst_den_Deckelhinweis_weg_wenn_der_Bericht_keinen_nennt()
    {
        var ohne = Kennzahlen() with { AenderungsBericht = "In die Datei: 1 Haltungen, 1 Schaechte (5 Objekte insgesamt)." };
        var text = KatasterPaketLiesmich.Baue(ohne, DateTime.Now);
        Assert.DoesNotContain("Deckel", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Kommt_ohne_erkennbare_Zahlen_aus_ohne_etwas_zu_erfinden()
    {
        var leer = new XtfPaketKennzahlen("Ohne Zahlen", "a.xtf", "b.xtf", 0, "", "");
        var text = KatasterPaketLiesmich.Baue(leer, DateTime.Now);
        Assert.Contains("Ohne Zahlen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Haltungen,", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Deckel", text, StringComparison.Ordinal);
    }
}
