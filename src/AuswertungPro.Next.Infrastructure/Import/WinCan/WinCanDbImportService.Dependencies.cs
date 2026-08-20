using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

public sealed partial class WinCanDbImportService
{
    private readonly IM150MdbRowReader _m150MdbRows;
    private readonly IXtfImportService _xtfImport;
    private readonly IProtocolService _protocolService;

    // Entscheidet, ob ein PDF wirklich zu DIESEM Schacht gehoert. Ohne diese Pruefung
    // landete jedes Haltungsprotokoll ("Section_4_892045-10.892870.pdf") auch an beiden
    // beteiligten Schaechten, weil deren Nummer im Dateinamen steht.
    private readonly IImportPdfReferenceResolver _pdfReferenzen;

    public WinCanDbImportService()
        : this(new PowerShellM150MdbRowReader(), new XtfImportServiceAdapter())
    {
    }

    public WinCanDbImportService(
        IM150MdbRowReader m150MdbRows,
        IXtfImportService xtfImport,
        IProtocolService? protocolService = null,
        IImportPdfReferenceResolver? pdfReferenzen = null)
    {
        _m150MdbRows = m150MdbRows ?? throw new ArgumentNullException(nameof(m150MdbRows));
        _xtfImport = xtfImport ?? throw new ArgumentNullException(nameof(xtfImport));
        _protocolService = protocolService ?? new ProtocolService();
        _pdfReferenzen = pdfReferenzen ?? new Protocols.ImportPdfReferenceResolver();
    }
}
