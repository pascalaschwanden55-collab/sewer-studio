using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Verteilte Protokolle muessen wie in der Verteilung benannt sein:
/// "JJJJMMTT_&lt;Haltung&gt;.pdf" bzw. "JJJJMMTT_&lt;Schacht&gt;.pdf" - dasselbe
/// Schema wie das Video daneben. Der Herstellername ("Section_20_...") darf nicht
/// stehen bleiben.
/// </summary>
public sealed class NameBasedProtocolDistributorBenennungTests : IDisposable
{
    private readonly string _projektOrdner = NeuerOrdner();
    private readonly string _quelle = NeuerOrdner();

    private static string NeuerOrdner()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nbpd_name_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static HaltungRecord Haltung(string name, string? datum = null)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
        if (datum is not null)
            r.SetFieldValue("Datum_Jahr", datum, FieldSource.Manual, false);
        return r;
    }

    private static string[] VerteilteDateien(string projektOrdner, string unterordner)
        => Directory.Exists(Path.Combine(projektOrdner, unterordner))
            ? Directory.EnumerateFiles(Path.Combine(projektOrdner, unterordner), "*.pdf", SearchOption.AllDirectories)
                .Select(pfad => Path.GetFileName(pfad)).OrderBy(name => name, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    [Fact]
    public void Haltungsprotokoll_TraegtDatumUndHaltungsnamen()
    {
        File.WriteAllText(Path.Combine(_quelle, "Section_20_892033-10.892858.pdf"), "x");

        var project = new Project();
        project.Data.Add(Haltung("892033-10.892858", "28.10.2024"));

        new NameBasedProtocolDistributor().Distribute(project, _projektOrdner, _quelle);

        Assert.Equal(
            new[] { "20241028_892033-10.892858.pdf" },
            VerteilteDateien(_projektOrdner, "Haltungen_Verteilt"));
    }

    [Fact]
    public void Schachtprotokoll_TraegtAusfuehrungsdatumUndSchachtnummer()
    {
        File.WriteAllText(Path.Combine(_quelle, "80707.pdf"), "x");

        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "80707");
        schacht.SetFieldValue("Ausführung Datum/Jahr", "28.10.2024");
        project.SchaechteData.Add(schacht);

        new NameBasedProtocolDistributor().Distribute(project, _projektOrdner, _quelle);

        Assert.Equal(
            new[] { "20241028_80707.pdf" },
            VerteilteDateien(_projektOrdner, "Schächte_Verteilt"));
    }

    [Fact]
    public void SchachtBaujahr_LandetNichtImDateinamen()
    {
        // "Datum/Jahr" ist beim Schacht das BAUJAHR (OBJ_ConstructionDate), nicht das
        // Pruefdatum. Ein Dateiname "19980101_80707.pdf" waere schlicht falsch.
        File.WriteAllText(Path.Combine(_quelle, "80707.pdf"), "x");

        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "80707");
        schacht.SetFieldValue("Datum/Jahr", "05.05.1998");
        project.SchaechteData.Add(schacht);

        new NameBasedProtocolDistributor().Distribute(project, _projektOrdner, _quelle);

        Assert.Equal(
            new[] { "00000000_80707.pdf" },
            VerteilteDateien(_projektOrdner, "Schächte_Verteilt"));
    }

    [Fact]
    public void OhneDatum_WirdNullstempelVerwendet()
    {
        // Gleiche Regel wie in der Verteilung: lieber "00000000" als ein erfundenes Datum.
        File.WriteAllText(Path.Combine(_quelle, "Section_20_892033-10.892858.pdf"), "x");

        var project = new Project();
        project.Data.Add(Haltung("892033-10.892858"));

        new NameBasedProtocolDistributor().Distribute(project, _projektOrdner, _quelle);

        Assert.Equal(
            new[] { "00000000_892033-10.892858.pdf" },
            VerteilteDateien(_projektOrdner, "Haltungen_Verteilt"));
    }

    [Fact]
    public void ZweitesAbweichendesProtokoll_GehtNichtVerloren()
    {
        File.WriteAllText(Path.Combine(_quelle, "Section_20_892033-10.892858.pdf"), "erstes");
        File.WriteAllText(Path.Combine(_quelle, "Nachtrag_892033-10.892858.pdf"), "zweites - anderer inhalt");

        var project = new Project();
        project.Data.Add(Haltung("892033-10.892858", "28.10.2024"));

        new NameBasedProtocolDistributor().Distribute(project, _projektOrdner, _quelle);

        var dateien = VerteilteDateien(_projektOrdner, "Haltungen_Verteilt");
        Assert.Equal(2, dateien.Length);
        Assert.Contains("20241028_892033-10.892858.pdf", dateien);
    }

    public void Dispose()
    {
        try { Directory.Delete(_projektOrdner, true); } catch { }
        try { Directory.Delete(_quelle, true); } catch { }
    }
}
