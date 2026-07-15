namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ergaenzt eine manuelle Schacht-PDF-Auswahl um zusammengehoerige
/// Schachtprotokoll- beziehungsweise Schachtfoto-PDFs im selben Ordner.
/// </summary>
public interface IShaftPdfSelectionExpander
{
    IReadOnlyList<string> Expand(IReadOnlyList<string> selectedPdfFiles);
}
