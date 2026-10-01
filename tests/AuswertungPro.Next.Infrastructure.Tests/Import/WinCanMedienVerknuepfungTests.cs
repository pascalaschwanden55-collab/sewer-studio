using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 4: Die Medienverknuepfung des WinCan-Wegs.
///
/// Zwei Fehler von Stand c1021e76e:
/// 1. Zwei bytegleiche Kopien galten als "mehrere Kandidaten" — es wurde GAR KEIN
///    Video verlinkt (Andermatt Zone 2.11, Haltung 327015-2414).
/// 2. Jeder nicht leere Link verhinderte die Ersatzsuche — auch ein toter.
/// </summary>
public sealed class WinCanMedienVerknuepfungTests
{
    [Fact]
    public void ZweiBytegleicheKopien_WerdenVerlinkt()
    {
        MitOrdner(ordner =>
        {
            Schreibe(ordner, "Video/Sec/327015-2414_0004.mpg", "dieselbe Aufnahme");
            Schreibe(ordner, "Misc/Exchange/327015-2414_0004.mpg", "dieselbe Aufnahme");

            var (projekt, record) = ProjektMitHaltung("327015-2414");
            var meldungen = Verknuepfe(ordner, projekt);

            Assert.False(string.IsNullOrWhiteSpace(record.GetFieldValue("Link")),
                string.Join("\n", meldungen));
            Assert.Contains(meldungen, m => m.Contains("bytegleiche Kopien", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ZweiVerschiedeneVideos_BleibenUnverlinktUndWerdenGemeldet()
    {
        MitOrdner(ordner =>
        {
            Schreibe(ordner, "Video/Sec/100-200_0001.mpg", "Hinfahrt---");
            Schreibe(ordner, "Misc/Exchange/100-200_0002.mpg", "Rueckfahrt-");

            var (projekt, record) = ProjektMitHaltung("100-200");
            var meldungen = Verknuepfe(ordner, projekt);

            Assert.True(string.IsNullOrWhiteSpace(record.GetFieldValue("Link")));
            Assert.Contains(meldungen,
                m => m.Contains("verschiedenen Inhalten", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ToterAbsoluterLink_WirdNeuAufgeloest()
    {
        MitOrdner(ordner =>
        {
            var echt = Schreibe(ordner, "Video/Sec/100-200_0001.mpg", "Aufnahme");

            var (projekt, record) = ProjektMitHaltung("100-200");
            record.SetFieldValue("Link", Path.Combine(ordner, "gibt-es-nicht", "100-200.mpg"), FieldSource.Legacy, userEdited: false);

            var meldungen = Verknuepfe(ordner, projekt);

            Assert.Equal(echt, record.GetFieldValue("Link"));
            Assert.Contains(meldungen, m => m.Contains("war tot", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ToterLinkOhneErsatz_BleibtStehenUndWirdGemeldet()
    {
        MitOrdner(ordner =>
        {
            var (projekt, record) = ProjektMitHaltung("100-200");
            var toterPfad = Path.Combine(ordner, "gibt-es-nicht", "100-200.mpg");
            record.SetFieldValue("Link", toterPfad, FieldSource.Legacy, userEdited: false);

            var meldungen = Verknuepfe(ordner, projekt);

            // Nichts erfinden und nichts wegwerfen: Der alte Wert bleibt sichtbar.
            Assert.Equal(toterPfad, record.GetFieldValue("Link"));
            Assert.Contains(meldungen, m => m.Contains("kein Ersatz", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void GueltigerLink_WirdNichtAngefasst()
    {
        MitOrdner(ordner =>
        {
            var vorhanden = Schreibe(ordner, "Video/Sec/100-200_0001.mpg", "Aufnahme");
            Schreibe(ordner, "Misc/Exchange/100-200_0002.mpg", "andere Aufnahme");

            var (projekt, record) = ProjektMitHaltung("100-200");
            record.SetFieldValue("Link", vorhanden, FieldSource.Legacy, userEdited: false);

            Verknuepfe(ordner, projekt);

            Assert.Equal(vorhanden, record.GetFieldValue("Link"));
        });
    }

    [Fact]
    public void ProjektinternerRelativerLink_BleibtUnangetastet()
    {
        // Ein relativer Link zeigt ins ZIELPROJEKT. Ihn hier durch einen Quellpfad zu
        // ersetzen, wuerde die fertige Verteilung zerstoeren.
        MitOrdner(ordner =>
        {
            Schreibe(ordner, "Video/Sec/100-200_0001.mpg", "Aufnahme");

            var (projekt, record) = ProjektMitHaltung("100-200");
            record.SetFieldValue("Link", @"Haltungen_Verteilt\100-200\20200702_100-200.mpg", FieldSource.Legacy, userEdited: false);

            Verknuepfe(ordner, projekt);

            Assert.Equal(@"Haltungen_Verteilt\100-200\20200702_100-200.mpg", record.GetFieldValue("Link"));
        });
    }

    // ---------------------------------------------------------------------

    private static List<string> Verknuepfe(string ordner, Project projekt)
    {
        var dateien = Directory.EnumerateFiles(ordner, "*", SearchOption.AllDirectories).ToList();
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var datei in dateien)
        {
            var name = Path.GetFileName(datei);
            if (!index.TryGetValue(name, out var liste))
                index[name] = liste = new List<string>();
            liste.Add(datei);
        }

        var meldungen = new List<string>();
        new WinCanDbImportService().LinkMediaFromFileIndex(projekt, index, meldungen);
        return meldungen;
    }

    private static (Project, HaltungRecord) ProjektMitHaltung(string name)
    {
        var projekt = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Legacy, userEdited: false);
        projekt.Data.Add(record);
        return (projekt, record);
    }

    private static void MitOrdner(Action<string> pruefung)
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"ap4-wincan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ordner);
        try { pruefung(ordner); }
        finally { try { Directory.Delete(ordner, recursive: true); } catch { } }
    }

    private static string Schreibe(string ordner, string relativerPfad, string inhalt)
    {
        var voll = Path.Combine(ordner, relativerPfad.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
        File.WriteAllText(voll, inhalt, new UTF8Encoding(false));
        return voll;
    }
}
