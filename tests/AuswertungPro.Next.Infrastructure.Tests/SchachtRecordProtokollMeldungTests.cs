using System.ComponentModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Fix-Runde 2 (Task 14, Nova-Etappe 2): Ein In-Place-Ersatz des Protokolls (zum Beispiel
/// "Aktualisieren" liest das Protokoll neu ein) liess <c>Record</c> referenzgleich, und die reine
/// Auto-Property meldete den Wechsel nirgends. Abonnenten wie die Schachtansicht sahen die neue
/// Schadensliste deshalb erst nach einem erneuten Binden. Der Setter meldet sich jetzt wie ein
/// Feldwert.
/// </summary>
public sealed class SchachtRecordProtokollMeldungTests
{
    [Fact]
    public void Setzen_von_Protocol_loest_PropertyChanged_mit_dem_Namen_Protocol_aus()
    {
        var record = new SchachtRecord();
        var gemeldeteNamen = new List<string?>();
        record.PropertyChanged += (_, e) => gemeldeteNamen.Add(e.PropertyName);

        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision { Entries = { new ProtocolEntry { Code = "BAB" } } }
        };

        Assert.Contains(nameof(SchachtRecord.Protocol), gemeldeteNamen);
    }

    [Fact]
    public void Ein_Ersatz_auf_null_meldet_sich_ebenfalls()
    {
        var record = new SchachtRecord { Protocol = new ProtocolDocument() };
        var gemeldet = false;
        record.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SchachtRecord.Protocol))
                gemeldet = true;
        };

        record.Protocol = null;

        Assert.True(gemeldet);
        Assert.Null(record.Protocol);
    }

    [Fact]
    public void Ohne_Abonnenten_bleibt_das_Setzen_folgenlos()
    {
        // Wie bei der JSON-Deserialisierung: der Setter wird ganz normal aufgerufen,
        // aber PropertyChanged hat noch keinen Zuhoerer und darf nicht werfen.
        var record = new SchachtRecord();

        var dokument = new ProtocolDocument();
        record.Protocol = dokument;

        Assert.Same(dokument, record.Protocol);
    }
}
