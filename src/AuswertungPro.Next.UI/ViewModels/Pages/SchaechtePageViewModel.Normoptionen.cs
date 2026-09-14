using AuswertungPro.Next.Domain.Models;
namespace AuswertungPro.Next.UI.ViewModels.Pages;
public sealed partial class SchaechtePageViewModel
{
    public IReadOnlyList<string> StatusOptions => FieldCatalog.GetComboItems(FieldKeys.OperatingStatus);
    public IReadOnlyList<string> SanierungsbedarfOptions => DataPage.SanierungsbedarfOptionen.Alle;
}
