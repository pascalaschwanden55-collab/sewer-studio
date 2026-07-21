using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel
{
    internal IInspectionProtocolFileLocator InspectionProtocolFiles
        => _inspectionProtocolFiles;
}
