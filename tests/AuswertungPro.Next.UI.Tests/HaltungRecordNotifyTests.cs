using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die Zeilen-Aktualisierung nach einer Feldaenderung laeuft ueber PropertyChanged
/// (nameof(Fields)) — NICHT ueber einen Collection-Replace. Nur so bleiben Scroll-Position
/// und Auswahl in der virtualisierten Haltungsliste erhalten.
/// </summary>
public sealed class HaltungRecordNotifyTests
{
    [Fact]
    public void RaiseAllFieldsChanged_meldet_Fields()
    {
        var record = new HaltungRecord();
        var meldungen = new List<string?>();
        record.PropertyChanged += (_, e) => meldungen.Add(e.PropertyName);

        record.RaiseAllFieldsChanged();

        Assert.Contains(nameof(HaltungRecord.Fields), meldungen);
    }

    [Fact]
    public void SetFieldValue_meldet_Fields_ohne_Referenzwechsel()
    {
        var record = new HaltungRecord();
        var fieldsRef = record.Fields;
        var meldungen = new List<string?>();
        record.PropertyChanged += (_, e) => meldungen.Add(e.PropertyName);

        record.SetFieldValue("Zustandsklasse", "3", FieldSource.Manual, userEdited: true);

        Assert.Contains(nameof(HaltungRecord.Fields), meldungen);
        // Gleiche Dictionary-Instanz -> die Zeile wird an Ort und Stelle aktualisiert,
        // nicht durch einen Neuaufbau der Auflistung.
        Assert.Same(fieldsRef, record.Fields);
        Assert.Equal("3", record.GetFieldValue("Zustandsklasse"));
    }
}
