using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Protocol;

// XmlCodeCatalogProvider WinCan-XML-Format-Loader: Liest WcClass/WcBaseCode/
// WcCharExt/WcParam-Tabellen aus dem WinCan-Katalog-XML, baut Kategorie-
// Pfade auf und mappt ColumnId -> DataKey. Aus dem Hauptdatei extrahiert
// (Slice 24a).
public sealed partial class XmlCodeCatalogProvider
{
    private void LoadWinCanCatalog(
        XElement root,
        Dictionary<string, CodeDefinition> fallback,
        Dictionary<string, FallbackText> fallbackTexts,
        Dictionary<string, int> counts)
    {
        XNamespace ns = root.Name.Namespace;

        _codes.Clear();
        _byCode.Clear();

        // Phase 1: Parse all WCCat elements into lookup dictionaries
        var classes = ParseWcClasses(root, ns);
        var baseCodes = ParseWcBaseCodes(root, ns);
        var charExts = ParseWcCharExts(root, ns);
        var parameters = ParseWcParams(root, ns);
        var paramLinks = ParseWcParamLinks(root, ns);
        var listValues = ParseWcListValues(root, ns);

        // Build lookup: BaseCode PK -> list of its CharExts
        var charExtsByBaseCode = charExts.Values
            .Where(ce => !string.IsNullOrWhiteSpace(ce.BaseCodeFK))
            .GroupBy(ce => ce.BaseCodeFK!)
            .ToDictionary(g => g.Key, g => g.OrderBy(ce => ce.SortOrder).ToList());

        // Build lookup: CharExt PK -> list of its ParamLinks
        var paramLinksByCharExt = paramLinks
            .Where(pl => !string.IsNullOrWhiteSpace(pl.CharExtFK))
            .GroupBy(pl => pl.CharExtFK!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build lookup: BaseCode PK -> list of its ParamLinks (for standalone basecodes)
        var paramLinksByBaseCode = paramLinks
            .Where(pl => !string.IsNullOrWhiteSpace(pl.BaseCodeFK) && string.IsNullOrWhiteSpace(pl.CharExtFK))
            .GroupBy(pl => pl.BaseCodeFK!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Phase 2: Emit CodeDefinitions from CHAREXT elements
        foreach (var ce in charExts.Values.OrderBy(c => c.SortOrder))
        {
            var code = ExtractCloseCode(ce.CloseCode ?? "");
            if (string.IsNullOrWhiteSpace(code))
                code = ce.Code.Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var title = ce.ChildCaption;
            var description = ce.Remarks ?? "";

            // Build category path from CLASS -> BASECODE -> CHAREXT hierarchy
            var categoryPath = BuildWcCategoryPath(ce, baseCodes, classes);

            // Build group from category path
            var group = categoryPath.Count > 0 ? string.Join(" / ", categoryPath.Take(2)) : "";

            // Build parameters from PARAMX -> PARAM chain
            var codeParams = paramLinksByCharExt.TryGetValue(ce.PK, out var links)
                ? BuildWcParameters(links, parameters, listValues)
                : new List<CodeParameter>();

            // Apply fallback enrichment
            ApplyFallback(ref title, ref group, ref description, code, fallback, fallbackTexts, out var fallbackDef);

            if (string.IsNullOrWhiteSpace(title))
                title = code;
            if (string.IsNullOrWhiteSpace(group))
                group = "Unbekannt";

            // Merge prefix-based path segments
            var prefixPath = BuildPrefixPath(code);
            var mergedPath = categoryPath.Concat(prefixPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Use fallback parameters if we didn't extract any from PARAMX
            if (codeParams.Count == 0 && fallbackDef?.Parameters is { Count: > 0 })
                codeParams = fallbackDef.Parameters.Select(CloneParameter).ToList();

            var def = new CodeDefinition
            {
                Code = code,
                Title = title,
                Group = group,
                CategoryPath = mergedPath,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Parameters = codeParams
            };

            if (fallbackDef is not null)
            {
                def.RequiresRange = fallbackDef.RequiresRange;
                def.RangeThresholdM = fallbackDef.RangeThresholdM;
                def.RangeThresholdText = fallbackDef.RangeThresholdText;
            }

            AddOrUpdate(def, counts);
        }

        // Phase 3: Emit standalone BASECODE entries (those with CloseCode but no CHAREXT children)
        foreach (var bc in baseCodes.Values.OrderBy(b => b.SortOrder))
        {
            if (bc.IsVirtual)
                continue;

            var code = ExtractCloseCode(bc.CloseCode ?? "");
            if (string.IsNullOrWhiteSpace(code))
                continue;

            // Skip if we already have this code from CHAREXT processing
            if (_byCode.ContainsKey(NormalizeCode(code)))
                continue;

            var title = bc.ChildCaption;
            var description = bc.Remarks ?? "";
            var categoryPath = BuildWcBaseCodeCategoryPath(bc, classes);
            var group = categoryPath.Count > 0 ? string.Join(" / ", categoryPath.Take(2)) : "";

            var codeParams = paramLinksByBaseCode.TryGetValue(bc.PK, out var bcLinks)
                ? BuildWcParameters(bcLinks, parameters, listValues)
                : new List<CodeParameter>();

            ApplyFallback(ref title, ref group, ref description, code, fallback, fallbackTexts, out var fallbackDef);

            if (string.IsNullOrWhiteSpace(title))
                title = code;
            if (string.IsNullOrWhiteSpace(group))
                group = "Unbekannt";

            var prefixPath = BuildPrefixPath(code);
            var mergedPath = categoryPath.Concat(prefixPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codeParams.Count == 0 && fallbackDef?.Parameters is { Count: > 0 })
                codeParams = fallbackDef.Parameters.Select(CloneParameter).ToList();

            var def = new CodeDefinition
            {
                Code = code,
                Title = title,
                Group = group,
                CategoryPath = mergedPath,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Parameters = codeParams
            };

            if (fallbackDef is not null)
            {
                def.RequiresRange = fallbackDef.RequiresRange;
                def.RangeThresholdM = fallbackDef.RangeThresholdM;
                def.RangeThresholdText = fallbackDef.RangeThresholdText;
            }

            AddOrUpdate(def, counts);
        }

        FinalizeLoad(counts);
    }

    private static void ApplyFallback(
        ref string title, ref string group, ref string description,
        string code,
        Dictionary<string, CodeDefinition> fallback,
        Dictionary<string, FallbackText> fallbackTexts,
        out CodeDefinition? fallbackDef)
    {
        fallbackDef = null;

        if (fallback.TryGetValue(code, out var fb))
        {
            fallbackDef = fb;
            if (!string.IsNullOrWhiteSpace(fb.Title))
                title = fb.Title;
            if (!string.IsNullOrWhiteSpace(fb.Group))
                group = fb.Group;
            if (!string.IsNullOrWhiteSpace(fb.Description))
                description = fb.Description;
        }

        if (fallbackTexts.TryGetValue(code, out var ft))
        {
            if (string.IsNullOrWhiteSpace(title))
                title = ft.Title;
            if (string.IsNullOrWhiteSpace(description))
                description = ft.Description ?? "";
        }
    }

    // ── WCCat Parse Methods ────────────────────────────────────────────

    private static Dictionary<string, WcClass> ParseWcClasses(XElement root, XNamespace ns)
    {
        var dict = new Dictionary<string, WcClass>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in root.Descendants(ns + "CLASS"))
        {
            var pk = el.Element(ns + "CLASS_PK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(pk))
                continue;

            dict[pk] = new WcClass
            {
                PK = pk,
                Level = int.TryParse(el.Element(ns + "CLASS_Level")?.Value, out var lv) ? lv : 0,
                SortOrder = int.TryParse(el.Element(ns + "CLASS_SortOrder")?.Value, out var so) ? so : 0,
                Remarks = el.Element(ns + "CLASS_Remarks")?.Value?.Trim(),
                ChildCaption = el.Element(ns + "CLASS_ChildCaption")?.Value?.Trim()
            };
        }
        return dict;
    }

    private static Dictionary<string, WcBaseCode> ParseWcBaseCodes(XElement root, XNamespace ns)
    {
        var dict = new Dictionary<string, WcBaseCode>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in root.Descendants(ns + "BASECODE"))
        {
            var pk = el.Element(ns + "BC_PK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(pk))
                continue;

            dict[pk] = new WcBaseCode
            {
                PK = pk,
                ClassFK = el.Element(ns + "BC_Class_FK")?.Value?.Trim(),
                Code = (el.Element(ns + "BC_Code")?.Value ?? "").Trim(),
                ChildCaption = (el.Element(ns + "BC_ChildCaption")?.Value ?? "").Trim(),
                Remarks = el.Element(ns + "BC_Remarks")?.Value?.Trim(),
                CloseCode = el.Element(ns + "BC_CloseCode")?.Value?.Trim(),
                Follower = el.Element(ns + "BC_Follower")?.Value?.Trim(),
                SortOrder = int.TryParse(el.Element(ns + "BC_SortOrder")?.Value, out var so) ? so : 0,
                IsVirtual = string.Equals(el.Element(ns + "BC_IsVirtual")?.Value, "true", StringComparison.OrdinalIgnoreCase)
            };
        }
        return dict;
    }

    private static Dictionary<string, WcCharExt> ParseWcCharExts(XElement root, XNamespace ns)
    {
        var dict = new Dictionary<string, WcCharExt>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in root.Descendants(ns + "CHAREXT"))
        {
            var pk = el.Element(ns + "CE_PK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(pk))
                continue;

            dict[pk] = new WcCharExt
            {
                PK = pk,
                BaseCodeFK = el.Element(ns + "CE_BaseCode_FK")?.Value?.Trim(),
                Code = (el.Element(ns + "CE_Code")?.Value ?? "").Trim(),
                ChildCaption = (el.Element(ns + "CE_ChildCaption")?.Value ?? "").Trim(),
                Remarks = el.Element(ns + "CE_Remarks")?.Value?.Trim(),
                CloseCode = el.Element(ns + "CE_CloseCode")?.Value?.Trim(),
                MetaCode = el.Element(ns + "CE_MetaCode")?.Value?.Trim(),
                SortOrder = int.TryParse(el.Element(ns + "CE_SortOrder")?.Value, out var so) ? so : 0
            };
        }
        return dict;
    }

    private static Dictionary<string, WcParam> ParseWcParams(XElement root, XNamespace ns)
    {
        var dict = new Dictionary<string, WcParam>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in root.Descendants(ns + "PARAM"))
        {
            var pk = el.Element(ns + "PARAM_PK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(pk))
                continue;

            dict[pk] = new WcParam
            {
                PK = pk,
                DataType = (el.Element(ns + "PARAM_DataType")?.Value ?? "TXT").Trim(),
                Placeholder = el.Element(ns + "PARAM_Placeholder")?.Value?.Trim(),
                Unit = el.Element(ns + "PARAM_Unit")?.Value?.Trim(),
                TypeFlags = int.TryParse(el.Element(ns + "PARAM_TypeFlags")?.Value, out var tf) ? tf : 0
            };
        }
        return dict;
    }

    private static List<WcParamLink> ParseWcParamLinks(XElement root, XNamespace ns)
    {
        var list = new List<WcParamLink>();
        foreach (var el in root.Descendants(ns + "PARAMX"))
        {
            var paramFK = el.Element(ns + "PX_Param_FK")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(paramFK))
                continue;

            var visible = el.Element(ns + "PX_Visible")?.Value;
            if (string.Equals(visible, "false", StringComparison.OrdinalIgnoreCase))
                continue;

            list.Add(new WcParamLink
            {
                CharExtFK = el.Element(ns + "PX_CharExt_FK")?.Value?.Trim(),
                BaseCodeFK = el.Element(ns + "PX_BaseCode_FK")?.Value?.Trim(),
                ParamFK = paramFK,
                Mandatory = string.Equals(el.Element(ns + "PX_Mandatory")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                RangeFrom = double.TryParse(el.Element(ns + "PX_RangeFrom")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rf) ? rf : null,
                RangeTo = double.TryParse(el.Element(ns + "PX_RangeTo")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rt) ? rt : null,
                ColumnId = el.Element(ns + "PX_Column_ID")?.Value?.Trim(),
                ListClassId = el.Element(ns + "PX_ListClass_ID")?.Value?.Trim()
            });
        }
        return list;
    }

    private static Dictionary<string, List<string>> ParseWcListValues(XElement root, XNamespace ns)
    {
        // Group LIST_Item values by LIST_Class_FK for enum parameter lookups
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // First, build LC lookup (LC_PK -> LC_Class_ID)
        var lcLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in root.Descendants(ns + "LC"))
        {
            var pk = el.Element(ns + "LC_PK")?.Value?.Trim();
            var classId = el.Element(ns + "LC_Class_ID")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(pk) && !string.IsNullOrWhiteSpace(classId))
                lcLookup[pk] = classId;
        }

        // Parse LIST elements and group by their LC class ID
        foreach (var el in root.Descendants(ns + "LIST"))
        {
            var classFK = el.Element(ns + "LIST_Class_FK")?.Value?.Trim();
            var item = el.Element(ns + "LIST_Item")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(classFK) || string.IsNullOrWhiteSpace(item))
                continue;

            // Resolve LC_Class_ID from classFK for a stable key
            var key = lcLookup.TryGetValue(classFK, out var lcId) ? lcId : classFK;

            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<string>();
                dict[key] = list;
            }
            if (!list.Contains(item, StringComparer.OrdinalIgnoreCase))
                list.Add(item);
        }

        return dict;
    }

    private static List<string> BuildWcCategoryPath(WcCharExt ce, Dictionary<string, WcBaseCode> baseCodes, Dictionary<string, WcClass> classes)
    {
        var path = new List<string>();

        // Resolve CHAREXT -> BASECODE -> CLASS
        WcBaseCode? bc = null;
        if (!string.IsNullOrWhiteSpace(ce.BaseCodeFK) && baseCodes.TryGetValue(ce.BaseCodeFK, out bc))
        {
            if (!string.IsNullOrWhiteSpace(bc.ClassFK) && classes.TryGetValue(bc.ClassFK, out var cls))
            {
                var classLabel = cls.ChildCaption ?? cls.Remarks;
                if (!string.IsNullOrWhiteSpace(classLabel))
                    path.Add(classLabel);
            }

            if (!string.IsNullOrWhiteSpace(bc.ChildCaption))
                path.Add(bc.ChildCaption);
        }

        return path;
    }

    private static List<string> BuildWcBaseCodeCategoryPath(WcBaseCode bc, Dictionary<string, WcClass> classes)
    {
        var path = new List<string>();
        if (!string.IsNullOrWhiteSpace(bc.ClassFK) && classes.TryGetValue(bc.ClassFK, out var cls))
        {
            var classLabel = cls.ChildCaption ?? cls.Remarks;
            if (!string.IsNullOrWhiteSpace(classLabel))
                path.Add(classLabel);
        }
        return path;
    }

    private static List<CodeParameter> BuildWcParameters(
        List<WcParamLink> links,
        Dictionary<string, WcParam> parameters,
        Dictionary<string, List<string>> listValues)
    {
        var result = new List<CodeParameter>();

        foreach (var link in links)
        {
            if (!parameters.TryGetValue(link.ParamFK, out var param))
                continue;

            var name = param.Placeholder ?? param.DataType;
            if (string.IsNullOrWhiteSpace(name))
                name = "Parameter";
            // Clean up placeholder names (remove @ prefix)
            if (name.StartsWith("@"))
                name = name.Substring(1);

            var columnId = link.ColumnId ?? "";
            var type = ResolveWcParamType(param, link, listValues);

            List<string>? allowedValues = null;
            if (!string.IsNullOrWhiteSpace(link.ListClassId) && listValues.TryGetValue(link.ListClassId, out var values))
                allowedValues = values;

            // Map ColumnId to a DataKey compatible with WinCan import
            var dataKey = MapColumnIdToDataKey(columnId);

            result.Add(new CodeParameter
            {
                Name = name,
                DataKey = dataKey,
                Type = type,
                AllowedValues = allowedValues,
                Unit = param.Unit,
                Required = link.Mandatory
            });
        }

        return result;
    }

    private static string ResolveWcParamType(WcParam param, WcParamLink link, Dictionary<string, List<string>> listValues)
    {
        // Clock detection
        var colId = link.ColumnId ?? "";
        if (colId.Contains("CLK", StringComparison.OrdinalIgnoreCase))
            return "clock";
        if (param.Placeholder is not null && param.Placeholder.Contains("CLK", StringComparison.OrdinalIgnoreCase))
            return "clock";

        // Enum detection (has list values)
        if (!string.IsNullOrWhiteSpace(link.ListClassId) && listValues.ContainsKey(link.ListClassId))
            return "enum";

        // Number detection
        var dt = param.DataType.ToUpperInvariant();
        if (dt is "INT" or "DEC" or "DOC")
            return "number";

        return "string";
    }

    private static string? MapColumnIdToDataKey(string columnId)
    {
        if (string.IsNullOrWhiteSpace(columnId))
            return null;

        // Map WinCan column IDs to the DataKey format used by ObservationCatalogViewModel
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

    private static string ExtractCloseCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0].Trim() : raw.Trim();
    }

}
