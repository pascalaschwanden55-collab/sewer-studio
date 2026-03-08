namespace AuswertungPro.Next.Domain.Models.Devis;

public sealed class DevisPosition
{
    public string PositionNummer { get; set; } = "";
    public int Hauptposition { get; set; }
    public double Unterposition { get; set; }
    public double Einzelposition { get; set; }
    public string Bezeichnung { get; set; } = "";
    public string? Beschreibung { get; set; }
    public string Einheit { get; set; } = "";
    public decimal Menge { get; set; }
    public decimal Einheitspreis { get; set; }
    public decimal Betrag => Menge * Einheitspreis;
    public MengenHerleitung? Herleitung { get; set; }
}

public sealed class DevisAbschnitt
{
    public string Bezeichnung { get; set; } = "";
    public string? VonSchacht { get; set; }
    public string? BisSchacht { get; set; }
    public List<DevisPosition> Positionen { get; set; } = [];
    public decimal Total => Positionen.Sum(p => p.Betrag);
}

public sealed class DevisHauptgruppe
{
    public int Nummer { get; set; }
    public string Bezeichnung { get; set; } = "";
    public List<DevisPosition> Positionen { get; set; } = [];
    public List<DevisAbschnitt> Abschnitte { get; set; } = [];
    public decimal Total => Positionen.Sum(p => p.Betrag) + Abschnitte.Sum(a => a.Total);
}

public sealed class Eigendevis
{
    public string Titel { get; set; } = "Eigendevis Abwasser";
    public string Baustelle { get; set; } = "";
    public string Zone { get; set; } = "";
    public GewerkTyp Gewerk { get; set; }
    public List<DevisHauptgruppe> Hauptgruppen { get; set; } = [];
    public decimal MwstSatz { get; set; } = 0.081m;
    public decimal GesamttotalExklMwst => Hauptgruppen.Sum(h => h.Total);
    public decimal MwstBetrag => GesamttotalExklMwst * MwstSatz;
    public decimal GesamttotalInklMwst => GesamttotalExklMwst + MwstBetrag;
}

public sealed class MengenHerleitung
{
    public string Quelle { get; set; } = "";
    public string Formel { get; set; } = "";
    public List<string> BezogeneHaltungen { get; set; } = [];
    public List<string> BezogeneSchaeden { get; set; } = [];
    public ConfidenceLevel Konfidenz { get; set; } = ConfidenceLevel.Medium;
}

public sealed class DevisErgebnis
{
    public Eigendevis Baumeister { get; set; } = new();
    public Eigendevis Rohrleitungsbau { get; set; } = new();
    public List<string> Warnungen { get; set; } = [];
}
