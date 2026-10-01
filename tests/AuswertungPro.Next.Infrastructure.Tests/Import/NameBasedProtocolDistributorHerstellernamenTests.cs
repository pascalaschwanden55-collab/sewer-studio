using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Herstellerexporte stellen dem Haltungsnamen etwas voran ("Section_8_892037-74091.pdf").
/// Diese Protokolle fielen frueher stillschweigend heraus: der Bericht meldete sie nicht
/// einmal als "nicht zugeordnet". Im Projekt Hellgasse blieben so alle 38 Haltungen ohne
/// ihr fertiges Protokoll.
/// </summary>
public sealed class NameBasedProtocolDistributorHerstellernamenTests : IDisposable
{
    private readonly string _projektOrdner = NeuerOrdner();
    private readonly string _quelle = NeuerOrdner();

    private static string NeuerOrdner()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nbpd_hersteller_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static HaltungRecord Haltung(string name)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
        return r;
    }

    [Fact]
    public void SectionPdfMitVorangestelltemText_LandetAnDerHaltung()
    {
        File.WriteAllText(Path.Combine(_quelle, "Section_8_892037-74091.pdf"), "x");

        var project = new Project();
        project.Data.Add(Haltung("892037-74091"));

        var bericht = new NameBasedProtocolDistributor()
            .Distribute(project, _projektOrdner, _quelle);

        Assert.Equal(1, bericht.HaltungProtokolle);
        Assert.False(string.IsNullOrWhiteSpace(project.Data[0].GetFieldValue("PDF_Path")));
        Assert.True(Directory.EnumerateFiles(
            Path.Combine(_projektOrdner, "Haltungen_Verteilt"), "*.pdf", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public void UnbekannteNummerImNamen_LegtKeineHaltungAn()
    {
        // Fail-closed: Aus einer fremden Zahl darf keine Geister-Haltung entstehen.
        File.WriteAllText(Path.Combine(_quelle, "Section_99_111111-222222.pdf"), "x");

        var project = new Project();
        project.Data.Add(Haltung("892037-74091"));

        var bericht = new NameBasedProtocolDistributor()
            .Distribute(project, _projektOrdner, _quelle);

        Assert.Equal(0, bericht.HaltungProtokolle);
        Assert.Single(project.Data);
        Assert.Empty(project.SchaechteData);
    }

    [Fact]
    public void UnzuordenbaresTvProtokoll_WirdGemeldetStattStillVerworfen()
    {
        // Inhalt weist die Datei als TV-Protokoll aus, der Name traegt aber keinen
        // bekannten Bezug. Das muss sichtbar werden.
        File.WriteAllText(
            Path.Combine(_quelle, "Bericht_ohne_nummer.pdf"),
            "Haltungsinspektion mit Leitungs-Stammdaten");

        var project = new Project();
        project.Data.Add(Haltung("892037-74091"));

        var bericht = new NameBasedProtocolDistributor()
            .Distribute(project, _projektOrdner, _quelle);

        Assert.Contains("Bericht_ohne_nummer.pdf", bericht.NichtZugeordnet);
    }

    public void Dispose()
    {
        try { Directory.Delete(_projektOrdner, true); } catch { }
        try { Directory.Delete(_quelle, true); } catch { }
    }
}
