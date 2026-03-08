namespace AuswertungPro.Next.Domain.Models.Devis;

public enum GewerkTyp
{
    Baumeister,
    Rohrleitungsbau
}

public enum MassnahmenTyp
{
    Relining,
    Kurzliner,
    Robotersanierung,
    Leitungsersatz,
    Teilersatz,
    SchachtSanierung,
    SchachtErsatz,
    AnschlussErsatz,
    Monitoring,
    KeineAktion
}

public enum ConfidenceLevel
{
    High,
    Medium,
    Low,
    Manual
}
