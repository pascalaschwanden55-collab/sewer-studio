using System;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Fixwelle 2b, Runde 2: Eine virtuelle Tabellenspalte (Praefix <c>Nova_</c>) ist kein
/// Feld und darf nie in <c>Fields</c> eines Datensatzes landen.
///
/// Anlass: Der Schacht-Rechtsklickpfad kannte den Schutz nicht und schrieb bei „Spalte
/// leeren" auf dem Kopf der Protokollspalte <c>Nova_Protokoll</c> in JEDEN Schachtdatensatz —
/// mit Handmarkierung, also dauerhaft geschuetzt, und in die gespeicherte Projektdatei
/// hinein. Die Oberflaeche faengt den Fall inzwischen ab; dies ist die zweite, tiefere
/// Sperre direkt am Datensatz.
/// </summary>
public sealed class VirtuelleSpalteSchutzTests
{
    [Theory]
    [InlineData("Nova_KI")]
    [InlineData("Nova_Pruefung")]
    [InlineData("Nova_Video")]
    [InlineData("Nova_Protokoll")]
    public void Eine_virtuelle_Statusspalte_laesst_sich_nicht_leeren_am_Schacht(string feld)
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "10001", FieldSource.Manual, userEdited: true);

        Assert.Throws<ArgumentException>(
            () => schacht.SetFieldValue(feld, string.Empty, FieldSource.Manual, userEdited: true));

        // Der Datensatz bleibt unveraendert: kein neuer Schluessel, keine Metadaten.
        Assert.False(schacht.Fields.ContainsKey(feld));
        Assert.False(schacht.FieldMeta.ContainsKey(feld));
        Assert.Equal("10001", schacht.GetFieldValue("Schachtnummer"));
    }

    [Theory]
    [InlineData("Nova_KI")]
    [InlineData("Nova_Protokoll")]
    public void Eine_virtuelle_Statusspalte_laesst_sich_nicht_leeren_an_der_Haltung(string feld)
    {
        var haltung = new HaltungRecord();
        haltung.SetFieldValue(FieldKeys.HoldingName, "10001-10002", FieldSource.Manual, userEdited: true);

        Assert.Throws<ArgumentException>(
            () => haltung.SetFieldValue(feld, string.Empty, FieldSource.Manual, userEdited: true));

        Assert.False(haltung.Fields.ContainsKey(feld));
        Assert.False(haltung.FieldMeta.ContainsKey(feld));
        Assert.Equal("10001-10002", haltung.GetFieldValue(FieldKeys.HoldingName));
    }

    /// <summary>Auch die uebrigen Schreibwege duerfen den Schluessel nicht durchlassen.</summary>
    [Fact]
    public void Auch_die_anderen_Schreibwege_weisen_den_Schluessel_ab()
    {
        var schacht = new SchachtRecord();
        Assert.Throws<ArgumentException>(() => schacht.SetFieldValue("Nova_Video", "x"));
        Assert.Throws<ArgumentException>(() => schacht.SetFieldValueTechnical("Nova_Video", "x"));
        Assert.Throws<ArgumentException>(() => schacht.FuelleLeeresFeld("Nova_Video", "x", FieldSource.Kataster));
        Assert.False(schacht.Fields.ContainsKey("Nova_Video"));

        // Ein HaltungRecord legt seine Katalogfelder beim Erzeugen an; geprueft wird deshalb
        // der eine Schluessel, nicht die leere Sammlung.
        var haltung = new HaltungRecord();
        Assert.Throws<ArgumentException>(() => haltung.FuelleLeeresFeld("Nova_Video", "x", FieldSource.Kataster));
        Assert.False(haltung.Fields.ContainsKey("Nova_Video"));
    }

    /// <summary>
    /// Der Schutz haengt am Praefix, nicht an einer Liste: Ein spaeter dazukommender
    /// virtueller Schluessel ist ohne weiteres Zutun gesperrt. Echte Felder bleiben frei —
    /// auch solche, die zufaellig „Nova" enthalten, aber nicht damit beginnen.
    /// </summary>
    [Theory]
    [InlineData("Nova_Irgendwas", true)]
    [InlineData("Nova_", true)]
    [InlineData("Novadorf", false)]
    [InlineData("Strasse_Nova_", false)]
    [InlineData("Strasse", false)]
    [InlineData(null, false)]
    public void Nur_das_Praefix_entscheidet(string? feld, bool virtuell)
        => Assert.Equal(virtuell, VirtuelleSpalte.IstVirtuell(feld));
}
