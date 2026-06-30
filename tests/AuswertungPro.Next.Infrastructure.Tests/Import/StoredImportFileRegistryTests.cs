using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer StoredImportFileRegistry.
/// Sichert das Serialisierungs-/Deserialisierungsverhalten der Dateilisten.
/// </summary>
public class StoredImportFileRegistryTests
{
    [Fact]
    public void Load_LeereMetadata_GibtLeereListeZurueck()
    {
        var metadata = new Dictionary<string, string>();
        var result = StoredImportFileRegistry.Load(metadata, "XTF_StoredFiles");
        Assert.Empty(result);
    }

    [Fact]
    public void Load_FehlendeSchluessel_GibtLeereListeZurueck()
    {
        var metadata = new Dictionary<string, string> { ["AndererKey"] = "wert" };
        var result = StoredImportFileRegistry.Load(metadata, "XTF_StoredFiles");
        Assert.Empty(result);
    }

    [Fact]
    public void Load_GueltigesJson_GibtListeZurueck()
    {
        var metadata = new Dictionary<string, string>
        {
            ["XTF_StoredFiles"] = "[\"Imports/XTF/a.xtf\",\"Imports/XTF/b.xtf\"]"
        };
        var result = StoredImportFileRegistry.Load(metadata, "XTF_StoredFiles");
        Assert.Equal(2, result.Count);
        Assert.Contains("Imports/XTF/a.xtf", result);
        Assert.Contains("Imports/XTF/b.xtf", result);
    }

    [Fact]
    public void Load_LegacySemikolonFormat_GibtListeZurueck()
    {
        // Aelteres Format: Semikolon-getrennte Pfade (kein gueltiges JSON)
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = "Imports/PDF/a.pdf;Imports/PDF/b.pdf"
        };
        var result = StoredImportFileRegistry.Load(metadata, "PDF_StoredFiles");
        Assert.Equal(2, result.Count);
        Assert.Contains("Imports/PDF/a.pdf", result);
    }

    [Fact]
    public void Load_WhitespaceWirdGetrimmt()
    {
        var metadata = new Dictionary<string, string>
        {
            ["TXT_StoredFiles"] = "[\"  Imports/TXT/a.txt  \"]"
        };
        var result = StoredImportFileRegistry.Load(metadata, "TXT_StoredFiles");
        Assert.Single(result);
        Assert.Equal("Imports/TXT/a.txt", result[0]);
    }

    [Fact]
    public void Save_SpeichertPfadeAlsJson()
    {
        var metadata = new Dictionary<string, string>();
        StoredImportFileRegistry.Save(metadata, "XTF_StoredFiles", new[] { "Imports/XTF/c.xtf" });
        Assert.True(metadata.ContainsKey("XTF_StoredFiles"));
        Assert.Contains("Imports/XTF/c.xtf", metadata["XTF_StoredFiles"]);
    }

    [Fact]
    public void Save_FuegtNeuePfadeHinzu_KeineDopplungen()
    {
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = "[\"Imports/PDF/a.pdf\"]"
        };
        // Einmal vorhandenen + einmal neuen Pfad hinzufuegen
        StoredImportFileRegistry.Save(metadata, "PDF_StoredFiles",
            new[] { "Imports/PDF/a.pdf", "Imports/PDF/b.pdf" });

        var loaded = StoredImportFileRegistry.Load(metadata, "PDF_StoredFiles");
        Assert.Equal(2, loaded.Count);
        Assert.Single(loaded, p => p == "Imports/PDF/a.pdf");
        Assert.Single(loaded, p => p == "Imports/PDF/b.pdf");
    }

    [Fact]
    public void Save_DoppelungenCaseInsensitive_NichtDoppeltGespeichert()
    {
        var metadata = new Dictionary<string, string>
        {
            ["XTF_StoredFiles"] = "[\"Imports/XTF/Test.xtf\"]"
        };
        // Gross-/Kleinschreibung variiert
        StoredImportFileRegistry.Save(metadata, "XTF_StoredFiles",
            new[] { "Imports/XTF/test.xtf" });

        var loaded = StoredImportFileRegistry.Load(metadata, "XTF_StoredFiles");
        Assert.Single(loaded);
    }
}
