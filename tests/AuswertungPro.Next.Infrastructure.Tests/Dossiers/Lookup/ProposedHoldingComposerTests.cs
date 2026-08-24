using System;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

/// <summary>
/// Die eine Regel, welche Leitungen einer Parzelle vorgeschlagen und welche
/// davon angehakt werden.
///
/// Sie entscheidet, was im Eigentuemerbrief steht. Bis heute lag sie zweimal
/// im Code — einmal fuer die Einzelabfrage, einmal fuer den Stapel — und die
/// zwei Fassungen waren bereits verschieden.
/// </summary>
public sealed class ProposedHoldingComposerTests
{
    // IsPrivate leitet sich aus dem Eigentuemertext des Kantons ab.
    private static NetworkHolding Kanton(string name, bool privat)
        => new(name, privat ? "Privat" : "Gemeinde", null, "");

    [Fact]
    public void Eine_private_Leitung_die_das_Projekt_fuehrt_ist_angehakt()
    {
        var ergebnis = ProposedHoldingComposer.Compose(
            new[] { Kanton("100-200", privat: true) },
            new[] { "100-200" },
            "439");

        var zeile = Assert.Single(ergebnis);
        Assert.True(zeile.IsPrivate);
        Assert.True(zeile.InProject);
        Assert.True(zeile.Preselected);
        Assert.Equal("Lage", zeile.Origin);
    }

    [Fact]
    public void Eine_oeffentliche_Leitung_wird_nicht_angehakt()
    {
        // Der Kanton unterhaelt sie — sie gehoert nicht in den Eigentuemerbrief.
        var ergebnis = ProposedHoldingComposer.Compose(
            new[] { Kanton("100-200", privat: false) },
            new[] { "100-200" },
            "439");

        Assert.False(Assert.Single(ergebnis).Preselected);
    }

    [Fact]
    public void Eine_Leitung_die_das_Projekt_nicht_fuehrt_wird_nicht_angehakt()
    {
        var ergebnis = ProposedHoldingComposer.Compose(
            new[] { Kanton("100-200", privat: true) },
            Array.Empty<string>(),
            "439");

        var zeile = Assert.Single(ergebnis);
        Assert.False(zeile.InProject);
        Assert.False(zeile.Preselected);
    }

    [Fact]
    public void Was_nur_der_Name_der_Parzelle_zuordnet_kommt_dazu_und_ist_angehakt()
    {
        // Hausanschluesse fuehrt der Kanton nicht; ihr Knotenname nennt die Parzelle.
        var ergebnis = ProposedHoldingComposer.Compose(
            Array.Empty<NetworkHolding>(),
            new[] { "439.01-36051" },
            "439");

        var zeile = Assert.Single(ergebnis);
        Assert.Equal("439.01-36051", zeile.Designation);
        Assert.True(zeile.IsPrivate);
        Assert.True(zeile.InProject);
        Assert.True(zeile.Preselected);
        Assert.Equal("Name", zeile.Origin);
    }

    [Fact]
    public void Eine_Leitung_die_beide_Wege_finden_erscheint_nur_einmal()
    {
        var ergebnis = ProposedHoldingComposer.Compose(
            new[] { Kanton("439.01-36051", privat: true) },
            new[] { "439.01-36051" },
            "439");

        var zeile = Assert.Single(ergebnis);
        Assert.Equal("Lage", zeile.Origin);
    }

    [Fact]
    public void Ein_doppelter_Kantonstreffer_erscheint_nur_einmal()
    {
        // Der Kartendienst liefert je Geometrieteil einen Treffer; dieselbe
        // Leitung darf im Brief trotzdem nur einmal stehen.
        var ergebnis = ProposedHoldingComposer.Compose(
            new[] { Kanton("100-200", true), Kanton("100-200", true) },
            new[] { "100-200" },
            "439");

        Assert.Single(ergebnis);
    }

    [Fact]
    public void Leerraum_um_einen_Projektnamen_zaehlt_nicht_als_anderer_Name()
    {
        // Sonst waere dieselbe Leitung je nach Weg einmal angehakt und einmal nicht.
        var ergebnis = ProposedHoldingComposer.Compose(
            new[] { Kanton("100-200", privat: true) },
            new[] { "  100-200  " },
            "439");

        Assert.True(Assert.Single(ergebnis).Preselected);
    }

    [Fact]
    public void Die_Lage_Treffer_stehen_vor_den_Namens_Treffern()
    {
        var ergebnis = ProposedHoldingComposer.Compose(
            new[] { Kanton("100-200", privat: true) },
            new[] { "100-200", "439.01-36051" },
            "439");

        Assert.Equal(new[] { "100-200", "439.01-36051" },
            ergebnis.Select(z => z.Designation).ToArray());
    }

    [Fact]
    public void Ohne_Parzellennummer_kommt_ueber_den_Namen_nichts_dazu()
    {
        var ergebnis = ProposedHoldingComposer.Compose(
            Array.Empty<NetworkHolding>(),
            new[] { "439.01-36051" },
            "");

        Assert.Empty(ergebnis);
    }

    [Fact]
    public void Fehlende_Listen_ergeben_eine_leere_Liste_und_keinen_Absturz()
    {
        Assert.Empty(ProposedHoldingComposer.Compose(null, null, "439"));
    }
}
