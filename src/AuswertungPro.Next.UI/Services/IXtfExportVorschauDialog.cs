using AuswertungPro.Next.Application.UseCases.Xtf;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Zeigt die XTF-Vorschau (Zusammenfassung, Alt/Neu-Tabelle, Warnungen, Details) in einem
/// eigenen Fenster. <see cref="Bestaetige"/> fragt "Jetzt schreiben?"; <see cref="ZeigeFehler"/>
/// zeigt dieselbe Vorschau nur zum Lesen.
/// </summary>
public interface IXtfExportVorschauDialog
{
    bool Bestaetige(XtfExportVorschau vorschau);

    void ZeigeFehler(XtfExportVorschau vorschau);
}
