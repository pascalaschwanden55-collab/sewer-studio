using System;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Die reine Regel hinter <see cref="PopupToggle"/>. Ein Aufklapper mit
/// <c>StaysOpen="False"</c> schliesst sich beim Klick daneben selbst — und der Klick auf den
/// eigenen Knopf ist ein Klick daneben. Der Knopf sah danach ein geschlossenes Fenster und
/// oeffnete es sofort wieder; zuklappen war ueber den Knopf unmoeglich.
///
/// Deshalb: Ein Oeffnungswunsch unmittelbar nach dem Schliessen gehoert noch zum selben Klick
/// und wird verworfen. Reine Werte-Logik ohne WPF, damit sie pruefbar bleibt.
/// </summary>
public static class PopupToggleGate
{
    /// <summary>So lange nach dem Schliessen gilt ein Oeffnungswunsch als derselbe Klick.</summary>
    public static readonly TimeSpan Sperrzeit = TimeSpan.FromMilliseconds(250);

    /// <param name="geschlossenUm">Zeitpunkt des letzten Schliessens; null = noch nie.</param>
    public static bool DarfOeffnen(DateTime? geschlossenUm, DateTime jetzt)
        => geschlossenUm is not { } zeit || jetzt - zeit >= Sperrzeit || jetzt < zeit;
}

/// <summary>
/// Verbindet einen Knopf mit seinem Aufklapper: Ein Klick oeffnet ihn, ein zweiter schliesst ihn
/// wieder. Die Zeitregel liegt in <see cref="PopupToggleGate"/>.
/// </summary>
public sealed class PopupToggle
{
    private readonly Popup _popup;
    private readonly Func<DateTime> _jetzt;
    private DateTime? _geschlossenUm;

    public PopupToggle(Popup popup, Func<DateTime>? jetzt = null)
    {
        _popup = popup ?? throw new ArgumentNullException(nameof(popup));
        _jetzt = jetzt ?? (() => DateTime.UtcNow);
        _popup.Closed += (_, _) => _geschlossenUm = _jetzt();
    }

    /// <summary>Auf- oder zuklappen; ein Klick, der den Aufklapper gerade geschlossen hat, oeffnet nicht erneut.</summary>
    public void Umschalten()
    {
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }

        if (!PopupToggleGate.DarfOeffnen(_geschlossenUm, _jetzt()))
            return;

        _geschlossenUm = null;
        _popup.IsOpen = true;
    }
}
