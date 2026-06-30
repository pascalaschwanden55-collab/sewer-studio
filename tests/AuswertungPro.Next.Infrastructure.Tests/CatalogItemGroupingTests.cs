using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer CatalogItemGrouping.
/// </summary>
public sealed class CatalogItemGroupingTests
{
    [Theory]
    [InlineData("INSTALL_UV_ANLAGE", "Installation")]
    [InlineData("install_hl_anlage", "Installation")]       // case-insensitive
    [InlineData("VORARBEIT_REINIGUNG", "Vorarbeiten")]
    [InlineData("QK_DICHTHEITSPRUEF", "Qualitaetskontrolle")]
    [InlineData("HAUPTARBEIT_X", "Hauptarbeit")]
    [InlineData("SCHLAUCHLINER_DN200", "Hauptarbeit")]
    [InlineData("LINERENDMANSCHETTE_LEM", "Hauptarbeit")]
    [InlineData("KURZLINER_DN150", "Hauptarbeit")]
    [InlineData("MANSCHETTE_ANKER", "Hauptarbeit")]
    [InlineData("ANSCHLUSS_FRAESEN", "Hauptarbeit")]
    [InlineData("ROBOTER_ARBEIT", "Sonstiges")]
    [InlineData("", "Sonstiges")]
    public void DeriveGroupFromKey_ReturnsExpectedGroup(string key, string expectedGroup)
    {
        var group = CatalogItemGrouping.DeriveGroupFromKey(key);
        Assert.Equal(expectedGroup, group);
    }

    [Fact]
    public void GetGroupOrder_InstallationIsFirst()
    {
        var installOrder = CatalogItemGrouping.GetGroupOrder("Installation");
        var sonstigesOrder = CatalogItemGrouping.GetGroupOrder("Sonstiges");

        Assert.True(installOrder < sonstigesOrder);
    }

    [Fact]
    public void GetGroupOrder_NullOrEmpty_ReturnsHigherThanAnyKnownGroup()
    {
        var unknown = CatalogItemGrouping.GetGroupOrder(null);
        var max = CatalogItemGrouping.GroupOrder.Length;

        Assert.True(unknown > max); // unbekannte Gruppe kommt nach allen bekannten
    }

    [Fact]
    public void GetGroupOrder_CaseInsensitive()
    {
        var lower = CatalogItemGrouping.GetGroupOrder("hauptarbeit");
        var upper = CatalogItemGrouping.GetGroupOrder("Hauptarbeit");

        Assert.Equal(lower, upper);
    }

    [Fact]
    public void GroupOrder_ContainsExpectedGroups()
    {
        Assert.Contains("Installation", CatalogItemGrouping.GroupOrder);
        Assert.Contains("Vorarbeiten", CatalogItemGrouping.GroupOrder);
        Assert.Contains("Hauptarbeit", CatalogItemGrouping.GroupOrder);
        Assert.Contains("Qualitaetskontrolle", CatalogItemGrouping.GroupOrder);
        Assert.Contains("Sonstiges", CatalogItemGrouping.GroupOrder);
    }

    // ── F3: unbekannte nicht-leere Gruppe hat denselben Rang wie leere Gruppe ─────────────

    [Fact]
    public void GetGroupOrder_UnbekanntNichtLeer_GleicherRangWieLeer()
    {
        // Alter Stand: unbekannte nicht-leere Gruppe -> GroupOrder.Length+1 (wie leere Gruppe)
        var unbekanntVoll = CatalogItemGrouping.GetGroupOrder("IrgendwasUnbekanntes");
        var leer = CatalogItemGrouping.GetGroupOrder(null);

        Assert.Equal(leer, unbekanntVoll);
    }
}
