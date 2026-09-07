using System;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2b, Task 4: Kompakt wird nur EINMAL als Standard gesetzt, wenn eine
/// bestehende Installation aktualisiert wird. Das Flag <c>NovaKompaktEinmalGesetzt</c> ist
/// dann noch nicht gesetzt (false); danach zaehlt ausschliesslich die vom Benutzer gewaehlte
/// Ansicht — auch wenn diese wieder "alle" oder eine andere Ansicht ist. Reine, WPF-freie
/// Regel ohne Seiteneffekt; das Speichern uebernimmt der Aufrufer.
/// </summary>
public static class KompaktStartRegel
{
    /// <param name="gespeicherterKey">Die zuletzt gespeicherte Spaltenansicht (kann null sein).</param>
    /// <param name="flag">Wurde die Einmal-Migration bereits durchgefuehrt?</param>
    /// <returns>
    /// Beim ersten Mal (flag = false): immer "kompakt" und das Flag neu = true.
    /// Danach (flag = true): der gespeicherte Schluessel bleibt unangetastet.
    /// </returns>
    public static (string? Key, bool FlagNeu) Entscheide(string? gespeicherterKey, bool flag)
        => flag ? (gespeicherterKey, true) : ("kompakt", true);

    /// <summary>
    /// Duenner Adapter fuer die Seiten (DataPage/SchaechtePage): wendet <see cref="Entscheide"/>
    /// auf ein <c>DataPageLayoutSettings</c> an und speichert nur bei einer echten Aenderung.
    /// </summary>
    public static void WendeAn(AuswertungPro.Next.UI.DataPageLayoutSettings layout, Action speichern)
    {
        var (key, flagNeu) = Entscheide(layout.ActiveColumnView, layout.NovaKompaktEinmalGesetzt);
        if (flagNeu == layout.NovaKompaktEinmalGesetzt
            && string.Equals(key, layout.ActiveColumnView, StringComparison.Ordinal))
            return;

        layout.ActiveColumnView = key ?? layout.ActiveColumnView;
        layout.NovaKompaktEinmalGesetzt = flagNeu;
        speichern();
    }
}
