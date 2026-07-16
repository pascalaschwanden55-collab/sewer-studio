using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

public sealed partial class WinCanDbImportService
{
    private readonly IM150MdbRowReader _m150MdbRows;
    private readonly IXtfImportService _xtfImport;

    public WinCanDbImportService()
        : this(new PowerShellM150MdbRowReader(), new XtfImportServiceAdapter())
    {
    }

    public WinCanDbImportService(
        IM150MdbRowReader m150MdbRows,
        IXtfImportService xtfImport)
    {
        _m150MdbRows = m150MdbRows ?? throw new ArgumentNullException(nameof(m150MdbRows));
        _xtfImport = xtfImport ?? throw new ArgumentNullException(nameof(xtfImport));
    }
}
