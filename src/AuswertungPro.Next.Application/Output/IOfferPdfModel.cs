namespace AuswertungPro.Next.Application.Output;

/// <summary>
/// Marker-Vertrag fuer ein renderbares Angebots-/Kosten-PDF-Modell. Der konkrete
/// Modelltyp liegt bewusst in Infrastructure (naeher an Vorlage und Renderer); dieser
/// Marker haelt <see cref="IOfferPdfExportService.ExportAsync"/> typsicher, sodass nur
/// ein echtes PDF-Modell uebergeben werden kann und nicht versehentlich ein beliebiges
/// <see cref="object"/>.
/// </summary>
public interface IOfferPdfModel
{
}
