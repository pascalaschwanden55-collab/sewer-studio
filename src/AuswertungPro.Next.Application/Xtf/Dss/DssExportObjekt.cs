using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

internal sealed class DssExportObjekt(string klasse, string tid, bool ohneTid = false)
{
    public string Klasse { get; } = klasse;
    public string Tid { get; } = tid;
    public bool OhneTid { get; } = ohneTid;
    public Dictionary<string, string> Werte { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Refs { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Strukturen { get; } = new(StringComparer.Ordinal);
    public XtfNeuGeometrie? Geometrie { get; set; }
    public void Setze(string attribut, string text)
    {
        var wert = DssExportSchema.Normalisiere(Klasse, attribut, text);
        if (Werte.GetValueOrDefault(attribut) == wert) return;
        if (wert is null) Werte.Remove(attribut); else Werte[attribut] = wert;
        Werte["Letzte_Aenderung"] = DateTime.Today.ToString("yyyyMMdd");
    }
    public XtfNeuObjekt Fertig() => new(Klasse, Tid, Werte.ToArray(), Refs.Select(p => new XtfNeuVerweis(p.Key, p.Value)).ToArray(),
        Geometrie, Klasse == "Organisation", Strukturen: new Dictionary<string, string>(Strukturen), OhneTid: OhneTid);
    public static DssExportObjekt Aus(ObjektQuellbeleg q)
    {
        var o = new DssExportObjekt(q.Klasse, q.Kennung, q.IstLokaleKennung);
        foreach (var (k, v) in q.Werte) o.Werte.Add(k, v);
        foreach (var (k, v) in q.Referenzen) o.Refs.Add(k, v);
        foreach (var (k, v) in q.Strukturen) o.Strukturen.Add(k, v);
        return o;
    }
    public static DssExportObjekt Aus(XtfNeuObjekt q)
    {
        var o = new DssExportObjekt(q.Klasse, q.Tid) { Geometrie = q.Geometrie };
        foreach (var (k, v) in q.Felder) o.Werte.Add(k, v);
        foreach (var v in q.Verweise) o.Refs.Add(v.Name, v.ZielTid);
        o.Werte.TryAdd("Letzte_Aenderung", DateTime.Today.ToString("yyyyMMdd"));
        return o;
    }
}
