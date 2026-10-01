namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>Erzeugt die Schachtliste eines Eigentuemerdossiers als PDF-Bytes.</summary>
public interface IDossierShaftListPdfService
{
    byte[] CreatePdf(DossierShaftListPdfModel model);
}
