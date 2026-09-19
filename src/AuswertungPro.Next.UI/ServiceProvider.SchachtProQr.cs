using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.SchachtPro;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    public ISchachtProQrImportService SchachtProQrImport { get; } = new SchachtProQrImportService(new QrImageReader());
}
