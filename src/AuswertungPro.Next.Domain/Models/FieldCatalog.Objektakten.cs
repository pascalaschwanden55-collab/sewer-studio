using System.Text.Json;

namespace AuswertungPro.Next.Domain.Models;

public static partial class FieldCatalog
{
    private static readonly Lazy<ObjektFeldKatalog> AktenKatalog = new(() =>
    {
        using var stream = typeof(FieldCatalog).Assembly.GetManifestResourceStream("Objektakten.Katalog.json")
            ?? throw new InvalidOperationException("Der Objektaktenkatalog fehlt.");
        var katalog = JsonSerializer.Deserialize<ObjektFeldKatalog>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Der Objektaktenkatalog ist leer.");
        katalog.Pruefe();
        return katalog;
    });

    /// <summary>Ergaenzt den bestehenden Katalog, ohne Tabellen-/Exportspalten umzubenennen.</summary>
    public static ObjektFeldKatalog Objektfelder => AktenKatalog.Value;
}

public sealed class ObjektFeldKatalog
{
    public int Version { get; init; }
    public string Stand { get; init; } = "";
    public IReadOnlyList<ObjektFeldDefinition> Felder { get; init; } = [];
    public IReadOnlyList<ObjektAuswahlKatalog> Kataloge { get; init; } = [];
    public IReadOnlyList<ObjektUnterliste> Unterlisten { get; init; } = [];
    public ObjektFeldDefinition Feld(string id) => Felder.Single(f => f.Id == id);
    public ObjektAuswahlKatalog? Auswahl(string? id) => Kataloge.SingleOrDefault(k => k.Id == id);
    public void Pruefe()
    {
        if (Version != 1 || Felder.Count == 0 || Felder.Select(f => f.Id).Distinct().Count() != Felder.Count
            || Kataloge.Select(k => k.Id).Distinct().Count() != Kataloge.Count)
            throw new InvalidOperationException("Ungültige Version oder doppelte Kennungen im Objektaktenkatalog.");
        foreach (var feld in Felder)
        {
            if (string.IsNullOrWhiteSpace(feld.Label) || feld.KatalogId is not null && Auswahl(feld.KatalogId) is null)
                throw new InvalidOperationException($"Unvollständige Felddefinition: {feld.Id}");
            if (feld.KatalogIdJeEltern is null) continue;
            // Ein Katalog je Elternwert ist nur mit bekanntem Elternfeld sinnvoll; ohne es
            // koennte die Anzeige nicht filtern und zeigte alle Gruppen durcheinander.
            var voll = Auswahl(feld.KatalogIdJeEltern);
            if (voll is null || feld.Elternfeld is null || Felder.All(f => f.Id != feld.Elternfeld)
                || voll.Eintraege.Any(e => string.IsNullOrEmpty(e.Eltern)))
                throw new InvalidOperationException($"Katalog je Elternwert unvollständig: {feld.Id}");
        }
        var arten = Felder.Select(f => f.Art).ToHashSet(StringComparer.Ordinal);
        foreach (var liste in Unterlisten)
            if (liste.ZeigtAufObjektart.Length > 0 && !arten.Contains(liste.ZeigtAufObjektart))
                throw new InvalidOperationException(
                    $"Die Liste '{liste.Label}' zeigt auf die unbekannte Objektart '{liste.ZeigtAufObjektart}'.");
    }
}

public sealed record ObjektFeldDefinition
{
    public string Id { get; init; } = "";
    public string Art { get; init; } = "";
    public string Label { get; init; } = "";
    public string Thema { get; init; } = "";
    public string Gruppe { get; init; } = "";
    public string? Speicherfeld { get; init; }
    public string? KatalogId { get; init; }
    public string? Exportziel { get; init; }
    public string Belegstatus { get; init; } = "offen";
    public bool NurLesen { get; init; }
    public int Quellanzeigen { get; init; }
    public string? WebgisKennung { get; init; }
    public int? WebgisReihenfolge { get; init; }
    public string? WebgisTabelle { get; init; }
    public string? ErbtVon { get; init; }
    public bool WebgisPflicht { get; init; }
    public string? WebgisFeldart { get; init; }
    public string? WebgisEinheit { get; init; }
    public int? WebgisMaxLaenge { get; init; }
    public IReadOnlyList<string> NurBeiArt { get; init; } = [];
    public string? Elternfeld { get; init; }
    public string? BelegterElterntext { get; init; }

    /// <summary>Katalog mit den Eintraegen ALLER Elternwerte, jeder mit <see cref="ObjektAuswahl.Eltern"/>.
    /// <see cref="KatalogId"/> bleibt der bisherige Katalog (im Bestand nur der belegte Elternwert);
    /// die Anzeige bevorzugt diesen vollen Katalog und filtert ihn nach dem Code des Elternwerts.</summary>
    public string? KatalogIdJeEltern { get; init; }
}

public sealed record ObjektAuswahlKatalog
{
    public string Id { get; init; } = "";
    public bool CodesBestaetigt { get; init; }
    public IReadOnlyList<ObjektAuswahl> Eintraege { get; init; } = [];
}

public sealed record ObjektAuswahl(int Index, string? OriginalCode, string Label)
{
    /// <summary>Code des Elternwerts, zu dem dieser Eintrag gehoert - nur in einem Katalog
    /// je Elternwert gesetzt (Materialgruppe Beton -> Betonsorten). Leer bei flachen Listen.</summary>
    public string? Eltern { get; init; }

    /// <summary>Vom Benutzer ergaenzter Eintrag - steht in keinem WebGIS-Katalog. Ohne Code geht
    /// er nie in eine XTF; das ist Schutz, keine Einschraenkung.</summary>
    public bool Eigen { get; init; }

    /// <summary>Anzeigetext der Auswahlliste; eigene Eintraege sind als solche gekennzeichnet.</summary>
    public string Anzeige => Eigen ? $"{Label} · eigener Eintrag" : Label;

    public override string ToString() => Label.Length == 0 ? "(leer)" : Label;
}

/// <summary>Eine Aufklappliste der WebGIS-Maske. <paramref name="Art"/> ist das Objekt, an dem
/// sie haengt; <see cref="ZeigtAufObjektart"/> das Objekt, das ihre Zeilen fuehren.</summary>
public sealed record ObjektUnterliste(string Art, string Id, string Label, IReadOnlyList<string> Spalten)
{
    public string? WebgisAbschnitt { get; init; }
    public string? WebgisRelation { get; init; }
    /// <summary>Objektart der Zeilen. Leer bei einer Liste, die im Katalog kein Ziel nennt.</summary>
    public string ZeigtAufObjektart { get; init; } = "";

    /// <summary>Wahr, wenn die Zeilen eigene Akten sind. Falsch, wenn die Liste auf vorhandene
    /// Datensaetze zeigt (Einlaeufe sind die anschliessende Haltung, nicht ein zweites Objekt).</summary>
    public bool EigeneObjektart { get; init; }

    /// <summary>Im WebGIS nur lesend. Solche Listen bieten kein Anlegen und kein Entfernen.</summary>
    public bool NurLesen { get; init; }

    /// <summary>Die Liste kann mehrere Zeilen fuehren.</summary>
    public bool MehrerePositionen { get; init; } = true;

    /// <summary>Zeilen dieser Liste duerfen angelegt werden.</summary>
    public bool DarfAnlegen => EigeneObjektart && !NurLesen && ZeigtAufObjektart.Length > 0;
}
