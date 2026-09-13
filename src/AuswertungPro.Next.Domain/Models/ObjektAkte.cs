using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>Eigene Identitaet fuer Deckel, Ereignisse und ergaenzende Angaben am Bestandsobjekt.</summary>
public sealed class ObjektAkte
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Art { get; set; } = "";
    public List<Guid> Bezuege { get; set; } = new();
    public Dictionary<string, ObjektFeldWert> Werte { get; set; } = new(StringComparer.Ordinal);
    public List<ObjektQuellbeleg> Quellen { get; set; } = new();
    public Guid? HauptdeckelId { get; set; }
    public Dictionary<string, List<Dictionary<string, string>>> Unterlisten { get; set; } = new();
    public override string ToString()
    {
        var name = Werte.GetValueOrDefault(Art == "sanierung" ? "sanierung.s_name" : Art + ".bezeichnung")?.Text;
        if (string.IsNullOrWhiteSpace(name)) name = Quellen.Select(q => q.Werte.GetValueOrDefault("Bezeichnung")).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        var art = Art.Length == 0 ? "Objekt" : char.ToUpperInvariant(Art[0]) + Art[1..];
        return string.IsNullOrWhiteSpace(name) ? $"{art} · {Id.ToString("N")[..6]}" : $"{art} · {name}";
    }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Zusatzdaten { get; set; }
}

public sealed class ObjektFeldWert
{
    // Bei einem Bestandsfeld ist dessen Fields-Wert die Wahrheit. Text ist dann nur
    // der zur Auswahl gehoerende Originaltext; bei Abweichung wird er nicht angezeigt.
    public string Text { get; set; } = "";
    public string? KatalogId { get; set; }
    public string? Originalcode { get; set; }
    public int? LokalerEintrag { get; set; }
    public string? Bestandswert { get; set; }
    public bool VonHand { get; set; }
    public DateTime GeaendertUtc { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Zusatzdaten { get; set; }
}

public sealed class ObjektQuellbeleg
{
    public string System { get; set; } = "";
    public string Datei { get; set; } = "";
    public string Modell { get; set; } = "";
    public string Klasse { get; set; } = "";
    public string Kennung { get; set; } = "";
    public bool IstLokaleKennung { get; set; }
    public DateTime ImportiertUtc { get; set; }
    public Dictionary<string, string> Werte { get; set; } = new();
    public Dictionary<string, string> Referenzen { get; set; } = new();
    /// <summary>Unveränderte INTERLIS-Strukturen (z.B. Verlauf mit Kreisbögen), keine externen Dateipfade.</summary>
    public Dictionary<string, string> Strukturen { get; set; } = new();
    [JsonExtensionData] public Dictionary<string, JsonElement>? Zusatzdaten { get; set; }
}
