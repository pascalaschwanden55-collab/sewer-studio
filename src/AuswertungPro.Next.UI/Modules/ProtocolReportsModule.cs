using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI.Modules;

/// <summary>
/// Phase 5.2.B: Modul fuer Protokoll-, Foto- und PDF-Report-Services.
/// Reine no-arg-Konstruktoren (keine Config-Pfade noetig).
/// </summary>
internal static class ProtocolReportsModule
{
    public sealed record Services(
        IProtocolService Protocols,
        IPhotoImportService PhotoImport,
        ProtocolPdfExporter ProtocolPdfExporter);

    public static Services Configure() => new(
        Protocols: new ProtocolService(),
        PhotoImport: new PhotoImportService(),
        ProtocolPdfExporter: new ProtocolPdfExporter());
}
