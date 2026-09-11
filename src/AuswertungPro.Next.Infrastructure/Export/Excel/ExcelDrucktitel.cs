using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>
/// ClosedXML schreibt Wiederholungsbereiche ohne absolute Bezüge. LibreOffice
/// ignoriert diese beim Druck. Normalisiert ausschliesslich die Drucktitel in
/// der noch unveroeffentlichten Exportdatei; keine Zell- oder Formelaenderung.
/// </summary>
internal static class ExcelDrucktitel
{
    public static void Normalisieren(string temporaereDatei)
    {
        using var zip = ZipFile.Open(temporaereDatei, ZipArchiveMode.Update);
        var eintrag = zip.GetEntry("xl/workbook.xml")
            ?? throw new InvalidDataException("Die Excel-Arbeitsmappe fehlt.");
        XDocument dokument;
        using (var stream = eintrag.Open())
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings
               { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            dokument = XDocument.Load(reader);

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var geaendert = false;
        foreach (var name in dokument.Descendants(ns + "definedName"))
        {
            if ((string?)name.Attribute("name") != "_xlnm.Print_Titles")
                continue;
            var absolut = Regex.Replace(name.Value,
                @"(?<=[!:])\$?([A-Za-z]+|\d+)(?=[:,]|$)",
                m => "$" + m.Groups[1].Value);
            if (absolut == name.Value) continue;
            name.Value = absolut;
            geaendert = true;
        }
        if (!geaendert) return;
        eintrag.Delete();
        using var ziel = zip.CreateEntry("xl/workbook.xml", CompressionLevel.Optimal).Open();
        dokument.Save(ziel);
    }
}
