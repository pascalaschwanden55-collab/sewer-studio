using System;
using System.IO;
using System.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class DichtheitImportDistributorConcurrencyTests
{
    [Fact]
    public void Distribute_KiAwaitMitOberflaechenKontext_BlockiertNicht()
    {
        var root = Path.Combine(Path.GetTempPath(), $"SewerStudio-DpKi-{Guid.NewGuid():N}");
        var sourceFolder = Path.Combine(root, "Quelle", "DP");
        var projectFolder = Path.Combine(root, "Projekt");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectFolder);
        WritePdf(Path.Combine(sourceFolder, "unklar.pdf"), "Unbekanntes Dokument");

        try
        {
            var ki = new PdfKiSchiedsrichter(async (_, _) =>
            {
                await System.Threading.Tasks.Task.Yield();
                return """{ "typ": "Dichtheitspruefung", "schacht_von": "100", "schacht_bis": "200", "datum": "13.07.2026" }""";
            });
            DichtheitImportDistributor.Result? result = null;
            Exception? error = null;

            var thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
                try
                {
                    result = DichtheitImportDistributor.Distribute(
                        new Project(),
                        projectFolder,
                        Path.Combine(root, "Quelle"),
                        ki);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            })
            {
                IsBackground = true
            };

            thread.Start();

            Assert.True(
                thread.Join(TimeSpan.FromSeconds(5)),
                "Der Dichtheitsimport blockiert beim asynchronen KI-Aufruf.");
            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(1, result!.Verteilt);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
            }
        }
    }

    private static void WritePdf(string path, params string[] lines)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = 780m;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(40, y), font);
            y -= 18;
        }

        File.WriteAllBytes(path, builder.Build());
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // Simuliert einen blockierten UI-Kontext: Fortsetzungen laufen erst,
            // wenn der Aufrufer den Thread wieder freigibt.
        }
    }
}
