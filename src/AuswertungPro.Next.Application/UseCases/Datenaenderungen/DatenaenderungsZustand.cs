using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Datenaenderungen;

/// <summary>
/// Gemeinsamer Zugriff auf Haltung und Schacht fuer den Verlauf. Beide fuehren <c>Fields</c> und
/// <c>FieldMeta</c> gleich; verschieden sind Name, Beschriftung, die Umbenennungsfelder und der
/// Wiederherstellungsweg.
/// </summary>
internal sealed class DatensatzZugriff
{
    private DatensatzZugriff(object datensatz, DatenaenderungsBereich bereich,
        Dictionary<string, string> fields, Dictionary<string, FieldMetadata> meta,
        Action<string, string?, FieldMetadata?> stelle, Func<string> name,
        Func<string, string> label, Func<string, bool> mitDateifolgen, Func<Project, bool> imProjekt)
    {
        Datensatz = datensatz;
        Bereich = bereich;
        Fields = fields;
        Meta = meta;
        Stelle = stelle;
        Name = name;
        Label = label;
        MitDateifolgen = mitDateifolgen;
        ImProjekt = imProjekt;
    }

    public object Datensatz { get; }
    public DatenaenderungsBereich Bereich { get; }
    public Dictionary<string, string> Fields { get; }
    public Dictionary<string, FieldMetadata> Meta { get; }
    public Action<string, string?, FieldMetadata?> Stelle { get; }
    public Func<string> Name { get; }
    public Func<string, string> Label { get; }
    public Func<string, bool> MitDateifolgen { get; }
    public Func<Project, bool> ImProjekt { get; }

    /// <summary>Felder, deren Aenderung Ordner und Dateien umbenennt — nie rueckgaengig machbar.</summary>
    internal static readonly string[] HaltungUmbenennungsfelder = [FieldKeys.HoldingName, "Schacht_oben", "Schacht_unten"];

    public static DatensatzZugriff Fuer(HaltungRecord h) => new(h, DatenaenderungsBereich.Haltungen,
        h.Fields, h.FieldMeta, h.StelleFeldzustandWiederHer,
        () => h.GetFieldValue(FieldKeys.HoldingName),
        feld => FieldCatalog.Get(feld).Label,
        feld => HaltungUmbenennungsfelder.Contains(feld, StringComparer.Ordinal),
        p => p.Data.Contains(h));

    public static DatensatzZugriff Fuer(SchachtRecord s) => new(s, DatenaenderungsBereich.Schaechte,
        s.Fields, s.FieldMeta, s.StelleFeldzustandWiederHer,
        () => s.GetFieldValue(SchachtFeldnamen.Feld(s, "Schachtnummer")),
        SchachtLabel,
        feld => string.Equals(SchachtFeldnamen.Falte(feld), SchachtFeldnamen.Falte("Schachtnummer"), StringComparison.Ordinal),
        p => p.SchaechteData.Contains(s));

    // Schachtfelder heissen nach der Kopfzeile der Excel-Vorlage; ein Umbruch darin wird zum Leerzeichen.
    private static string SchachtLabel(string feld)
        => FieldCatalog.Definitions.TryGetValue(feld, out var def)
            ? def.Label
            : string.Join(' ', feld.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>Ein Feld vorher/nachher; <c>null</c> heisst: Feld bzw. Metadaten gab es nicht.</summary>
internal sealed record FeldAenderung(string Feld, string? WertVorher, FieldMetadata? MetaVorher,
    string? WertNachher, FieldMetadata? MetaNachher)
{
    public bool WertGeaendert => !string.Equals(WertVorher, WertNachher, StringComparison.Ordinal);
}

internal sealed record DatensatzAenderung(DatensatzZugriff Zugriff, IReadOnlyList<FeldAenderung> Felder);

/// <summary>Aktenwerte eines Objektakten-Eintrags vorher/nachher; die Akte kann neu angelegt worden sein.</summary>
internal sealed record AkteAenderung(ObjektAkte Akte, bool VorherVorhanden, bool NachherVorhanden,
    IReadOnlyList<(string Feld, ObjektFeldWert? Vorher, ObjektFeldWert? Nachher)> Werte);

internal sealed record VerlaufEintrag(Project Projekt, DatenaenderungsBereich Bereich, string Beschreibung,
    IReadOnlyList<DatensatzAenderung> Datensaetze, IReadOnlyList<AkteAenderung> Akten);

/// <summary>Merkt sich den Zustand eines Datensatzes beim Beginn einer Eingabe.</summary>
internal sealed class DatensatzSchnappschuss
{
    private readonly Dictionary<string, string> _fields;
    private readonly Dictionary<string, FieldMetadata> _meta;

    public DatensatzSchnappschuss(DatensatzZugriff zugriff)
    {
        Zugriff = zugriff;
        _fields = new Dictionary<string, string>(zugriff.Fields, StringComparer.Ordinal);
        _meta = zugriff.Meta.ToDictionary(p => p.Key, p => FieldMetadataKopie.Von(p.Value), StringComparer.Ordinal);
    }

    public DatensatzZugriff Zugriff { get; }

    /// <summary>Alle Felder, deren Wert oder Herkunftsdaten sich seit dem Schnappschuss geaendert haben.</summary>
    public List<FeldAenderung> Unterschiede()
    {
        var felder = new SortedSet<string>(_fields.Keys, StringComparer.Ordinal);
        felder.UnionWith(Zugriff.Fields.Keys);
        felder.UnionWith(_meta.Keys);
        felder.UnionWith(Zugriff.Meta.Keys);
        var liste = new List<FeldAenderung>();
        foreach (var feld in felder)
        {
            var wertVorher = _fields.TryGetValue(feld, out var v) ? v : null;
            var wertNachher = Zugriff.Fields.TryGetValue(feld, out var n) ? n : null;
            var metaVorher = _meta.GetValueOrDefault(feld);
            var metaNachher = Zugriff.Meta.GetValueOrDefault(feld);
            if (string.Equals(wertVorher, wertNachher, StringComparison.Ordinal)
                && FieldMetadataKopie.Gleich(metaVorher, metaNachher, mitZeitstempel: true))
                continue;
            liste.Add(new FeldAenderung(feld, wertVorher, metaVorher, wertNachher,
                metaNachher is null ? null : FieldMetadataKopie.Von(metaNachher)));
        }
        return liste;
    }
}

internal static class AktenWertKopie
{
    public static ObjektFeldWert Von(ObjektFeldWert w) => new()
    {
        Text = w.Text, KatalogId = w.KatalogId, Originalcode = w.Originalcode, LokalerEintrag = w.LokalerEintrag,
        Bestandswert = w.Bestandswert, VonHand = w.VonHand, GeaendertUtc = w.GeaendertUtc,
        Zusatzdaten = w.Zusatzdaten is null ? null : new(w.Zusatzdaten),
    };

    public static Dictionary<string, ObjektFeldWert> Von(Dictionary<string, ObjektFeldWert> werte)
        => werte.ToDictionary(p => p.Key, p => Von(p.Value), StringComparer.Ordinal);

    public static bool Gleich(ObjektFeldWert? a, ObjektFeldWert? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        return a.Text == b.Text && a.KatalogId == b.KatalogId && a.Originalcode == b.Originalcode
               && a.LokalerEintrag == b.LokalerEintrag && a.Bestandswert == b.Bestandswert
               && a.VonHand == b.VonHand && a.GeaendertUtc == b.GeaendertUtc
               && (a.Zusatzdaten?.Count ?? 0) == (b.Zusatzdaten?.Count ?? 0);
    }
}
