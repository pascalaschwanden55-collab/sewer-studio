using System;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Fixwelle (Minor): Ein Aufklapper mit <c>StaysOpen="False"</c> schliesst sich beim Klick
/// auf seinen eigenen Knopf selbst. Ohne Zeitregel oeffnete derselbe Klick ihn sofort wieder —
/// zuklappen war ueber den Knopf unmoeglich.
/// </summary>
public sealed class PopupToggleGateTests
{
    private static readonly DateTime Jetzt = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Ohne_vorheriges_Schliessen_darf_geoeffnet_werden()
        => Assert.True(PopupToggleGate.DarfOeffnen(null, Jetzt));

    [Fact]
    public void Unmittelbar_nach_dem_Schliessen_wird_nicht_erneut_geoeffnet()
        => Assert.False(PopupToggleGate.DarfOeffnen(Jetzt, Jetzt.AddMilliseconds(10)));

    [Fact]
    public void Nach_der_Sperrzeit_darf_wieder_geoeffnet_werden()
        => Assert.True(PopupToggleGate.DarfOeffnen(Jetzt, Jetzt + PopupToggleGate.Sperrzeit));

    [Fact]
    public void Eine_rueckwaerts_laufende_Uhr_blockiert_nicht_dauerhaft()
        => Assert.True(PopupToggleGate.DarfOeffnen(Jetzt, Jetzt.AddSeconds(-5)));
}
