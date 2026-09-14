namespace AuswertungPro.Next.Application.UseCases;

/// <summary>Die Vorschau erlaubt nur die Wahl zwischen zwei festgehaltenen Werten.</summary>
public sealed class GeoShopFeldWahl : System.ComponentModel.INotifyPropertyChanged
{
    public Guid ObjektId { get; }
    public string Feld { get; }
    public bool Bestandsfeld { get; }
    public string Objekt { get; }
    public string Bezeichnung { get; }
    public string Vorher { get; }
    public string Nachher { get; }
    public string Herkunft { get; }
    private readonly bool _darfUebernehmen;
    internal GeoShopFeldWahl? Partner { get; set; }
    public bool DarfUebernehmen => _darfUebernehmen && Partner?._darfUebernehmen != false;
    private readonly string _hinweis;
    public string Hinweis => Partner is null ? _hinweis
        : !DarfUebernehmen ? "Dieses Wertepaar enthält eine geschützte Handeingabe. Beide Werte bleiben erhalten."
        : _hinweis + " Beide Werte werden gemeinsam gewählt.";
    private bool _uebernehmen;
    public bool Uebernehmen
    {
        get => _uebernehmen && DarfUebernehmen;
        set
        {
            _uebernehmen = value && DarfUebernehmen;
            PropertyChanged?.Invoke(this, new(nameof(Uebernehmen)));
            if (Partner is { } partner)
            {
                partner._uebernehmen = _uebernehmen;
                partner.PropertyChanged?.Invoke(partner, new(nameof(Uebernehmen)));
            }
        }
    }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    internal string Schluessel => $"{ObjektId:N}|{(Bestandsfeld ? "feld" : "akte")}|{Feld}";

    internal GeoShopFeldWahl(Guid id, string feld, bool bestandsfeld, string objekt, string label,
        string vorher, string nachher, string herkunft, bool hand)
    {
        ObjektId = id; Feld = feld; Bestandsfeld = bestandsfeld; Objekt = objekt; Bezeichnung = label;
        Vorher = vorher; Nachher = nachher; Herkunft = herkunft; _darfUebernehmen = !hand;
        Uebernehmen = !hand && string.IsNullOrWhiteSpace(vorher);
        _hinweis = hand ? "Handeingabe geschützt (auch bewusst leer)."
            : string.IsNullOrWhiteSpace(vorher) ? "Leeres Feld ergänzen."
            : feld == "Material" ? "Abweichung prüfen: Bauwerksmaterial und Auskleidung können verschieden sein."
            : "Abweichung: bisherigen Wert behalten oder GeoShop wählen.";
    }

    // Kennungen und Codes dürfen ihre führenden Nullen nicht durch einen Zahlenvergleich verlieren.
    internal static bool Gleich(string a, string b) => a.Trim() == b.Trim();
}
