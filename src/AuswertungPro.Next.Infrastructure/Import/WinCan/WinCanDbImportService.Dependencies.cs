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

    // Schaut beim SDF-Ersatzweg in jede gefundene XTF hinein, statt alle blind zu lesen.
    private readonly IXtfQuellenPruefer _xtfQuellenPruefer;

    // Erkennt bytegleiche Videokopien. Eigene Instanz je Dienst: Der Zwischenspeicher
    // gehoert zu einem Importlauf und darf nicht zwischen Laeufen weiterleben.
    private readonly IMedienInhaltsIndex _medienInhalt;

    public WinCanDbImportService()
        : this(new PowerShellM150MdbRowReader(), new XtfImportServiceAdapter())
    {
    }

    public WinCanDbImportService(
        IM150MdbRowReader m150MdbRows,
        IXtfImportService xtfImport,
        IProtocolService? protocolService = null,
        IImportPdfReferenceResolver? pdfReferenzen = null,
        IXtfQuellenPruefer? xtfQuellenPruefer = null,
        IMedienInhaltsIndex? medienInhalt = null)
    {
        _m150MdbRows = m150MdbRows ?? throw new ArgumentNullException(nameof(m150MdbRows));
        _xtfImport = xtfImport ?? throw new ArgumentNullException(nameof(xtfImport));
        _protocolService = protocolService ?? new ProtocolService();
        _pdfReferenzen = pdfReferenzen ?? new Protocols.ImportPdfReferenceResolver();
        _xtfQuellenPruefer = xtfQuellenPruefer ?? new XtfQuellenPruefer();
        _medienInhalt = medienInhalt ?? new Common.MedienInhaltsIndex();
    }
}
