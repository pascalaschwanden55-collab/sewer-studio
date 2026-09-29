using AuswertungPro.Next.Application.UseCases.Datenaenderungen;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private IDatenaenderungsVerlauf? _datenaenderungsVerlauf;

    /// <summary>Rueckgaengig/Wiederholen fuer Haltungs- und Schachtdaten (Optik Aufgabe 16), einer je Programmlauf.</summary>
    public IDatenaenderungsVerlauf DatenaenderungsVerlauf => _datenaenderungsVerlauf ??= new DatenaenderungsVerlauf();
}
