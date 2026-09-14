using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Buerglen 14.09.2026, Sichtprobe Pascal: Die Vorschau der Aenderungslieferung zeigte keine
/// einzige der 534 Aenderungen, dafuer 483 Meldungen ueber fehlende Felder, und behauptete
/// «Diese Datei erhaelt neue Kennungen». Die Datei behaelt die Original-TIDs.
/// </summary>
public sealed class XtfDssVorschauTests
{
    [Fact]
    public void Aenderungslieferung_zeigt_die_Feldauftraege_mit_Alt_und_Neu()
    {
        var p = XtfDssVerbundTests.Verbund();
        var s = p.SchaechteData[0];
        p.Objektakten.Single(a => a.Id == s.Id).Quellen.Single(q => q.Klasse == "Normschacht").Werte["Bemerkung"] = "Alter Text";
        s.SetFieldValue(FieldKeys.Remarks, "Neu saniert", FieldSource.Manual, true);

        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true, NurAenderungen: true));
        Assert.True(r.Ok, r.Fehler);

        var v = XtfExportVorschau.AusBericht("Probe", r.Bericht, r.Aenderungen);
        Assert.True(v.HatZeilen);
        var zeile = Assert.Single(v.Zeilen, z => z.Feld == "Bemerkung" && z.Alt == "Alter Text");
        Assert.Equal("Neu saniert", zeile.Neu);
        Assert.Contains("B", zeile.Objekt, StringComparison.Ordinal);
        Assert.Contains("Feldauftr", v.Zusammenfassung, StringComparison.Ordinal);
        Assert.Contains("Originalkennungen bleiben erhalten", v.Zusammenfassung, StringComparison.Ordinal);
    }

    [Fact]
    public void Vorschau_und_Bericht_nennen_dieselbe_Auftragszahl()
    {
        var p = XtfDssVerbundTests.Verbund();
        p.SchaechteData[0].SetFieldValue(FieldKeys.ConditionClass, "Keine Mängel (Z4)", FieldSource.Manual, true);

        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true, NurAenderungen: true));
        Assert.True(r.Ok, r.Fehler);

        // Der Bericht nennt die Gesamtzahl; die Vorschau muss auf dieselbe kommen, sonst
        // steht in der Liesmich-Datei eine andere Zahl als in der Datei.
        var zeile = Assert.Single(r.Bericht.Split('\n').Where(z => z.Contains("Feldaufträge an Original-TIDs", StringComparison.Ordinal)));
        var zahl = int.Parse(new string(zeile.SkipWhile(c => !char.IsAsciiDigit(c)).TakeWhile(char.IsAsciiDigit).ToArray()),
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(zahl, r.Aenderungen.Count);
        // Die Eingabepakete sind Auftraege und stehen mit in der Tabelle.
        Assert.Contains(r.Aenderungen, a => a.Feld.StartsWith("Zusatz:", StringComparison.Ordinal));
        Assert.Contains(XtfExportVorschau.AusBericht("Probe", r.Bericht, r.Aenderungen).Zeilen,
            z => z.Feld == "Erfasste Angaben");
    }

    [Fact]
    public void Geleertes_Feld_erscheint_in_der_Vorschau_als_entfernt()
    {
        var p = XtfDssVerbundTests.Verbund();
        var s = p.SchaechteData[0];
        p.Objektakten.Single(a => a.Id == s.Id).Quellen.Single(q => q.Klasse == "Normschacht").Werte["Bemerkung"] = "Alter Text";
        s.SetFieldValue(FieldKeys.Remarks, "", FieldSource.Manual, true);

        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true, NurAenderungen: true));
        Assert.True(r.Ok, r.Fehler);
        var v = XtfExportVorschau.AusBericht("Probe", r.Bericht, r.Aenderungen);
        Assert.Contains(v.Zeilen, z => z.Feld == "Bemerkung" && z.Alt == "Alter Text" && z.Neu == "(entfernt)");
    }

    [Fact]
    public void Vollstaendige_Lieferung_hat_keine_Auftragstabelle()
    {
        var p = XtfDssVerbundTests.Verbund();
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.True(r.Ok, r.Fehler);
        Assert.Empty(r.Aenderungen);
        Assert.False(XtfExportVorschau.AusBericht("Probe", r.Bericht, r.Aenderungen).HatZeilen);
    }

    [Fact]
    public void Hinweis_nennt_den_Schachtnamen_und_nicht_die_erste_beste_Quelle()
    {
        var p = XtfDssVerbundTests.Verbund();
        var s = p.SchaechteData[0];
        // Wie in Buerglen: Der GeoShop-Leser haengt den ganzen Verbund an die Akte; die
        // erste Quelle ist dort eine Haltung, nicht der Schacht.
        var akte = p.Objektakten.Single(a => a.Id == s.Id);
        akte.Quellen.Insert(0, p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung"));
        s.SetFieldValue(FieldKeys.ShaftShape, "Oval", FieldSource.Manual, true);

        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.True(r.Ok, r.Fehler);
        Assert.Contains("schacht «B»", r.Bericht, StringComparison.Ordinal);
        Assert.DoesNotContain("schacht «A-B»", r.Bericht, StringComparison.Ordinal);
    }

    [Fact]
    public void Viele_gleichartige_Luecken_werden_in_der_Kurzansicht_gebuendelt()
    {
        var bericht = "In die Datei: 1 Haltungen, 33 Schaechte (991 Objekte insgesamt).\n" + string.Join("\n",
            Enumerable.Range(1, 30).Select(i => $"  schacht «{i}»: Form = „Rund“ fehlt in der XTF, weil kein belegtes DSS-Zielfeld besteht; bleibt in Projekt/Objektakten-JSON.")
            .Concat(Enumerable.Range(1, 12).Select(i => $"  schacht «{i}»: Tiefe [m] = „1.80“ fehlt in der XTF, weil kein belegtes DSS-Zielfeld besteht; bleibt in Projekt/Objektakten-JSON.")));

        var v = XtfExportVorschau.AusBericht("Probe", bericht);
        Assert.Equal(42, v.Warnungen.Count); // Details bleiben vollstaendig
        Assert.Contains(v.KurzeWarnungen, w => w.Contains("Form", StringComparison.Ordinal) && w.Contains("30", StringComparison.Ordinal));
        Assert.Contains(v.KurzeWarnungen, w => w.Contains("Tiefe [m]", StringComparison.Ordinal) && w.Contains("12", StringComparison.Ordinal));
        Assert.DoesNotContain(v.KurzeWarnungen, w => w.Contains("und 39 weitere", StringComparison.Ordinal));
    }
}
