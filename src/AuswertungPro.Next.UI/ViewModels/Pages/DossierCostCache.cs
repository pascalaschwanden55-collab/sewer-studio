using System;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Die Kostendateien eines Dossier-Durchgangs: die der Haltungen und die
/// zusammengefuehrten der Schaechte.
/// </summary>
internal sealed record DossierCostSnapshot(
    ProjectCostStore Haltungen,
    ProjectCostStore Schaechte);

/// <summary>
/// Haelt den einmal gelesenen Kostenstand fuer die geoeffnete Dossier-Seite.
///
/// Die drei Dateien liegen zusammen bei knapp einem halben Megabyte. Sie bei
/// jedem Klick auf eine Liegenschaft neu einzulesen laesst die Oberflaeche
/// stocken — und liefert dabei immer dieselben Zahlen.
///
/// Veralten kann der Stand kaum: Die Seite wird bei jeder Navigation neu
/// gebaut, beim Betreten ist er also frisch. Wer waehrenddessen in einem
/// anderen Fenster Kosten aendert, holt sie mit „Aktualisieren".
/// </summary>
internal sealed class DossierCostCache
{
    private readonly Func<DossierCostSnapshot> _load;
    private DossierCostSnapshot? _current;

    public DossierCostCache(Func<DossierCostSnapshot> load)
        => _load = load ?? throw new ArgumentNullException(nameof(load));

    public DossierCostSnapshot Get() => _current ??= _load();

    /// <summary>Verwirft den Stand; die naechste Abfrage liest erneut.</summary>
    public void Invalidate() => _current = null;
}
