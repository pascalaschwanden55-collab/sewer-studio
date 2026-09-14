using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Application.UseCases.Xtf;

/// <summary>Die drei Ordner eines Pakets. Der Paketordner wird am Schluss gepackt.</summary>
public sealed record XtfPaketOrt(string Paketordner, string AenderungenOrdner, string VollstaendigOrdner);

/// <summary>Was ausser den XTF-Dateien ins Paket gehoert: die Erklaerung und beide Berichte.</summary>
public sealed record XtfPaketInhalte(string Liesmich, string AenderungsBericht, string VollstaendigerBericht);

/// <summary>
/// Legt den Paketordner an, schreibt die Liesmich-Datei und packt das Paket. Getrennt vom
/// Ablauf, damit dieser ohne Dateizugriff pruefbar bleibt.
/// </summary>
public interface IXtfPaketAblage
{
    /// <summary>Legt einen neuen, noch freien Paketordner mit seinen zwei Unterordnern an.</summary>
    XtfPaketOrt Beginne(string zielordner, string projektname);

    /// <summary>Schreibt Erklaerung und Berichte und packt das Paket; liefert den Pfad der ZIP.</summary>
    string Schliesse(XtfPaketOrt ort, XtfPaketInhalte inhalte);

    /// <summary>Entfernt ein angefangenes Paket wieder. Nur der eigene, in diesem Lauf angelegte Ordner.</summary>
    void Verwirf(XtfPaketOrt ort);
}

/// <summary>
/// Der Ablauf hinter «Paket für GEONIS erstellen»: Beide Fassungen werden zuerst geprueft,
/// der Mensch bestaetigt einmal, dann werden beide geschrieben und gepackt.
///
/// Warum zwei Fassungen: Ein fremdes Modell im Transfer macht die ganze Datei unlesbar, wenn
/// der Empfaenger sein .ili nicht abgelegt hat. Die Aenderungslieferung traegt die Feldauftraege
/// und braucht das Zusatzmodell; die reine Normlieferung kommt ohne aus. Scheitert eine der
/// beiden, bleibt kein halbes Paket liegen — es koennte sonst versehentlich verschickt werden.
/// </summary>
public static class XtfKatasterPaketUseCase
{
    public const string Titel = "Paket für GEONIS erstellen";

    public static XtfExportErgebnis Execute(
        IXtfNeuExportService dienst,
        IXtfPaketAblage ablage,
        XtfNeuExportRequest request,
        XtfExportActions actions)
    {
        ArgumentNullException.ThrowIfNull(dienst);
        ArgumentNullException.ThrowIfNull(ablage);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var aenderung = dienst.Erzeuge(Aenderungen(request) with { NurPruefen = true });
        if (aenderung.QuelleFehlt)
        {
            var gewaehlt = actions.WaehleQuelldateien();
            if (gewaehlt.Count == 0) return new(false, "Abgebrochen — keine Original-XTF zum Ergänzen gewählt.", null);
            request = request with { Quelldateien = gewaehlt };
            aenderung = dienst.Erzeuge(Aenderungen(request) with { NurPruefen = true });
        }

        if (Gescheitert(aenderung, "Die Prüfung der Änderungslieferung ist fehlgeschlagen.") is { } fehler1)
            return Melde(actions, fehler1);

        var voll = dienst.Erzeuge(Vollstaendig(request) with { NurPruefen = true });
        if (Gescheitert(voll, "Die Prüfung der vollständigen Lieferung ist fehlgeschlagen.") is { } fehler2)
            return Melde(actions, fehler2);

        var vorschau = XtfExportVorschau.AusBericht(Titel, aenderung.Bericht, aenderung.Aenderungen);
        vorschau = vorschau with
        {
            Zusammenfassung = vorschau.Zusammenfassung
                + "\nEs entstehen zwei Fassungen in einem Paket: die Änderungslieferung mit den Feldaufträgen"
                + " und eine reine Normdatei, die der Empfänger ohne unser Zusatzmodell lesen kann."
        };
        if (!actions.BestaetigeVorschau(vorschau))
            return new XtfExportErgebnis(false, "Abgebrochen — nichts geschrieben.", null);

        var ort = ablage.Beginne(request.ZielOrdner, request.Projekt?.Name ?? "Projekt");
        var geschriebeneAenderung = dienst.Erzeuge(Aenderungen(request) with { ZielOrdner = ort.AenderungenOrdner });
        if (Gescheitert(geschriebeneAenderung, "Die Änderungslieferung wurde nicht geschrieben.", brauchtDatei: true) is { } fehler3)
        {
            ablage.Verwirf(ort);
            return Melde(actions, fehler3, "Paket nicht erstellt.");
        }

        var geschriebeneVolle = dienst.Erzeuge(Vollstaendig(request) with { ZielOrdner = ort.VollstaendigOrdner });
        if (Gescheitert(geschriebeneVolle, "Die vollständige Lieferung wurde nicht geschrieben.", brauchtDatei: true) is { } fehler4)
        {
            ablage.Verwirf(ort);
            return Melde(actions, fehler4, "Paket nicht erstellt.");
        }

        var liesmich = KatasterPaketLiesmich.Baue(new XtfPaketKennzahlen(
            request.Projekt?.Name ?? "Projekt",
            Path.GetFileName(geschriebeneAenderung.Datei) ?? "",
            Path.GetFileName(geschriebeneVolle.Datei) ?? "",
            geschriebeneAenderung.Aenderungen.Count,
            geschriebeneAenderung.Bericht,
            geschriebeneVolle.Bericht), DateTime.Now);

        var zip = ablage.Schliesse(ort, new XtfPaketInhalte(
            liesmich, geschriebeneAenderung.Bericht, geschriebeneVolle.Bericht));
        return new XtfExportErgebnis(true, $"Paket für GEONIS erstellt: {Path.GetFileName(zip)}", ort.Paketordner);
    }

    private static XtfNeuExportRequest Aenderungen(XtfNeuExportRequest r)
        => r with { NurPruefen = false, NurAenderungen = true, MitZusatzangaben = true };

    /// <summary>Ohne Zusatzmodell — sonst braucht der Empfänger auch hier unsere .ili.</summary>
    private static XtfNeuExportRequest Vollstaendig(XtfNeuExportRequest r)
        => r with { NurPruefen = false, NurAenderungen = false, MitZusatzangaben = false };

    /// <summary>Null heisst in Ordnung; sonst die fertige Fehleranzeige.</summary>
    private static XtfExportVorschau? Gescheitert(XtfNeuExportResult ergebnis, string ersatztext, bool brauchtDatei = false)
        => ergebnis.Ok && !(brauchtDatei && string.IsNullOrWhiteSpace(ergebnis.Datei))
            ? null
            : XtfExportVorschau.Fehler(Titel,
                string.IsNullOrWhiteSpace(ergebnis.Fehler) ? ersatztext : ergebnis.Fehler, ergebnis.Bericht);

    private static XtfExportErgebnis Melde(XtfExportActions actions, XtfExportVorschau fehler, string meldung = "Prüfung nicht bestanden — nichts geschrieben.")
    {
        actions.ZeigeFehler(fehler);
        return new XtfExportErgebnis(false, meldung, null);
    }
}
