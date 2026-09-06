using AuswertungPro.Next.Application.UseCases.Import.Quellen;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Schaut in eine XTF-Datei hinein und meldet, was drinsteht.
///
/// Bewusst ein eigener Vertrag: Die Erkennung braucht diese Angaben, ohne die Datei
/// importieren zu muessen — und ohne sich auf die ersten Kilobyte zu verlassen.
/// </summary>
public interface IXtfQuellenPruefer
{
    /// <summary>
    /// Liest Modellnamen und Elementzahlen. Eine unlesbare Datei liefert
    /// <see cref="XtfQuellenmerkmale.NichtLesbar"/> und wirft nicht — ein defektes XML
    /// darf die Pruefung der uebrigen Quellen nicht abbrechen.
    /// </summary>
    XtfQuellenmerkmale Pruefe(string pfad);
}
