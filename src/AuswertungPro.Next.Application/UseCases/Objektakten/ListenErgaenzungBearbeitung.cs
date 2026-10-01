using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Eine Zeile im Fenster «Liste bearbeiten»: ein WebGIS-Eintrag mit seinem Zustand oder
/// ein eigener Eintrag. Reine Daten fuer die Anzeige; geschrieben wird ueber die Bearbeitung.</summary>
public sealed record ListenErgaenzungZeile(string? Code, string Originaltext, string Text, bool Ausgeblendet, bool Eigen)
{
    public bool Umbenannt => !Eigen && Text != Originaltext;
}

/// <summary>Bearbeitet die Ergaenzungen EINER Liste (Katalog und ggf. Elterngruppe), WPF-frei.
///
/// Der Katalog bleibt unangetastet: Umbenennen aendert nur den Anzeigetext, der WebGIS-Code
/// bleibt; Ausblenden entfernt den Eintrag aus der Liste, gespeicherte Werte bleiben lesbar;
/// eigene Eintraege tragen optional einen Code und gehen ohne Code nie in eine XTF.
/// Gespeichert wird erst mit <see cref="Speichere"/>; Ergaenzungen anderer Listen bleiben erhalten.
/// </summary>
public sealed class ListenErgaenzungBearbeitung
{
    private readonly IObjektaktenListenErgaenzungen _speicher;
    private readonly IReadOnlyList<ObjektAuswahl> _katalog;
    private readonly List<ListenErgaenzungZeile> _zeilen;

    public ListenErgaenzungBearbeitung(IObjektaktenListenErgaenzungen speicher, string titel, string katalogId,
        string? eltern, IEnumerable<ObjektAuswahl> katalogEintraege)
    {
        _speicher = speicher;
        Titel = titel;
        KatalogId = katalogId;
        Eltern = string.IsNullOrEmpty(eltern) ? null : eltern;
        _katalog = katalogEintraege.Where(e => !e.Eigen).ToArray();
        var vorhanden = speicher.Lade();
        var eigene = vorhanden.Where(Passt).Where(e => e.Art == ListenErgaenzungArt.Hinzugefuegt).ToArray();
        var angewendet = ListenErgaenzungRegel.Anwenden(_katalog, KatalogId, Eltern, vorhanden);
        var ausgeblendet = vorhanden.Where(Passt).Where(e => e.Art == ListenErgaenzungArt.Ausgeblendet).Select(e => e.Code).ToHashSet();
        _zeilen = _katalog.Select(k => new ListenErgaenzungZeile(
                k.OriginalCode, k.Label,
                angewendet.FirstOrDefault(a => !a.Eigen && a.OriginalCode == k.OriginalCode)?.Label ?? k.Label,
                k.OriginalCode is not null && ausgeblendet.Contains(k.OriginalCode), Eigen: false))
            .Concat(eigene.Select(e => new ListenErgaenzungZeile(
                string.IsNullOrWhiteSpace(e.Code) ? null : e.Code.Trim(), e.Text!.Trim(), e.Text!.Trim(), false, Eigen: true)))
            .ToList();
    }

    public string Titel { get; }
    public string KatalogId { get; }
    public string? Eltern { get; }
    public IReadOnlyList<ListenErgaenzungZeile> Zeilen => _zeilen;
    public bool Geaendert { get; private set; }

    /// <summary>Anzeigetext eines WebGIS-Eintrags aendern; der Originaltext stellt ihn zurueck.</summary>
    public void Umbenennen(string code, string neuerText)
    {
        var text = (neuerText ?? "").Trim();
        if (text.Length == 0) throw new ArgumentException("Ein Eintrag braucht einen Text.");
        Ersetze(z => !z.Eigen && z.Code == code, z => z with { Text = text });
    }

    public void Ausblenden(string code, bool ausblenden)
        => Ersetze(z => !z.Eigen && z.Code == code, z => z with { Ausgeblendet = ausblenden });

    /// <summary>Eigenen Eintrag anlegen. Ein Text, der schon in der Liste steht, wird abgewiesen -
    /// zwei gleiche Eintraege waeren spaeter nicht mehr zu unterscheiden.</summary>
    public ListenErgaenzungZeile Hinzufuegen(string text, string? code)
    {
        var t = (text ?? "").Trim();
        var c = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        if (t.Length == 0) throw new ArgumentException("Ein eigener Eintrag braucht einen Text.");
        if (_zeilen.Any(z => string.Equals(z.Text, t, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"«{t}» steht bereits in dieser Liste.");
        if (c is not null && _zeilen.Any(z => z.Code == c))
            throw new ArgumentException($"Der Code «{c}» ist in dieser Liste schon vergeben.");
        var zeile = new ListenErgaenzungZeile(c, t, t, false, Eigen: true);
        _zeilen.Add(zeile);
        Geaendert = true;
        return zeile;
    }

    /// <summary>Eigenen Eintrag entfernen. WebGIS-Eintraege werden nicht entfernt, nur ausgeblendet.</summary>
    public void Entfernen(ListenErgaenzungZeile zeile)
    {
        if (!zeile.Eigen) throw new InvalidOperationException("Ein WebGIS-Eintrag wird ausgeblendet, nicht entfernt.");
        if (_zeilen.Remove(zeile)) Geaendert = true;
    }

    /// <summary>Schreibt die Ergaenzungen dieser Liste; alle anderen Listen bleiben, wie sie waren.</summary>
    public void Speichere()
    {
        var andere = _speicher.Lade().Where(e => !Passt(e));
        var eigene = _zeilen.Where(z => !z.Eigen).SelectMany(z =>
        {
            var liste = new List<ListenErgaenzung>();
            if (z.Umbenannt) liste.Add(new ListenErgaenzung(KatalogId, Eltern, ListenErgaenzungArt.Umbenannt, z.Code, z.Text));
            if (z.Ausgeblendet) liste.Add(new ListenErgaenzung(KatalogId, Eltern, ListenErgaenzungArt.Ausgeblendet, z.Code, null));
            return liste;
        }).Concat(_zeilen.Where(z => z.Eigen)
            .Select(z => new ListenErgaenzung(KatalogId, Eltern, ListenErgaenzungArt.Hinzugefuegt, z.Code, z.Text)));
        _speicher.Speichere(andere.Concat(eigene));
        Geaendert = false;
    }

    private bool Passt(ListenErgaenzung e) => e.KatalogId == KatalogId && (e.Eltern ?? "") == (Eltern ?? "");

    private void Ersetze(Func<ListenErgaenzungZeile, bool> treffer, Func<ListenErgaenzungZeile, ListenErgaenzungZeile> neu)
    {
        var index = _zeilen.FindIndex(z => treffer(z));
        if (index < 0) throw new ArgumentException("Dieser Eintrag steht nicht in der Liste.");
        var ersetzt = neu(_zeilen[index]);
        if (ersetzt == _zeilen[index]) return;
        _zeilen[index] = ersetzt;
        Geaendert = true;
    }
}
