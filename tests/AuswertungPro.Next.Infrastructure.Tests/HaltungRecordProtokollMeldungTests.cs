using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 3: Die Statusspalten KI und Pruefung der Haltungstabelle lesen
/// <see cref="HaltungZeilenStatus"/>, und das haengt am Protokoll. Ein In-Place-Ersatz des
/// Protokolls (neu eingelesenes Protokoll, KI-Lauf) laesst den Datensatz referenzgleich; ohne
/// Meldung bliebe die Ampel auf dem alten Stand stehen. Gleiche Regel wie bei
/// <see cref="SchachtRecord"/> (siehe SchachtRecordProtokollMeldungTests).
/// </summary>
public sealed class HaltungRecordProtokollMeldungTests
{
    [Fact]
    public void Setzen_von_Protocol_loest_PropertyChanged_mit_dem_Namen_Protocol_aus()
    {
        var record = new HaltungRecord();
        var gemeldeteNamen = new List<string?>();
        record.PropertyChanged += (_, e) => gemeldeteNamen.Add(e.PropertyName);

        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision { Entries = { new ProtocolEntry { Code = "BAB" } } }
        };

        Assert.Contains(nameof(HaltungRecord.Protocol), gemeldeteNamen);
    }

    [Fact]
    public void Ein_Ersatz_auf_null_meldet_sich_ebenfalls()
    {
        var record = new HaltungRecord { Protocol = new ProtocolDocument() };
        var gemeldet = false;
        record.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HaltungRecord.Protocol))
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
        var record = new HaltungRecord();

        var dokument = new ProtocolDocument();
        record.Protocol = dokument;

        Assert.Same(dokument, record.Protocol);
    }
}
