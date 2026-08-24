using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Das Sicherungsexemplar der Dossierdatei.
///
/// Wurde die Hauptdatei unlesbar und der Stand aus dem .bak geladen, darf das
/// naechste Speichern nicht ausgerechnet dieses gute .bak mit der kaputten
/// Datei ueberschreiben — dann waere die letzte gute Fassung weg.
/// </summary>
public sealed class DossierStoreBackupTests
{
    private static string NeuerOrdner()
    {
        var pfad = Path.Combine(Path.GetTempPath(), "dossier_bak_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pfad);
        return pfad;
    }

    private static string DateiPfad(string root)
        => DossierFolderPlanner.ResolveDocumentPath(root);

    [Fact]
    public async Task Eine_kaputte_Hauptdatei_ersetzt_das_gute_Sicherungsexemplar_nicht()
    {
        var root = NeuerOrdner();
        try
        {
            var store = new DossierFileStore();

            // Ein guter Stand, damit ein .bak entsteht.
            var gut = new DossierDocument();
            gut.Dossiers.Add(new DossierDefinition { Name = "Musterweg 1" });
            await store.SaveAsync(root, gut);
            await store.SaveAsync(root, gut);

            var pfad = DateiPfad(root);
            var bak = pfad + ".bak";
            Assert.True(File.Exists(bak), "Ohne .bak sagt der Test nichts aus.");

            var gutesBackup = await File.ReadAllTextAsync(bak, Encoding.UTF8);

            // Die Hauptdatei geht kaputt; geladen wird aus dem .bak.
            await File.WriteAllTextAsync(pfad, "{ kaputt", Encoding.UTF8);
            var geladen = await store.LoadAsync(root);
            Assert.Single(geladen.Dossiers);

            // Und jetzt speichern.
            await store.SaveAsync(root, geladen);

            Assert.Equal(gutesBackup, await File.ReadAllTextAsync(bak, Encoding.UTF8));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Ein_guter_Stand_wird_weiterhin_gesichert()
    {
        var root = NeuerOrdner();
        try
        {
            var store = new DossierFileStore();

            var erst = new DossierDocument();
            erst.Dossiers.Add(new DossierDefinition { Name = "Erster" });
            await store.SaveAsync(root, erst);

            var zweit = new DossierDocument();
            zweit.Dossiers.Add(new DossierDefinition { Name = "Zweiter" });
            await store.SaveAsync(root, zweit);

            var bak = await File.ReadAllTextAsync(DateiPfad(root) + ".bak", Encoding.UTF8);
            Assert.Contains("Erster", bak, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
