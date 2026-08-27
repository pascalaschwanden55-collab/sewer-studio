using AuswertungPro.Next.UI.Dossiers;
using AuswertungPro.Next.UI.QgisBridge;
using AuswertungPro.Next.UI.ViewModels.Pages;
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
    public void Dossier_Haltungszeile_meldet_die_sichtbare_Haltung_an_Qgis()
    {
        var projectId = Guid.NewGuid();
        var row = new DossierHoldingRow(
            Guid.NewGuid(), "  77467-77463  ", "3.60 m", "Z2", "", "");

        DossierQgisSelectionReporter.Report(row);

        Assert.Equal("77467-77463", QgisBridgeSelection.CurrentFor(projectId));
    }

    [Fact]
    public void Dossier_Schachtzeile_meldet_den_sichtbaren_Schacht_an_Qgis()
    {
        var projectId = Guid.NewGuid();
        var row = new DossierShaftRow(Guid.NewGuid(), "  77467  ", "Kontrollschacht", "", "");

        DossierQgisSelectionReporter.Report(row);

        Assert.Equal("77467", QgisBridgeSelection.CurrentSchachtFor(projectId));
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

    [Fact]
    public void SetSchacht_merkt_sich_schacht_unabhaengig_von_der_haltung()
    {
        var projectId = Guid.NewGuid();

        QgisBridgeSelection.Set("A-B");
        QgisBridgeSelection.SetSchacht("KS 60191");

        // Beide Auswahlen bestehen nebeneinander (getrennte Kanaele).
        Assert.Equal("A-B", QgisBridgeSelection.CurrentFor(projectId));
        Assert.Equal("KS 60191", QgisBridgeSelection.CurrentSchachtFor(projectId));
    }

    [Fact]
    public void SchachtStempel_ist_getrennt_vom_haltungsstempel()
    {
        var beforeSchacht = QgisBridgeSelection.SchachtStamp;
        var beforeHolding = QgisBridgeSelection.Stamp;

        QgisBridgeSelection.SetSchacht("KS 1");
        QgisBridgeSelection.SetSchacht("KS 1");

        Assert.Equal(beforeSchacht + 2, QgisBridgeSelection.SchachtStamp);
        // Haltungsstempel bleibt unberuehrt.
        Assert.Equal(beforeHolding, QgisBridgeSelection.Stamp);
    }

    [Fact]
    public void SetSchacht_ignoriert_leere_werte_und_bleibt_sticky()
    {
        var projectId = Guid.NewGuid();
        QgisBridgeSelection.SetSchacht("KS 1");
        _ = QgisBridgeSelection.CurrentSchachtFor(projectId);

        QgisBridgeSelection.SetSchacht(null);
        QgisBridgeSelection.SetSchacht("  ");

        Assert.Equal("KS 1", QgisBridgeSelection.CurrentSchachtFor(projectId));
    }

    [Fact]
    public void SchachtSelectionChanged_feuert_bei_jeder_auswahl()
    {
        var fired = 0;
        void Handler() => fired++;
        QgisBridgeSelection.SchachtSelectionChanged += Handler;
        try
        {
            QgisBridgeSelection.SetSchacht("KS 1");
            QgisBridgeSelection.SetSchacht("KS 2");
            QgisBridgeSelection.SetSchacht("");   // ignoriert -> kein Event
        }
        finally
        {
            QgisBridgeSelection.SchachtSelectionChanged -= Handler;
        }

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Projektwechsel_verwirft_haltung_und_schacht_gemeinsam()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        QgisBridgeSelection.Set("A-B");
        QgisBridgeSelection.SetSchacht("KS 1");
        Assert.Equal("KS 1", QgisBridgeSelection.CurrentSchachtFor(first));

        // Wechsel auf ein anderes Projekt leert BEIDE Auswahlen.
        Assert.Equal("", QgisBridgeSelection.CurrentFor(second));
        Assert.Equal("", QgisBridgeSelection.CurrentSchachtFor(second));
    }
}
