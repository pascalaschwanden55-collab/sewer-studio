using AuswertungPro.Next.Domain.Models;
namespace AuswertungPro.Next.UI.ViewModels.Pages;
public sealed partial class SchaechtePageViewModel
{
    public IReadOnlyList<string> StatusOptions => FieldCatalog.GetComboItems(FieldKeys.OperatingStatus);
    public IReadOnlyList<string> SanierungsbedarfOptions => DataPage.SanierungsbedarfOptionen.Alle;
    // Seit 23.09.2026 Auswahlfelder mit der WebGIS-Liste des Schachts (vorher Freitext).
    public IReadOnlyList<string> NutzungsartOptions => WebGisBegriffe.Fuer(true, FieldKeys.UsageType)!.Auswahl;
    public IReadOnlyList<string> LagebestimmungOptions => WebGisBegriffe.Fuer(true, FieldKeys.PositionAccuracy)!.Auswahl;
}
