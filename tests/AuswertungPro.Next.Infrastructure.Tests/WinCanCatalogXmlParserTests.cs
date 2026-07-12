using System.Xml.Linq;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class WinCanCatalogXmlParserTests
{
    [Fact]
    public void Parse_ErhaeltVerknuepfungenListenUndSichtbareParameter()
    {
        var parsed = WinCanCatalogXmlParser.Parse(XElement.Parse(CreateCatalogXml()));

        var characterExtension = Assert.Single(parsed.CharacterExtensions).Value;
        Assert.Equal("BC-1", characterExtension.BaseCodeFK);
        Assert.Equal("BABAC;IGNORED", characterExtension.CloseCode);

        var link = Assert.Single(parsed.ParameterLinks);
        Assert.Equal("CE-1", link.CharExtFK);
        Assert.Equal(1.5, link.RangeFrom);
        Assert.Equal(4.5, link.RangeTo);

        var codeParameter = Assert.Single(WinCanCatalogXmlParser.BuildParameters(
            parsed.ParameterLinks,
            parsed.Parameters,
            parsed.ListValues));

        Assert.Equal("Ausprägung", codeParameter.Name);
        Assert.Equal("CHAR1", codeParameter.DataKey);
        Assert.Equal("enum", codeParameter.Type);
        Assert.True(codeParameter.Required);
        Assert.Equal(["Schmal", "Breit"], codeParameter.AllowedValues);
    }

    [Fact]
    public void Parse_BewahrtParameterReihenfolgeUndDedupliziertListenwerteOhneUmsortierung()
    {
        var root = XElement.Parse(CreateCatalogXml());
        var ns = root.Name.Namespace;
        root.Add(
            new XElement(ns + "PARAM",
                new XElement(ns + "PARAM_PK", "PARAM-2"),
                new XElement(ns + "PARAM_DataType", "DEC"),
                new XElement(ns + "PARAM_Placeholder", "@Tiefe"),
                new XElement(ns + "PARAM_Unit", "mm")),
            new XElement(ns + "PARAMX",
                new XElement(ns + "PX_CharExt_FK", "CE-1"),
                new XElement(ns + "PX_Param_FK", "PARAM-2"),
                new XElement(ns + "PX_Visible", "true"),
                new XElement(ns + "PX_Column_ID", "COL_ID_QUANT1")),
            new XElement(ns + "LIST",
                new XElement(ns + "LIST_Class_FK", "LC-1"),
                new XElement(ns + "LIST_Item", "schmal")));

        var parsed = WinCanCatalogXmlParser.Parse(root);
        var parameters = WinCanCatalogXmlParser.BuildParameters(
            parsed.ParameterLinks.Where(link => link.CharExtFK == "CE-1"),
            parsed.Parameters,
            parsed.ListValues);

        Assert.Collection(
            parameters,
            first =>
            {
                Assert.Equal("Auspr\u00E4gung", first.Name);
                Assert.Equal(["Schmal", "Breit"], first.AllowedValues);
            },
            second =>
            {
                Assert.Equal("Tiefe", second.Name);
                Assert.Equal("Q1", second.DataKey);
                Assert.Equal("number", second.Type);
                Assert.Equal("mm", second.Unit);
            });
    }

    [Fact]
    public void XmlProvider_BautCodesUndKategorienWieBisherAusWinCanKatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "SewerStudioWinCanCatalog", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var catalogPath = Path.Combine(root, "catalog.xml");

        try
        {
            File.WriteAllText(catalogPath, CreateCatalogXml());
            var provider = new XmlCodeCatalogProvider(catalogPath);

            Assert.True(provider.TryGet("BABAC", out var characterCode));
            Assert.Equal("Riss längs", characterCode.Title);
            Assert.Equal("Schäden / Risse", characterCode.Group);
            Assert.Equal(["Schäden", "Risse", "BAB", "BABA", "BABAC"], characterCode.CategoryPath);
            Assert.Equal("Sichtbare Beschreibung", characterCode.Description);

            var parameter = Assert.Single(characterCode.Parameters);
            Assert.Equal("Ausprägung", parameter.Name);
            Assert.Equal("enum", parameter.Type);
            Assert.Equal(["Schmal", "Breit"], parameter.AllowedValues);

            Assert.True(provider.TryGet("DAA", out var standaloneCode));
            Assert.Equal("Anschluss", standaloneCode.Title);
            Assert.Equal(["Schäden", "DAA"], standaloneCode.CategoryPath);
            Assert.False(provider.TryGet("VIRTUAL", out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void XmlProvider_FasstDoppelteCodesWieBisherZusammenUndWarnt()
    {
        var root = XElement.Parse(CreateCatalogXml());
        var ns = root.Name.Namespace;
        root.Add(
            new XElement(ns + "CHAREXT",
                new XElement(ns + "CE_PK", "CE-DUPLICATE"),
                new XElement(ns + "CE_BaseCode_FK", "BC-1"),
                new XElement(ns + "CE_Code", "BABAC-ALT"),
                new XElement(ns + "CE_ChildCaption", "Doppelter Riss"),
                new XElement(ns + "CE_CloseCode", "BABAC;ALTERNATIVE"),
                new XElement(ns + "CE_SortOrder", "99")));
        var directory = Path.Combine(
            Path.GetTempPath(),
            "SewerStudioWinCanCatalogDuplicate",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var catalogPath = Path.Combine(directory, "catalog.xml");

        try
        {
            File.WriteAllText(catalogPath, root.ToString(SaveOptions.DisableFormatting));
            var provider = new XmlCodeCatalogProvider(catalogPath);

            Assert.Single(provider.GetAll(), code => code.Code == "BABAC");
            Assert.Contains("Duplikat-Code 'BABAC' (2x)", provider.LastLoadWarnings);
            Assert.Equal(
                provider.GetAll().Select(code => code.Code).OrderBy(code => code, StringComparer.OrdinalIgnoreCase),
                provider.GetAll().Select(code => code.Code));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateCatalogXml()
        => """
           <WCCat xmlns="CDLAB.WinCan.WinCanCatalog_2011-04-04_2">
             <CLASS>
               <CLASS_PK>CLASS-1</CLASS_PK>
               <CLASS_Level>1</CLASS_Level>
               <CLASS_SortOrder>1</CLASS_SortOrder>
               <CLASS_ChildCaption>Schäden</CLASS_ChildCaption>
             </CLASS>
             <BASECODE>
               <BC_PK>BC-1</BC_PK>
               <BC_Class_FK>CLASS-1</BC_Class_FK>
               <BC_Code>BAB</BC_Code>
               <BC_ChildCaption>Risse</BC_ChildCaption>
               <BC_SortOrder>1</BC_SortOrder>
             </BASECODE>
             <BASECODE>
               <BC_PK>BC-2</BC_PK>
               <BC_Class_FK>CLASS-1</BC_Class_FK>
               <BC_Code>DAA</BC_Code>
               <BC_ChildCaption>Anschluss</BC_ChildCaption>
               <BC_CloseCode>DAA;IGNORED</BC_CloseCode>
               <BC_SortOrder>2</BC_SortOrder>
             </BASECODE>
             <BASECODE>
               <BC_PK>BC-3</BC_PK>
               <BC_ChildCaption>Virtuell</BC_ChildCaption>
               <BC_CloseCode>VIRTUAL</BC_CloseCode>
               <BC_IsVirtual>true</BC_IsVirtual>
             </BASECODE>
             <CHAREXT>
               <CE_PK>CE-1</CE_PK>
               <CE_BaseCode_FK>BC-1</CE_BaseCode_FK>
               <CE_Code>BABAC</CE_Code>
               <CE_ChildCaption>Riss längs</CE_ChildCaption>
               <CE_Remarks>Sichtbare Beschreibung</CE_Remarks>
               <CE_CloseCode>BABAC;IGNORED</CE_CloseCode>
               <CE_SortOrder>1</CE_SortOrder>
             </CHAREXT>
             <PARAM>
               <PARAM_PK>PARAM-1</PARAM_PK>
               <PARAM_DataType>TXT</PARAM_DataType>
               <PARAM_Placeholder>@Ausprägung</PARAM_Placeholder>
             </PARAM>
             <PARAM>
               <PARAM_PK>PARAM-HIDDEN</PARAM_PK>
               <PARAM_DataType>INT</PARAM_DataType>
             </PARAM>
             <PARAMX>
               <PX_CharExt_FK>CE-1</PX_CharExt_FK>
               <PX_Param_FK>PARAM-1</PX_Param_FK>
               <PX_Visible>true</PX_Visible>
               <PX_Mandatory>true</PX_Mandatory>
               <PX_RangeFrom>1.5</PX_RangeFrom>
               <PX_RangeTo>4.5</PX_RangeTo>
               <PX_Column_ID>COL_ID_CHAR1</PX_Column_ID>
               <PX_ListClass_ID>DAMAGE-WIDTH</PX_ListClass_ID>
             </PARAMX>
             <PARAMX>
               <PX_CharExt_FK>CE-1</PX_CharExt_FK>
               <PX_Param_FK>PARAM-HIDDEN</PX_Param_FK>
               <PX_Visible>false</PX_Visible>
             </PARAMX>
             <LC>
               <LC_PK>LC-1</LC_PK>
               <LC_Class_ID>DAMAGE-WIDTH</LC_Class_ID>
             </LC>
             <LIST>
               <LIST_Class_FK>LC-1</LIST_Class_FK>
               <LIST_Item>Schmal</LIST_Item>
             </LIST>
             <LIST>
               <LIST_Class_FK>LC-1</LIST_Class_FK>
               <LIST_Item>Breit</LIST_Item>
             </LIST>
           </WCCat>
           """;
}
