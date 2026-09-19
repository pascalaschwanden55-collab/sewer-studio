using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Auditbefund 13 (18.09.2026): Beim Umbenennen einer Haltung zog der Dienst nur
/// `FotoPaths` nach. `OriginalFotoPaths` (die unveraenderte Quelle je Fotoslot) und
/// `ProtocolRevision.ImportVideoPaths` zeigten danach auf die verschobenen alten
/// Dateien — die historischen Belege waren nicht mehr erreichbar.
///
/// Fremde Pfade ausserhalb des Projekts duerfen dabei unveraendert bleiben.
/// </summary>
public sealed class HoldingRenameMedienverweiseTests
{
    [Fact]
    public void Umbenennen_ZiehtOriginalfotosUndRevisionsvideosMit()
    {
        var root = NeuerOrdner();
        try
        {
            var alt = "100-200";
            var neu = "100-201";
            var altOrdner = Path.Combine(root, "Haltungen_Verteilt", alt);
            Directory.CreateDirectory(Path.Combine(altOrdner, "Fotos"));
            var aktuellesFoto = Path.Combine(altOrdner, "Fotos", "f1.jpg");
            var originalFoto = Path.Combine(altOrdner, "Fotos", "f1_original.jpg");
            var video = Path.Combine(altOrdner, "20260918_100-200.mp4");
            File.WriteAllText(aktuellesFoto, "a");
            File.WriteAllText(originalFoto, "o");
            File.WriteAllText(video, "v");

            var projektDatei = Path.Combine(root, "Projektdateien", "projekt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(projektDatei)!);
            File.WriteAllText(projektDatei, "{}");

            var record = new HaltungRecord();
            record.SetFieldValue(FieldKeys.HoldingName, alt, FieldSource.Xtf, userEdited: false);
            record.SetFieldValue(FieldKeys.Link, video, FieldSource.Xtf, userEdited: false);

            var eintrag = new ProtocolEntry();
            eintrag.FotoPaths.Add(aktuellesFoto);
            eintrag.OriginalFotoPaths.Add(originalFoto);
            var revision = new ProtocolRevision
            {
                ImportVideoPaths = new List<string> { video, @"D:\Fremd\ausserhalb.mp4" }
            };
            revision.Entries.Add(eintrag);
            record.Protocol = new ProtocolDocument { HaltungId = alt, Current = revision };

            var ergebnis = HoldingRenameService.Rename(record, alt, neu, projektDatei);
            Assert.True(ergebnis.Success, ergebnis.ErrorMessage);

            Assert.DoesNotContain(alt, eintrag.OriginalFotoPaths[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains(neu, eintrag.OriginalFotoPaths[0], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(alt, revision.ImportVideoPaths![0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains(neu, revision.ImportVideoPaths[0], StringComparison.OrdinalIgnoreCase);
            // Fremde Quelle ausserhalb des Projekts bleibt unangetastet.
            Assert.Equal(@"D:\Fremd\ausserhalb.mp4", revision.ImportVideoPaths[1]);
        }
        finally { Loeschen(root); }
    }

    private static string NeuerOrdner()
    {
        var d = Path.Combine(Path.GetTempPath(), $"rename-medien-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }

    private static void Loeschen(string d)
    {
        try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { }
    }
}
