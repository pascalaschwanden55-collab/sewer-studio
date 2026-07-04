using AuswertungPro.Next.UI.QgisBridge;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die QGIS-Bridge merkt sich die zuletzt gewaehlte Haltung projektbezogen und "sticky":
/// Abwahl/Seitenwechsel loeschen sie nicht, ein Projektwechsel schon.
/// Achtung: <see cref="QgisBridgeSelection"/> ist statisch — jeder Test setzt sie zurueck.
/// </summary>
public sealed class QgisBridgeSelectionTests : IDisposable
{
    public QgisBridgeSelectionTests() => QgisBridgeSelection.Reset();

    public void Dispose() => QgisBridgeSelection.Reset();

    [Fact]
    public void Set_merkt_sich_auswahl_fuer_projekt()
    {
        var projectId = Guid.NewGuid();

        QgisBridgeSelection.Set("A-B");

        Assert.Equal("A-B", QgisBridgeSelection.CurrentFor(projectId));
    }

    [Fact]
    public void Set_ignoriert_leere_werte_und_bleibt_sticky()
    {
        var projectId = Guid.NewGuid();
        QgisBridgeSelection.Set("A-B");
        _ = QgisBridgeSelection.CurrentFor(projectId);

        QgisBridgeSelection.Set(null);
        QgisBridgeSelection.Set("   ");

        Assert.Equal("A-B", QgisBridgeSelection.CurrentFor(projectId));
    }

    [Fact]
    public void Auswahl_vor_erstem_abruf_ueberlebt_die_projektzuordnung()
    {
        // Reihenfolge beim App-Start: Nutzer waehlt Haltung, erst danach fragt QGIS ab.
        QgisBridgeSelection.Set("A-B");

        Assert.Equal("A-B", QgisBridgeSelection.CurrentFor(Guid.NewGuid()));
    }

    [Fact]
    public void Jeder_klick_erhoeht_den_stempel_auch_bei_gleicher_haltung()
    {
        var before = QgisBridgeSelection.Stamp;

        QgisBridgeSelection.Set("A-B");
        QgisBridgeSelection.Set("A-B");

        // Zwei Klicks = zwei Stempel: QGIS zoomt so auch beim erneuten Klick
        // auf dieselbe Haltung wieder hin.
        Assert.Equal(before + 2, QgisBridgeSelection.Stamp);
    }

    [Fact]
    public void Projektwechsel_verwirft_die_gemerkte_auswahl()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        QgisBridgeSelection.Set("A-B");
        Assert.Equal("A-B", QgisBridgeSelection.CurrentFor(first));

        Assert.Equal("", QgisBridgeSelection.CurrentFor(second));

        QgisBridgeSelection.Set("C-D");
        Assert.Equal("C-D", QgisBridgeSelection.CurrentFor(second));
    }
}
