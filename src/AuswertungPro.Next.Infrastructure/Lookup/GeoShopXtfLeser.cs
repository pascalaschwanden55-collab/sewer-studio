using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Xtf.Dss;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Liest grosse INTERLIS-2.3-Lieferungen in begrenzten Durchlaeufen. Im Speicher
/// bleiben nur die angefragten Bauteile und ihr Objektverbund samt Originalgeometrien.
/// Derselbe lesend geoeffnete Dateistrom bleibt bis zum Ende gesperrt gegen Schreiben.
/// </summary>
public sealed class GeoShopXtfLeser : IGeoShopLeser
{
    public IReadOnlyDictionary<string, string> LiesEigentuemer(string datei) => GeoShopEigentuemerDatei.Lies(datei);
    private const string Ns = "http://www.interlis.ch/INTERLIS2.3";
    private static readonly HashSet<string> Modelle = new(StringComparer.Ordinal)
    {
        "DSS_2020_1_LV95", "SIA405_ABWASSER_2020_LV95", "SIA405_ABWASSER_2020_1_LV95",
        "SIA405_Base_Abwasser_1_LV95", "SIA405_Base_Abwasser_LV95"
    };

    public GeoShopBestand Lies(string datei, BauteilArt art, IReadOnlyCollection<string> namen,
        CancellationToken cancellationToken = default)
    {
        var gesucht = new HashSet<string>(namen.Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
        if (art == BauteilArt.Haltung)
            foreach (var name in gesucht.ToArray())
            {
                var teile = name.Split('-');
                if (teile.Length == 2) gesucht.Add($"{teile[1].Trim()}-{teile[0].Trim()}");
            }

        using var stream = new FileStream(datei, FileMode.Open, FileAccess.Read, FileShare.Read);
        var objekte = new Dictionary<string, GeoShopXtfObjekt>(StringComparer.Ordinal);
        var primaer = new List<GeoShopXtfObjekt>();
        var mehrfach = new HashSet<string>(StringComparer.Ordinal);
        var klasse = art == BauteilArt.Haltung ? "Haltung" : "Abwasserknoten";
        LiesDurchlauf(stream, (tid, k) => k == klasse, o =>
        {
            if (!gesucht.Contains(o.Wert("Bezeichnung"))) return;
            primaer.Add(o);
            if (!objekte.TryAdd(o.Tid, o)) mehrfach.Add(o.Tid);
        }, cancellationToken, mehrfach);

        // Haltung -> Kanal/Punkte/Profil -> Knoten/Organisation -> Schachtbauwerk.
        for (var tiefe = 0; tiefe < 4; tiefe++)
        {
            var fehlt = objekte.Values.SelectMany(o => o.Refs.Values)
                .Where(id => !objekte.ContainsKey(id)).ToHashSet(StringComparer.Ordinal);
            if (fehlt.Count == 0) break;
            var gefunden = new HashSet<string>(StringComparer.Ordinal);
            LiesDurchlauf(stream, (tid, _) => fehlt.Contains(tid), o =>
            {
                if (!gefunden.Add(o.Tid)) mehrfach.Add(o.Tid);
                objekte.TryAdd(o.Tid, o);
            }, cancellationToken);
            if (gefunden.Count == 0) break;
        }

        // Rueckwaertige Beziehungen sind nicht am Bauwerk gespeichert: Deckel,
        // Einstiegshilfe und Ereignis-Assoziation zeigen AUF das Bauwerk.
        var bauwerke = primaer.Select(o => o.Ref("AbwasserbauwerkRef")).ToHashSet(StringComparer.Ordinal);
        var knoten = art == BauteilArt.Schacht ? primaer.Select(o => o.Tid).ToHashSet(StringComparer.Ordinal) : new HashSet<string>();
        var einbauEltern = objekte.Values.Where(o => o.Klasse == "Abwasserknoten").Select(o => o.Tid)
            .Concat(objekte.Values.Where(o => o.Klasse is "Abwasserknoten" or "Haltung").Select(o => o.Ref("AbwasserbauwerkRef")))
            .Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);
        LiesDurchlauf(stream, (_, k) => DssEinbautenZuordnung.Art(k) is not null
            || k is "Deckel" or "Haltungspunkt" or "Erhaltungsereignis_AbwasserbauwerkAssoc", o =>
        {
            if (DssEinbautenZuordnung.Elternrolle(o.Klasse) is { } rolle ? einbauEltern.Contains(o.Ref(rolle))
                : bauwerke.Contains(o.Ref("AbwasserbauwerkRef")) || knoten.Contains(o.Ref("AbwassernetzelementRef")))
                objekte.TryAdd(o.Tid, o);
        }, cancellationToken);
        // Ereignisse und danach deren Firmenassoziationen/Organisationsverweise.
        for (var tiefe = 0; tiefe < 3; tiefe++)
        {
            var fehlt = objekte.Values.SelectMany(o => o.Refs.Values).Where(id => !objekte.ContainsKey(id)).ToHashSet(StringComparer.Ordinal);
            var ereignisse = objekte.Values.Where(o => o.Klasse is "Unterhalt" or "Erhaltungsereignis").Select(o => o.Tid).ToHashSet();
            var anschluesse = objekte.Values.Where(o => o.Klasse == "Haltungspunkt" && knoten.Contains(o.Ref("AbwassernetzelementRef")))
                .Select(o => o.Tid).ToHashSet();
            LiesDurchlauf(stream, (tid, k) => fehlt.Contains(tid) || k == "Erhaltungsereignis_Ausfuehrende_FirmaAssoc"
                || art == BauteilArt.Schacht && k == "Haltung", o =>
            {
                if (fehlt.Contains(o.Tid) || o.Refs.Values.Any(ereignisse.Contains)
                    || o.Klasse == "Haltung" && o.Refs.Values.Any(anschluesse.Contains)) objekte.TryAdd(o.Tid, o);
            }, cancellationToken);
        }
        var inverse = objekte.Values.SelectMany(o => o.Refs.Values.Select(r => (Ref: r, Objekt: o))).ToLookup(x => x.Ref, x => x.Objekt);
        return GeoShopEigentuemerDatei.ErgaenzeBegleitdatei(new GeoShopBestand(art, Path.GetFullPath(datei), primaer.Select(o =>
        {
            var bauteil = GeoShopXtfZuordnung.Baue(o, art, objekte, mehrfach);
            var ids = new HashSet<string> { o.Tid };
            var queue = new Queue<string>(); queue.Enqueue(o.Tid);
            while (queue.TryDequeue(out var tid))
            {
                if (!objekte.TryGetValue(tid, out var current)) continue;
                var kinder = tid == o.Ref("AbwasserbauwerkRef") || art == BauteilArt.Schacht && tid == o.Tid
                    || current.Klasse is "Unterhalt" or "Erhaltungsereignis"
                    || current.Klasse == "Haltungspunkt" && knoten.Contains(current.Ref("AbwassernetzelementRef"))
                    ? inverse[tid].Select(x => x.Tid)
                    : inverse[tid].Where(x => DssEinbautenZuordnung.Elternrolle(x.Klasse) is { } rolle && x.Ref(rolle) == tid).Select(x => x.Tid);
                foreach (var id in current.Refs.Values.Concat(kinder)) if (ids.Add(id)) queue.Enqueue(id);
            }
            var quellen = ids.Where(id => objekte.ContainsKey(id) && !mehrfach.Contains(id)).OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => objekte[id]).Select(x => new AuswertungPro.Next.Domain.Models.ObjektQuellbeleg
                {
                    System = "GeoShop-XTF", Datei = Path.GetFullPath(datei), Modell = x.Modell, Klasse = x.Klasse,
                    Kennung = x.Tid, IstLokaleKennung = x.Tid.StartsWith("lokal:", StringComparison.Ordinal), Werte = new(x.Werte), Referenzen = new(x.Refs), Strukturen = new(x.Strukturen)
                }).ToArray();
            return AuswertungPro.Next.Application.UseCases.Objektakten.GeoShopAttributZuordnung.ErgaenzeBestandsfelder(
                bauteil with { Quellen = quellen, Hinweis = ids.Any(mehrfach.Contains)
                    ? (bauteil.Hinweis + " Doppelte Quellkennungen im Verbund werden nicht in die Akte übernommen.").Trim() : bauteil.Hinweis }, art);
        }).ToArray()));
    }

    internal static void LiesDurchlauf(Stream stream, Func<string, string, bool> benoetigt,
        Action<GeoShopXtfObjekt> nimm, CancellationToken cancellationToken, HashSet<string>? doppelteTids = null)
    {
        stream.Position = 0;
        using var xml = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            IgnoreComments = true, IgnoreWhitespace = true, CloseInput = false
        });
        xml.MoveToContent();
        if (xml.LocalName != "TRANSFER" || xml.NamespaceURI != Ns)
            throw new InvalidDataException("Bitte eine INTERLIS-2.3-XTF aus GeoShop auswählen.");
        var modellGefunden = false;
        var alleTids = doppelteTids is null ? null : new HashSet<string>(StringComparer.Ordinal);
        while (!xml.EOF)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (xml.NodeType == XmlNodeType.Element && xml.Depth == 3 && xml.NamespaceURI == Ns)
            {
                var tid = xml.GetAttribute("TID") ?? "";
                var teile = xml.LocalName.Split('.');
                if (teile.Length != 3 || !Modelle.Contains(teile[0])) { xml.Skip(); continue; }
                modellGefunden = true;
                if (tid.Length == 0 && !teile[2].EndsWith("Assoc", StringComparison.Ordinal)) { xml.Skip(); continue; }
                if (tid.Length > 0 && alleTids is not null && !alleTids.Add(tid)) doppelteTids!.Add(tid);
                if (!benoetigt(tid, teile[2])) { xml.Skip(); continue; }
                var element = (XElement)XNode.ReadFrom(xml);
                var werte = new Dictionary<string, string>(StringComparer.Ordinal);
                var refs = new Dictionary<string, string>(StringComparer.Ordinal);
                var strukturen = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var feld in element.Elements())
                {
                    if (feld.Attribute("REF") is { } referenz) refs[feld.Name.LocalName] = referenz.Value;
                    else if (!feld.HasElements) werte[feld.Name.LocalName] = feld.Value;
                    else strukturen[feld.Name.LocalName] = feld.ToString(SaveOptions.DisableFormatting);
                }
                // Eine Assoziation hat im Original keine TID. Ihre lokale Beleg-ID
                // wird niemals als externe Objektkennung ausgegeben.
                if (tid.Length == 0) tid = "lokal:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(element.Name + "|" + string.Join("|", refs.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)))));
                nimm(new GeoShopXtfObjekt(tid, teile[2], werte, refs, strukturen, teile[0]));
            }
            else xml.Read();
        }
        if (!modellGefunden)
            throw new InvalidDataException("Keine unterstützten SIA405-/DSS-2020-Objekte in der XTF gefunden.");
    }
}

internal sealed record GeoShopXtfObjekt(string Tid, string Klasse,
    IReadOnlyDictionary<string, string> Werte, IReadOnlyDictionary<string, string> Refs, IReadOnlyDictionary<string, string> Strukturen, string Modell = "")
{
    public string Wert(string feld) => Werte.TryGetValue(feld, out var wert) ? wert : "";
    public string Ref(string feld) => Refs.TryGetValue(feld, out var wert) ? wert : "";
}
