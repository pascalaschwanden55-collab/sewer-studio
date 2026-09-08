using System.Windows.Input;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste, Fix-Runde 1: die Tastaturregel als reine Entscheidung.
///
/// Der Kern ist Escape aus einem Eingabefeld: Es darf die Haltung nicht zuklappen, sonst
/// verschwindet das Formular unter dem Cursor und die noch nicht zurueckgeschriebene Eingabe
/// mit ihm. Erst der zweite Escape — dann auf der Zeile — klappt zu.
/// </summary>
public sealed class HaltungAufklappTastenregelTests
{
    [Theory]
    // Auf der Zeile
    [InlineData(Key.Enter, true, AufklappTastenAktion.Schalten)]
    [InlineData(Key.Space, true, AufklappTastenAktion.Schalten)]
    [InlineData(Key.Escape, true, AufklappTastenAktion.Zuklappen)]
    // Im Formular (Eingabefeld)
    [InlineData(Key.Escape, false, AufklappTastenAktion.FokusAufZeile)]
    [InlineData(Key.Enter, false, AufklappTastenAktion.Nichts)]
    [InlineData(Key.Space, false, AufklappTastenAktion.Nichts)]
    // Die Pfeiltasten gehoeren der Liste: Auswahl wechseln, aber nicht aufklappen.
    [InlineData(Key.Down, true, AufklappTastenAktion.Nichts)]
    [InlineData(Key.Up, true, AufklappTastenAktion.Nichts)]
    public void Regel(Key taste, bool aufDerZeile, AufklappTastenAktion erwartet)
        => Assert.Equal(erwartet, HaltungAufklappTastenregel.Bestimme(taste, inEinerZeile: true, aufDerZeile));

    [Theory]
    [InlineData(Key.Escape)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void Ausserhalb_einer_Zeile_passiert_nichts(Key taste)
        => Assert.Equal(
            AufklappTastenAktion.Nichts,
            HaltungAufklappTastenregel.Bestimme(taste, inEinerZeile: false, aufDerZeile: false));
}
