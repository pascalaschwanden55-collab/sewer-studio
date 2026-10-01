using AuswertungPro.Next.Domain.Models;
namespace AuswertungPro.Next.UI.ViewModels.Pages;
public sealed partial class DataPageViewModel
{
    public IReadOnlyList<string> NutzungsartOptions => FieldCatalog.GetComboItems(FieldKeys.UsageType);
}
