using System.Globalization;
using System.Xml.Linq;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.VsaKek;

/// <summary>
/// Eine <c>Untersuchung</c> der Datei, Werte so wie sie dort stehen.
///
/// ACHTUNG Importbeleg: Der Schacht- und Fassungs-<c>ImportFingerprint</c> ist SHA-256 ueber
/// die JSON-Form dieser Klasse samt <see cref="Schaeden"/> und <see cref="Schachtschaeden"/>
/// und steht in gespeicherten Projekten. Name, Reihenfolge und Typ der Eigenschaften
/// (auch in <see cref="VsaKekKanalschaden"/> und <see cref="VsaKekSchachtschaden"/>) nie
/// aendern, keine Eigenschaft ergaenzen — sonst erkennt ein Wiederholungsimport bereits
/// abgelegte Begehungen nicht mehr. Waechter: <c>XtfVsaKekFingerabdruckTests</c>.
/// Bis 30.09.2026 hiess die Klasse <c>LegacyXtfImportService.Untersuchung</c>; der
/// Klassenname geht nicht in den Beleg ein.
/// </summary>
internal sealed class VsaKekUntersuchung
{
    public string Tid { get; init; } = "";
    public string Bezeichnung { get; set; } = "";
    public string Ausfuehrender { get; set; } = "";
    public string Zeitpunkt { get; set; } = "";
    public string InspizierteLaenge { get; set; } = "";
    public string Erfassungsart { get; set; } = "";
    public string Fahrzeug { get; set; } = "";
    public string Geraet { get; set; } = "";
    public string Witterung { get; set; } = "";
    public string Grund { get; set; } = "";
    public string VonPunkt { get; set; } = "";
    public string BisPunkt { get; set; } = "";
    /// <summary>Rohwert aus der XTF: "in_Fliessrichtung" / "gegen_Fliessrichtung".</summary>
    public string Fliessrichtung { get; set; } = "";
    public string Operateur { get; set; } = "";
    /// <summary>REF aus <c>AbwasserbauwerkRef</c>; leer, wenn nicht angegeben.</summary>
    public string AbwasserbauwerkRef { get; set; } = "";
    /// <summary>Die Kanalschaeden mit gueltigem Bezug; fuellt <c>VsaKekBeziehungen</c>.</summary>
    public List<VsaKekKanalschaden> Schaeden { get; } = new();
    /// <summary>Schachtschaeden derselben Untersuchung — bis 2026-09-05 nie gelesen.</summary>
    public List<VsaKekSchachtschaden> Schachtschaeden { get; } = new();
}

/// <summary>
/// Ein <c>Normschachtschaden</c>. Bewusst getrennt von <see cref="VsaKekKanalschaden"/>:
/// Er traegt einen eigenen Codebereich (D..) und einen Schachtbereich statt eines
/// Meterwerts entlang einer Haltung. Teil des Importbelegs, siehe <see cref="VsaKekUntersuchung"/>.
/// </summary>
internal sealed class VsaKekSchachtschaden
{
    public string Schadencode { get; set; } = "";
    public string Distanz { get; set; } = "";
    public string Anmerkung { get; set; } = "";
    public string Einzelschadenklasse { get; set; } = "";
    public string Schachtbereich { get; set; } = "";
    public string Videozaehlerstand { get; set; } = "";
    public string Quantifizierung1 { get; set; } = "";
    public string Quantifizierung2 { get; set; } = "";
}

/// <summary>
/// Die Rohwerte eines <c>Kanalschadens</c>. Teil des Importbelegs, siehe
/// <see cref="VsaKekUntersuchung"/>; darum stehen Kennung, Bezug und Videozaehlerstand
/// nicht hier, sondern in <see cref="VsaKekKanalschadenObjekt"/>.
/// </summary>
internal sealed class VsaKekKanalschaden
{
    public string ObjId { get; set; } = "";
    public string Schadencode { get; set; } = "";
    public string Distanz { get; set; } = "";
    public string Anmerkung { get; set; } = "";
    public string Einzelschadenklasse { get; set; } = "";
    public string Streckenschaden { get; set; } = "";
    public string Quantifizierung1 { get; set; } = "";
    public string Quantifizierung2 { get; set; } = "";
    public string SchadenlageAnfang { get; set; } = "";
    public string SchadenlageEnde { get; set; } = "";
    public double LL { get; set; }
}

/// <summary>
/// Ein <c>Kanalschaden</c> der Datei: Rohwerte, Befund in Programmform und der
/// unaufgeloeste Verweis auf seine Untersuchung.
/// </summary>
internal sealed record VsaKekKanalschadenObjekt(
    string? Tid,
    string? UntersuchungRef,
    VsaKekKanalschaden Werte,
    VsaFinding Befund);

/// <summary>
/// Ein <c>Normschachtschaden</c> mit dem unaufgeloesten Verweis auf seine Untersuchung. Die
/// Kennung steht hier und nicht in <see cref="VsaKekSchachtschaden"/> (Importbeleg).
/// </summary>
internal sealed record VsaKekNormschachtschadenObjekt(string? Tid, string? UntersuchungRef, VsaKekSchachtschaden Werte);

/// <summary>
/// Ein <c>Datei</c>-Objekt (Foto oder Video); <c>Objekt</c> ist die unaufgeloeste Kennung,
/// <c>Tid</c> die eigene Kennung (nur fuer den Importbericht).
/// </summary>
internal readonly record struct VsaKekDatei(string Art, string Klasse, string Objekt, string Bezeichnung, string Relativpfad, string Tid = "");

/// <summary>Alle VSA-KEK-Objekte einer Datei; Verweise bleiben hier Kennungen.</summary>
internal sealed class VsaKekBestand
{
    /// <summary>Modellname aus der HEADERSECTION; leer, wenn er fehlt.</summary>
    public string ModellName { get; init; } = "";

    /// <summary>Je TID, in der Reihenfolge des ersten Auftretens. Eine zweite gleiche TID ersetzt die erste.</summary>
    public Dictionary<string, VsaKekUntersuchung> Untersuchungen { get; } = new(StringComparer.Ordinal);

    /// <summary>In Dateireihenfolge, auch solche ohne gueltigen Bezug.</summary>
    public List<VsaKekKanalschadenObjekt> Kanalschaeden { get; } = new();
    public List<VsaKekNormschachtschadenObjekt> Normschachtschaeden { get; } = new();
    public List<VsaKekDatei> Dateien { get; } = new();

    /// <summary>Bauwerke, die die Datei selbst mitliefert: TID bzw. OBJ_ID -> Art.</summary>
    public Dictionary<string, VsaKekBauteilart> Bauwerksarten { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Schritt 1 des VSA-KEK-Imports: liest Untersuchung, Kanalschaden, Normschachtschaden,
/// Datei und die mitgelieferten Bauwerke mit ihren Rohwerten und Kennungen. Nichts wird
/// hier zugeordnet oder verworfen; das macht <c>VsaKekBeziehungen</c>.
/// </summary>
internal static class VsaKekObjektLeser
{
    public static VsaKekBestand Lies(XDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var bestand = new VsaKekBestand
        {
            ModellName = LiesModellName(doc),
            Bauwerksarten = LiesBauwerksarten(doc)
        };

        foreach (var node in doc.Descendants().Where(e => e.Name.LocalName.Contains("Untersuchung", StringComparison.OrdinalIgnoreCase)))
        {
            var untersuchung = LiesUntersuchung(node);
            if (untersuchung is not null)
                bestand.Untersuchungen[untersuchung.Tid] = untersuchung;
        }

        // Der Namensfilter trifft auch das Feld <KanalSchadencode>. Solche Felder ohne
        // Unterelemente haben keinen UntersuchungRef und fielen schon immer weg; sie
        // gehoeren nicht zu den Kanalschaeden der Datei.
        foreach (var node in doc.Descendants().Where(e => e.Name.LocalName.Contains("Kanalschaden", StringComparison.OrdinalIgnoreCase)
                                                          && e.HasElements))
            bestand.Kanalschaeden.Add(LiesKanalschaden(node));

        // Normschachtschaeden. Bis 2026-09-05 wurden sie ueberhaupt nicht gelesen: Die
        // 132 Schachtschaeden aus Andermatt Zone 2.11 verschwanden ersatzlos, und ihre
        // Begehungen landeten als Haltungen in der Haltungsliste.
        foreach (var node in doc.Descendants()
                     .Where(e => e.Name.LocalName.EndsWith(".Normschachtschaden", StringComparison.OrdinalIgnoreCase)
                                 || e.Name.LocalName.Equals("Normschachtschaden", StringComparison.OrdinalIgnoreCase)))
            bestand.Normschachtschaeden.Add(LiesNormschachtschaden(node));

        foreach (var node in doc.Descendants().Where(e => e.Name.LocalName.Contains("Datei", StringComparison.OrdinalIgnoreCase)))
            bestand.Dateien.Add(LiesDatei(node));

        return bestand;
    }

    /// <summary>
    /// Liest den Modellnamen aus der HEADERSECTION (erstes MODEL-Element). Fehlt er,
    /// bleibt die Angabe leer — sie ist eine Zusatzinformation und darf keinen Import stoppen.
    /// </summary>
    private static string LiesModellName(XDocument doc)
    {
        var model = doc.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "MODEL", StringComparison.OrdinalIgnoreCase));
        return (string?)model?.Attribute("NAME") ?? "";
    }

    /// <summary>Eine Untersuchung ohne TID laesst sich nicht verweisen und wird nicht gelesen.</summary>
    private static VsaKekUntersuchung? LiesUntersuchung(XElement node)
    {
        var tid = (string?)node.Attribute("TID");
        if (string.IsNullOrWhiteSpace(tid))
            return null;

        var u = new VsaKekUntersuchung { Tid = tid! };

        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Bezeichnung": u.Bezeichnung = child.Value; break;
                case "Ausfuehrender": u.Ausfuehrender = child.Value; break;
                case "Zeitpunkt": u.Zeitpunkt = child.Value; break;
                case "Inspizierte_Laenge": u.InspizierteLaenge = child.Value; break;
                case "Erfassungsart": u.Erfassungsart = child.Value; break;
                case "Fahrzeug": u.Fahrzeug = child.Value; break;
                case "Geraet": u.Geraet = child.Value; break;
                case "Witterung": u.Witterung = child.Value; break;
                case "Grund": u.Grund = child.Value; break;
                case "vonPunktBezeichnung": u.VonPunkt = child.Value; break;
                case "bisPunktBezeichnung": u.BisPunkt = child.Value; break;
                case "Fliessrichtung": u.Fliessrichtung = child.Value; break;
                case "Operateur": u.Operateur = child.Value; break;
                case "AbwasserbauwerkRef":
                    u.AbwasserbauwerkRef = (string?)child.Attribute("REF") ?? "";
                    break;
            }
        }

        return u;
    }

    /// <summary>
    /// Liest einen Kanalschaden in zwei Formen aus demselben Durchgang: die Rohwerte
    /// (fuer den Importbeleg und die Wahl der Haupt-Untersuchung) und den Befund, wie ihn
    /// das Programm fuehrt (Meter, Klassen, Uhrlagen als Zahl).
    /// </summary>
    private static VsaKekKanalschadenObjekt LiesKanalschaden(XElement node)
    {
        // UntersuchungRef/@REF
        var refNode = node.Elements().FirstOrDefault(e => e.Name.LocalName == "UntersuchungRef");
        var refTid = (string?)refNode?.Attribute("REF");

        var schadenTid = (string?)node.Attribute("TID");
        var s = new VsaKekKanalschaden();
        var finding = new VsaFinding
        {
            // Herkunft festhalten, solange sie bekannt ist: Nur damit laesst sich
            // spaeter genau dieses Element in der Originaldatei wiederfinden.
            KanalschadenTid = string.IsNullOrWhiteSpace(schadenTid) ? null : schadenTid,
            UntersuchungTid = refTid
        };
        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "OBJ_ID":
                    s.ObjId = child.Value;
                    break;
                case "KanalSchadencode":
                    s.Schadencode = child.Value;
                    finding.KanalSchadencode = child.Value;
                    break;
                case "Distanz":
                    s.Distanz = child.Value;
                    if (XtfValueNormalizer.TryParseDouble(child.Value, out var meter))
                        finding.MeterStart = meter;
                    break;
                case "Videozaehlerstand":
                    // Sekunde ab Dateianfang (SN EN 13508-2, Kapitel 3.1.10).
                    // Wurde bis 2026-08-13 nie eingelesen, obwohl die ganze
                    // Weiterverarbeitung dahinter steht: finding.MPEG ->
                    // entry.Mpeg/entry.Zeit -> CodingBoundaryImportReferencePolicy.
                    // Ohne diesen Wert fiel die Videoreferenz von Rohranfang und
                    // Rohrende dort still auf TimeSpan.Zero zurueck.
                    finding.MPEG = child.Value;
                    break;
                case "Anmerkung":
                    s.Anmerkung = child.Value;
                    finding.Raw = child.Value;
                    break;
                case "Einzelschadenklasse":
                    s.Einzelschadenklasse = child.Value;
                    if (int.TryParse(child.Value, out var ez))
                    {
                        // Best-effort: wenn keine Regel vorhanden, nutze Einzelschadenklasse für alle Anforderungen
                        if (ez < 0) ez = 0;
                        if (ez > 4) ez = 4;
                        finding.EZD = ez;
                        finding.EZS = ez;
                        finding.EZB = ez;
                    }
                    break;
                case "Streckenschaden":
                    s.Streckenschaden = child.Value;
                    break;
                case "Quantifizierung1":
                    s.Quantifizierung1 = child.Value;
                    finding.Quantifizierung1 = child.Value;
                    break;
                case "Quantifizierung2":
                    s.Quantifizierung2 = child.Value;
                    finding.Quantifizierung2 = child.Value;
                    break;
                case "SchadenlageAnfang":
                    s.SchadenlageAnfang = child.Value;
                    if (double.TryParse(child.Value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var anfang))
                        finding.SchadenlageAnfang = anfang;
                    break;
                case "SchadenlageEnde":
                    s.SchadenlageEnde = child.Value;
                    if (double.TryParse(child.Value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var ende))
                        finding.SchadenlageEnde = ende;
                    break;
            }
        }

        // SchadenlageAnfang/-Ende sind Uhrlagen im Rohrquerschnitt und keine
        // Laengenpositionen. Eine Streckenlaenge darf daraus nie entstehen.
        double ll = 0.0;
        if (string.Equals(s.Streckenschaden, "true", StringComparison.OrdinalIgnoreCase)
            && XtfValueNormalizer.TryParseDouble(s.Quantifizierung1, out var q1))
        {
            ll = q1;
        }
        s.LL = ll;
        finding.LL = ll;

        return new VsaKekKanalschadenObjekt(schadenTid, refTid, s, finding);
    }

    private static VsaKekNormschachtschadenObjekt LiesNormschachtschaden(XElement node)
    {
        var refNode = node.Elements().FirstOrDefault(e => e.Name.LocalName == "UntersuchungRef");
        var refTid = (string?)refNode?.Attribute("REF");

        var schachtschaden = new VsaKekSchachtschaden();
        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                // Beide Schreibweisen: Der WinCan-VX-Export schreibt
                // "SchachtSchadencode", neuere Modellfassungen "NormschachtSchadencode".
                case "SchachtSchadencode":
                case "NormschachtSchadencode":
                    schachtschaden.Schadencode = child.Value;
                    break;
                case "Distanz": schachtschaden.Distanz = child.Value; break;
                case "Anmerkung": schachtschaden.Anmerkung = child.Value; break;
                case "Einzelschadenklasse": schachtschaden.Einzelschadenklasse = child.Value; break;
                case "Schachtbereich": schachtschaden.Schachtbereich = child.Value; break;
                case "Videozaehlerstand": schachtschaden.Videozaehlerstand = child.Value; break;
                case "Quantifizierung1": schachtschaden.Quantifizierung1 = child.Value; break;
                case "Quantifizierung2": schachtschaden.Quantifizierung2 = child.Value; break;
            }
        }

        return new VsaKekNormschachtschadenObjekt((string?)node.Attribute("TID"), refTid, schachtschaden);
    }

    private static VsaKekDatei LiesDatei(XElement node)
    {
        string art = "";
        string klasse = "";
        string objekt = "";
        string bezeichnung = "";
        string relativpfad = "";

        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Art": art = child.Value; break;
                case "Klasse": klasse = child.Value; break;
                case "Objekt": objekt = child.Value; break;
                case "Bezeichnung": bezeichnung = child.Value; break;
                case "Relativpfad": relativpfad = child.Value; break;
            }
        }

        return new VsaKekDatei(art, klasse, objekt, bezeichnung, relativpfad, (string?)node.Attribute("TID") ?? "");
    }

    /// <summary>
    /// Welche Bauwerke die Datei selbst mitliefert, nach TID und OBJ_ID nachschlagbar.
    ///
    /// WinCan-Exporte enthalten diese Objekte meist NICHT — dann bleibt die Karte leer
    /// und die Einordnung stuetzt sich auf Schadensart und Untersuchungsform. Liegt das
    /// Objekt aber vor, ist es der staerkste Beleg.
    /// </summary>
    private static Dictionary<string, VsaKekBauteilart> LiesBauwerksarten(XDocument doc)
    {
        var karte = new Dictionary<string, VsaKekBauteilart>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in doc.Descendants())
        {
            var lokal = node.Name.LocalName;
            VsaKekBauteilart art;
            if (EndetAuf(lokal, "Normschacht"))
                art = VsaKekBauteilart.Schacht;
            else if (EndetAuf(lokal, "Kanal") || EndetAuf(lokal, "Haltung"))
                art = VsaKekBauteilart.Haltung;
            else
                continue;

            var tid = (string?)node.Attribute("TID");
            if (!string.IsNullOrWhiteSpace(tid))
                karte[tid!] = art;

            var objId = node.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals("OBJ_ID", StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrWhiteSpace(objId))
                karte[objId!] = art;
        }

        return karte;

        static bool EndetAuf(string elementname, string klasse)
            => elementname.Equals(klasse, StringComparison.OrdinalIgnoreCase)
               || elementname.EndsWith("." + klasse, StringComparison.OrdinalIgnoreCase);
    }
}
