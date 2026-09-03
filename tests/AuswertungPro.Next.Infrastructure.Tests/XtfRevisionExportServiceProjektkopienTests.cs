using System.IO;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Exportseite zeigt vor dem Start, welche Original-XTF die Revision verwenden wird.
/// Dafuer liefert der Dienst die Importkopien des Projekts — dieselbe Suche wie beim Schreiben.
/// </summary>
public sealed class XtfRevisionExportServiceProjektkopienTests
{
    [Fact]
    public void Findet_die_Importkopien_unter_Imports_XTF_neueste_zuerst()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "XtfKopien_" + Guid.NewGuid().ToString("N"));
        try
        {
            var projektDatei = Path.Combine(wurzel, "Projekt", "Projektdateien", "projekt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(projektDatei)!);
            File.WriteAllText(projektDatei, "{}");
            var kopien = Path.Combine(wurzel, "Projekt", "Imports", "XTF");
            Directory.CreateDirectory(kopien);
            var alt = Path.Combine(kopien, "alt.xtf");
            var neu = Path.Combine(kopien, "neu.xtf");
            File.WriteAllText(alt, "<xtf/>");
            File.WriteAllText(neu, "<xtf/>");
            File.SetLastWriteTime(alt, new DateTime(2024, 1, 5));
            File.SetLastWriteTime(neu, new DateTime(2025, 11, 18));

            IXtfRevisionExportService dienst = new XtfRevisionExportService();
            var ergebnis = dienst.FindeProjektkopien(projektDatei);

            Assert.Equal(2, ergebnis.Count);
            Assert.Equal(neu, ergebnis[0].Pfad);
            Assert.Equal(new DateTime(2025, 11, 18), ergebnis[0].GeaendertLokal);
            Assert.Equal(alt, ergebnis[1].Pfad);
        }
        finally
        {
            if (Directory.Exists(wurzel))
                Directory.Delete(wurzel, recursive: true);
        }
    }

    [Fact]
    public void Ohne_Projekt_oder_ohne_Ablage_ist_die_Liste_leer()
    {
        IXtfRevisionExportService dienst = new XtfRevisionExportService();

        Assert.Empty(dienst.FindeProjektkopien(null));
        Assert.Empty(dienst.FindeProjektkopien(Path.Combine(Path.GetTempPath(), "gibt-es-nicht_" + Guid.NewGuid().ToString("N"), "projekt.json")));
    }
}
