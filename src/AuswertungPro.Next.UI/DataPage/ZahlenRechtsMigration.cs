using System;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Fixwelle 2b, Runde 2: Einmalige Migration des gespeicherten Spaltenlayouts.
///
/// Anlass: „Zahlen stehen rechts" (P1) und die Startbreiten (P3) wirken nur beim Aufbau der
/// Spalten. Danach ueberschreibt <c>DataGridColumnLayoutController.Restore</c> beides mit dem
/// gespeicherten Layout — und in einer bestehenden Installation steht dort ueberall
/// <c>Left</c> und eine Kopfbreite. Bei Pascal stuenden DN und Laenge deshalb weiterhin
/// links, obwohl der Code stimmt.
///
/// Gleiches Muster wie <see cref="KompaktStartRegel"/>: Solange
/// <c>DataPageLayoutSettings.ZahlenRechtsEinmalGesetzt</c> false ist, wird EINMAL angepasst,
/// danach nie wieder. Reine, WPF-freie Rechnung ohne Seiteneffekt; das Speichern uebernimmt
/// der Aufrufer.
///
/// Zwei Regeln dabei:
/// <list type="bullet">
/// <item>Die Ausrichtung jeder Zahlenspalte wird auf <c>Right</c> gehoben. Links konnte sie
/// niemand bewusst gewaehlt haben — bis zu dieser Etappe hat das Programm gar nichts
/// anderes erzeugt.</item>
/// <item>Breiten werden nur ANGEHOBEN, nie verkleinert. Eine in Pixeln gespeicherte Breite
/// ist eine bewusste Handeinstellung und bleibt, solange sie mindestens der Startbreite
/// entspricht. Eine Breite ohne Pixel-Einheit (<c>SizeToHeader</c>) ist dagegen gar keine
/// Wahl des Benutzers, sondern die Kopfbreite — genau das, was der Neuaufbau heute durch die
/// Startbreite ersetzt.</item>
/// </list>
/// </summary>
public static class ZahlenRechtsMigration
{
    private const string Rechts = "Right";
    private const string Pixel = "Pixel";

    /// <summary>
    /// Wendet die Migration auf <paramref name="layout"/> an und ruft <paramref name="speichern"/>
    /// nur bei einer echten Aenderung.
    /// </summary>
    /// <param name="falte">
    /// Namensvergleich der Liste (bei Schaechten <c>SchachtFeldnamen.Falte</c>), weil
    /// Schachtfelder nach der Kopfzeile der Excel-Vorlage heissen.
    /// </param>
    public static void WendeAn(DataPageLayoutSettings layout, Action speichern, Func<string, string>? falte = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(speichern);

        if (!Entscheide(layout, falte))
            return;

        speichern();
    }

    /// <summary>
    /// Die reine Entscheidung: veraendert <paramref name="layout"/> und liefert, ob sich
    /// etwas geaendert hat. Getrennt vom Speichern, damit sie ohne Datei pruefbar ist.
    /// </summary>
    public static bool Entscheide(DataPageLayoutSettings layout, Func<string, string>? falte = null)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.ZahlenRechtsEinmalGesetzt)
            return false;

        // Schon das Flag selbst muss gespeichert werden, sonst liefe die Migration bei jedem
        // Start erneut und wuerde eine spaeter von Hand nach links gestellte Zahlenspalte
        // immer wieder umstellen.
        layout.ZahlenRechtsEinmalGesetzt = true;

        foreach (var spalte in layout.Columns ?? [])
        {
            if (string.IsNullOrWhiteSpace(spalte.FieldName))
                continue;

            if (DataPageColumnStyleRules.IstZahlenspalte(spalte.FieldName, falte)
                && !string.Equals(spalte.HorizontalAlignment, Rechts, StringComparison.Ordinal))
            {
                spalte.HorizontalAlignment = Rechts;
            }

            if (NovaSpaltenbreiten.Startbreite(spalte.FieldName, falte) is not double startbreite)
                continue;

            var istHandbreite = string.Equals(spalte.WidthUnitType, Pixel, StringComparison.Ordinal);
            if (!istHandbreite || spalte.WidthValue < startbreite)
            {
                spalte.WidthValue = startbreite;
                spalte.WidthUnitType = Pixel;
            }
        }

        return true;
    }
}
