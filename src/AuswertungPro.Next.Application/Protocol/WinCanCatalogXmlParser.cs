using System.Globalization;
using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Liest die Tabellenstruktur eines WinCan-WCCat-Dokuments.
/// Die Zusammenfuehrung mit Ersatzkatalogen bleibt beim aufrufenden Provider.
/// </summary>
internal static class WinCanCatalogXmlParser
{
    public static WinCanCatalogXmlData Parse(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var ns = root.Name.Namespace;

        return new WinCanCatalogXmlData(
            ParseClasses(root, ns),
            ParseBaseCodes(root, ns),
            ParseCharacterExtensions(root, ns),
            ParseParameters(root, ns),
            ParseParameterLinks(root, ns),
            ParseListValues(root, ns));
    }

    public static List<string> BuildCategoryPath(
        WinCanCharacterExtension characterExtension,
        IReadOnlyDictionary<string, WinCanBaseCode> baseCodes,
        IReadOnlyDictionary<string, WinCanClass> classes)
    {
        var path = new List<string>();
        if (string.IsNullOrWhiteSpace(characterExtension.BaseCodeFK)
            || !baseCodes.TryGetValue(characterExtension.BaseCodeFK, out var baseCode))
        {
            return path;
        }

        if (!string.IsNullOrWhiteSpace(baseCode.ClassFK)
            && classes.TryGetValue(baseCode.ClassFK, out var catalogClass))
        {
            var classLabel = catalogClass.ChildCaption ?? catalogClass.Remarks;
            if (!string.IsNullOrWhiteSpace(classLabel))
                path.Add(classLabel);
        }

        if (!string.IsNullOrWhiteSpace(baseCode.ChildCaption))
            path.Add(baseCode.ChildCaption);

        return path;
    }

    public static List<string> BuildCategoryPath(
        WinCanBaseCode baseCode,
        IReadOnlyDictionary<string, WinCanClass> classes)
    {
        var path = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseCode.ClassFK)
            && classes.TryGetValue(baseCode.ClassFK, out var catalogClass))
        {
            var classLabel = catalogClass.ChildCaption ?? catalogClass.Remarks;
            if (!string.IsNullOrWhiteSpace(classLabel))
                path.Add(classLabel);
        }

        return path;
    }

    public static List<CodeParameter> BuildParameters(
        IEnumerable<WinCanParameterLink> links,
        IReadOnlyDictionary<string, WinCanParameter> parameters,
        IReadOnlyDictionary<string, List<string>> listValues)
    {
        var result = new List<CodeParameter>();

        foreach (var link in links)
        {
            if (!parameters.TryGetValue(link.ParamFK, out var parameter))
                continue;

            var name = parameter.Placeholder ?? parameter.DataType;
            if (string.IsNullOrWhiteSpace(name))
                name = "Parameter";
            if (name.StartsWith('@'))
                name = name[1..];

            List<string>? allowedValues = null;
            if (!string.IsNullOrWhiteSpace(link.ListClassId)
                && listValues.TryGetValue(link.ListClassId, out var values))
            {
                allowedValues = values;
            }

            result.Add(new CodeParameter
            {
                Name = name,
                DataKey = MapColumnIdToDataKey(link.ColumnId ?? string.Empty),
                Type = ResolveParameterType(parameter, link, listValues),
                AllowedValues = allowedValues,
                Unit = parameter.Unit,
                Required = link.Mandatory
            });
        }

        return result;
    }

    public static string ExtractCloseCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0].Trim() : raw.Trim();
    }

    private static Dictionary<string, WinCanClass> ParseClasses(XElement root, XNamespace ns)
    {
        var result = new Dictionary<string, WinCanClass>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in root.Descendants(ns + "CLASS"))
        {
            var key = element.Element(ns + "CLASS_PK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            result[key] = new WinCanClass
            {
                PK = key,
                Level = ParseInt(element.Element(ns + "CLASS_Level")),
                SortOrder = ParseInt(element.Element(ns + "CLASS_SortOrder")),
                Remarks = element.Element(ns + "CLASS_Remarks")?.Value?.Trim(),
                ChildCaption = element.Element(ns + "CLASS_ChildCaption")?.Value?.Trim()
            };
        }

        return result;
    }

    private static Dictionary<string, WinCanBaseCode> ParseBaseCodes(XElement root, XNamespace ns)
    {
        var result = new Dictionary<string, WinCanBaseCode>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in root.Descendants(ns + "BASECODE"))
        {
            var key = element.Element(ns + "BC_PK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            result[key] = new WinCanBaseCode
            {
                PK = key,
                ClassFK = element.Element(ns + "BC_Class_FK")?.Value?.Trim(),
                Code = (element.Element(ns + "BC_Code")?.Value ?? string.Empty).Trim(),
                ChildCaption = (element.Element(ns + "BC_ChildCaption")?.Value ?? string.Empty).Trim(),
                Remarks = element.Element(ns + "BC_Remarks")?.Value?.Trim(),
                CloseCode = element.Element(ns + "BC_CloseCode")?.Value?.Trim(),
                Follower = element.Element(ns + "BC_Follower")?.Value?.Trim(),
                SortOrder = ParseInt(element.Element(ns + "BC_SortOrder")),
                IsVirtual = string.Equals(
                    element.Element(ns + "BC_IsVirtual")?.Value,
                    "true",
                    StringComparison.OrdinalIgnoreCase)
            };
        }

        return result;
    }

    private static Dictionary<string, WinCanCharacterExtension> ParseCharacterExtensions(
        XElement root,
        XNamespace ns)
    {
        var result = new Dictionary<string, WinCanCharacterExtension>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in root.Descendants(ns + "CHAREXT"))
        {
            var key = element.Element(ns + "CE_PK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            result[key] = new WinCanCharacterExtension
            {
                PK = key,
                BaseCodeFK = element.Element(ns + "CE_BaseCode_FK")?.Value?.Trim(),
                Code = (element.Element(ns + "CE_Code")?.Value ?? string.Empty).Trim(),
                ChildCaption = (element.Element(ns + "CE_ChildCaption")?.Value ?? string.Empty).Trim(),
                Remarks = element.Element(ns + "CE_Remarks")?.Value?.Trim(),
                CloseCode = element.Element(ns + "CE_CloseCode")?.Value?.Trim(),
                MetaCode = element.Element(ns + "CE_MetaCode")?.Value?.Trim(),
                SortOrder = ParseInt(element.Element(ns + "CE_SortOrder"))
            };
        }

        return result;
    }

    private static Dictionary<string, WinCanParameter> ParseParameters(XElement root, XNamespace ns)
    {
        var result = new Dictionary<string, WinCanParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in root.Descendants(ns + "PARAM"))
        {
            var key = element.Element(ns + "PARAM_PK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            result[key] = new WinCanParameter
            {
                PK = key,
                DataType = (element.Element(ns + "PARAM_DataType")?.Value ?? "TXT").Trim(),
                Placeholder = element.Element(ns + "PARAM_Placeholder")?.Value?.Trim(),
                Unit = element.Element(ns + "PARAM_Unit")?.Value?.Trim(),
                TypeFlags = ParseInt(element.Element(ns + "PARAM_TypeFlags"))
            };
        }

        return result;
    }

    private static List<WinCanParameterLink> ParseParameterLinks(XElement root, XNamespace ns)
    {
        var result = new List<WinCanParameterLink>();
        foreach (var element in root.Descendants(ns + "PARAMX"))
        {
            var parameterKey = element.Element(ns + "PX_Param_FK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(parameterKey))
                continue;
            if (string.Equals(
                    element.Element(ns + "PX_Visible")?.Value,
                    "false",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new WinCanParameterLink
            {
                CharExtFK = element.Element(ns + "PX_CharExt_FK")?.Value?.Trim(),
                BaseCodeFK = element.Element(ns + "PX_BaseCode_FK")?.Value?.Trim(),
                ParamFK = parameterKey,
                Mandatory = string.Equals(
                    element.Element(ns + "PX_Mandatory")?.Value,
                    "true",
                    StringComparison.OrdinalIgnoreCase),
                RangeFrom = ParseDouble(element.Element(ns + "PX_RangeFrom")),
                RangeTo = ParseDouble(element.Element(ns + "PX_RangeTo")),
                ColumnId = element.Element(ns + "PX_Column_ID")?.Value?.Trim(),
                ListClassId = element.Element(ns + "PX_ListClass_ID")?.Value?.Trim()
            });
        }

        return result;
    }

    private static Dictionary<string, List<string>> ParseListValues(XElement root, XNamespace ns)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var classIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in root.Descendants(ns + "LC"))
        {
            var key = element.Element(ns + "LC_PK")?.Value?.Trim();
            var classId = element.Element(ns + "LC_Class_ID")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(classId))
                classIds[key] = classId;
        }

        foreach (var element in root.Descendants(ns + "LIST"))
        {
            var classKey = element.Element(ns + "LIST_Class_FK")?.Value?.Trim();
            var item = element.Element(ns + "LIST_Item")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(classKey) || string.IsNullOrWhiteSpace(item))
                continue;

            var key = classIds.TryGetValue(classKey, out var classId) ? classId : classKey;
            if (!result.TryGetValue(key, out var values))
            {
                values = new List<string>();
                result[key] = values;
            }

            if (!values.Contains(item, StringComparer.OrdinalIgnoreCase))
                values.Add(item);
        }

        return result;
    }

    private static string ResolveParameterType(
        WinCanParameter parameter,
        WinCanParameterLink link,
        IReadOnlyDictionary<string, List<string>> listValues)
    {
        var columnId = link.ColumnId ?? string.Empty;
        if (columnId.Contains("CLK", StringComparison.OrdinalIgnoreCase)
            || parameter.Placeholder?.Contains("CLK", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "clock";
        }

        if (!string.IsNullOrWhiteSpace(link.ListClassId) && listValues.ContainsKey(link.ListClassId))
            return "enum";

        return parameter.DataType.ToUpperInvariant() is "INT" or "DEC" or "DOC"
            ? "number"
            : "string";
    }

    private static string? MapColumnIdToDataKey(string columnId)
    {
        if (string.IsNullOrWhiteSpace(columnId))
            return null;

        return columnId.ToUpperInvariant() switch
        {
            "COL_ID_CLK1" => "CLK1",
            "COL_ID_CLK2" => "CLK2",
            "COL_ID_QUANT1" => "Q1",
            "COL_ID_QUANT2" => "Q2",
            "COL_ID_QUANT3" => "Q3",
            "COL_ID_UNIT1" => "UNIT1",
            "COL_ID_UNIT2" => "UNIT2",
            "COL_ID_UNIT3" => "UNIT3",
            "COL_ID_CHAR1" => "CHAR1",
            "COL_ID_CD" => "CD",
            "COL_ID_REMARKS" => "REMARKS",
            _ => columnId
        };
    }

    private static int ParseInt(XElement? element)
        => int.TryParse(element?.Value, out var value) ? value : 0;

    private static double? ParseDouble(XElement? element)
        => double.TryParse(element?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}

internal sealed record WinCanCatalogXmlData(
    Dictionary<string, WinCanClass> Classes,
    Dictionary<string, WinCanBaseCode> BaseCodes,
    Dictionary<string, WinCanCharacterExtension> CharacterExtensions,
    Dictionary<string, WinCanParameter> Parameters,
    List<WinCanParameterLink> ParameterLinks,
    Dictionary<string, List<string>> ListValues);

internal sealed class WinCanClass
{
    public string PK { get; init; } = string.Empty;
    public int Level { get; init; }
    public int SortOrder { get; init; }
    public string? Remarks { get; init; }
    public string? ChildCaption { get; init; }
}

internal sealed class WinCanBaseCode
{
    public string PK { get; init; } = string.Empty;
    public string? ClassFK { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ChildCaption { get; init; } = string.Empty;
    public string? Remarks { get; init; }
    public string? CloseCode { get; init; }
    public string? Follower { get; init; }
    public int SortOrder { get; init; }
    public bool IsVirtual { get; init; }
}

internal sealed class WinCanCharacterExtension
{
    public string PK { get; init; } = string.Empty;
    public string? BaseCodeFK { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ChildCaption { get; init; } = string.Empty;
    public string? Remarks { get; init; }
    public string? CloseCode { get; init; }
    public string? MetaCode { get; init; }
    public int SortOrder { get; init; }
}

internal sealed class WinCanParameter
{
    public string PK { get; init; } = string.Empty;
    public string DataType { get; init; } = "TXT";
    public string? Placeholder { get; init; }
    public string? Unit { get; init; }
    public int TypeFlags { get; init; }
}

internal sealed class WinCanParameterLink
{
    public string? CharExtFK { get; init; }
    public string? BaseCodeFK { get; init; }
    public string ParamFK { get; init; } = string.Empty;
    public bool Mandatory { get; init; }
    public double? RangeFrom { get; init; }
    public double? RangeTo { get; init; }
    public string? ColumnId { get; init; }
    public string? ListClassId { get; init; }
}
