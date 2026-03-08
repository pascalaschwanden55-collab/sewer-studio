using AuswertungPro.Next.Domain.Models.Devis;

namespace AuswertungPro.Next.Application.Devis;

public interface IDevisMappingService
{
    DevisMappingConfig LoadConfig();
    MassnahmenEmpfehlung GetEmpfehlung(string schadensCode, string? char1, string? char2, int zustandsKlasse, int dn);
}

public sealed class MassnahmenEmpfehlung
{
    public DefektMassnahmeMapping? Mapping { get; set; }
    public ConfidenceLevel Konfidenz { get; set; }
    public string Begruendung { get; set; } = "";

    public static MassnahmenEmpfehlung KeineEmpfehlung(string code) => new()
    {
        Mapping = null,
        Konfidenz = ConfidenceLevel.Low,
        Begruendung = $"Kein Mapping fuer Schaden '{code}' definiert"
    };
}
