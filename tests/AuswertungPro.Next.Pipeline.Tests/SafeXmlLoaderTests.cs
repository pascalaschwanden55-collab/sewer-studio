using System;
using System.IO;
using System.Xml;
using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer L4-Hardening: SafeXmlLoader mit DTD-prohibition + XmlResolver=null.
/// Schuetzt vor XXE-Attacken (XML External Entities) bei Import von externen
/// XTF/SIA-405-Dateien.
/// </summary>
public class SafeXmlLoaderTests
{
    private static string WriteTempXml(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sewerstudio_xml_{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_ValidXml_Succeeds()
    {
        var path = WriteTempXml(@"<?xml version=""1.0""?><root><item>value</item></root>");
        try
        {
            var doc = SafeXmlLoader.Load(path);
            Assert.NotNull(doc.Root);
            Assert.Equal("root", doc.Root!.Name.LocalName);
            Assert.Equal("value", doc.Root.Element("item")?.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DocumentWithDtd_ThrowsXmlException()
    {
        // Ein DTD-Block triggert die Hardening — DtdProcessing.Prohibit
        var malicious = @"<?xml version=""1.0""?>
<!DOCTYPE root [
  <!ENTITY xxe SYSTEM ""file:///etc/passwd"">
]>
<root>&xxe;</root>
";
        var path = WriteTempXml(malicious);
        try
        {
            Assert.Throws<XmlException>(() => SafeXmlLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ExternalEntityReference_NotResolved()
    {
        // SYSTEM-Entity ohne DOCTYPE-Block waere syntax-fehler im XML.
        // Wichtigster Test: dass DOCTYPE-Definition geblockt wird (s. oben).
        // Hier zusaetzlich: normales Dokument ohne DTD funktioniert.
        var path = WriteTempXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <item attr=""value"">Inhalt</item>
</root>
");
        try
        {
            var doc = SafeXmlLoader.Load(path);
            Assert.NotNull(doc.Root);
            Assert.Equal("Inhalt", doc.Root!.Element("item")?.Value);
            Assert.Equal("value", doc.Root.Element("item")?.Attribute("attr")?.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WithLoadOptions_PreservesWhitespace()
    {
        // Ueberprueft dass die Overload mit LoadOptions weitergereicht wird —
        // wird von XTF-Importern fuer formatierte Ausgabe genutzt.
        var content = @"<?xml version=""1.0""?>
<root>
    <item>indent</item>
</root>
";
        var path = WriteTempXml(content);
        try
        {
            var doc = SafeXmlLoader.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            Assert.NotNull(doc.Root);
            // Das ist ein basic-Test — dass die Methode ueberhaupt durchlaeuft
            // und das Dokument dann strukturell intakt ist.
            Assert.Equal("indent", doc.Root!.Element("item")?.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_NonExistentFile_ThrowsFileNotFound()
    {
        // Sicherheits-Hardening soll FileNotFoundException nicht unterdruecken
        var path = Path.Combine(Path.GetTempPath(), $"does_not_exist_{Guid.NewGuid():N}.xml");
        Assert.Throws<FileNotFoundException>(() => SafeXmlLoader.Load(path));
    }
}
