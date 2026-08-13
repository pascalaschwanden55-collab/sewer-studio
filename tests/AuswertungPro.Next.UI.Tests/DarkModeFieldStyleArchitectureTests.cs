using System.IO;
using System.Xml;
using System.Xml.Linq;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Verhindert, dass ein lokaler Zusatzstil den zentralen Hell-/Dunkelmodus eines
/// Eingabefelds unbemerkt durch den hellen Windows-Standard ersetzt.
/// </summary>
public sealed class DarkModeFieldStyleArchitectureTests
{
    private static readonly string[] ThemedFieldTypes =
    [
        "TextBox",
        "ComboBox",
        "ComboBoxItem",
        "CheckBox",
        "RadioButton"
    ];

    private static readonly HashSet<string> ReadableFieldTypes =
    [
        "TextBox",
        "PasswordBox",
        "ComboBox",
        "CheckBox",
        "RadioButton",
        "DatePicker",
        "ListBox",
        "ListView",
        "DataGrid"
    ];

    [Fact]
    public void Local_field_styles_inherit_the_global_theme_or_define_a_complete_template()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var offenders = new List<string>();

        foreach (var file in EnumerateApplicationXaml(uiRoot))
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var style in document.Descendants().Where(element => element.Name.LocalName == "Style"))
            {
                var targetType = Attribute(style, "TargetType");
                if (!ThemedFieldTypes.Any(type => TargetTypeMatches(targetType, type)))
                    continue;

                if (Attribute(style, "BasedOn") is not null || DefinesOwnTemplate(style))
                    continue;

                offenders.Add(Describe(uiRoot, file, style, targetType ?? "unbekannt"));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Lokale Feldstile ohne zentralen Dunkelmodus gefunden:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Fields_do_not_use_fixed_light_or_dark_foreground_and_background_colors()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var offenders = new List<string>();

        foreach (var file in EnumerateApplicationXaml(uiRoot))
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var field in document.Descendants()
                         .Where(element => ReadableFieldTypes.Contains(element.Name.LocalName)))
            {
                foreach (var property in new[] { "Foreground", "Background" })
                {
                    var value = Attribute(field, property);
                    if (value is null || IsThemeAwareBrush(value))
                        continue;

                    offenders.Add(Describe(uiRoot, file, field, $"{field.Name.LocalName}.{property}={value}"));
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Felder mit fest verdrahteten Farben gefunden:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Shared_combobox_style_themes_the_field_and_its_dropdown()
    {
        var controls = XDocument.Load(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Controls.xaml"));
        var styles = controls.Descendants().Where(element => element.Name.LocalName == "Style").ToList();

        var comboBoxStyle = Assert.Single(styles.Where(style =>
            TargetTypeMatches(Attribute(style, "TargetType"), "ComboBox")
            && Attribute(style, "Key") is null));
        AssertSetter(comboBoxStyle, "Background", "{DynamicResource CardBrush}");
        AssertSetter(comboBoxStyle, "Foreground", "{DynamicResource TextBrush}");
        AssertSetter(comboBoxStyle, "BorderBrush", "{DynamicResource BorderBrush}");

        var dropdownBorder = Assert.Single(comboBoxStyle.Descendants().Where(element =>
            element.Name.LocalName == "Border" && Attribute(element, "Name") == "DropdownBorder"));
        Assert.Equal("{DynamicResource CardBrush}", Attribute(dropdownBorder, "Background"));
        Assert.Equal("{DynamicResource BorderBrush}", Attribute(dropdownBorder, "BorderBrush"));

        var itemStyle = Assert.Single(styles.Where(style =>
            TargetTypeMatches(Attribute(style, "TargetType"), "ComboBoxItem")
            && Attribute(style, "Key") is null));
        AssertSetter(itemStyle, "Foreground", "{DynamicResource TextBrush}");
    }

    [Fact]
    public void Programmatically_created_field_styles_do_not_bypass_the_application_theme()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var offenders = Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, lineNumber: index + 1)))
            .Where(entry => ThemedFieldTypes.Any(type =>
                entry.line.Contains($"new Style(typeof({type}));", StringComparison.Ordinal)))
            .Select(entry => $"{Path.GetRelativePath(uiRoot, entry.file)}:{entry.lineNumber}: {entry.line.Trim()}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Im Code erzeugte Feldstile ohne zentrale Designvorlage gefunden:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Clock_inputs_use_theme_resources_instead_of_a_fixed_light_face()
    {
        var clockXaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Controls", "ClockPickerControl.xaml"));
        var rangeXaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Controls", "ClockRangePickerControl.xaml"));
        var clockCode = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Controls", "ClockPickerControl.xaml.cs"));
        var rangeCode = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Controls", "ClockRangePickerControl.xaml.cs"));

        var xaml = clockXaml + rangeXaml;
        var code = clockCode + rangeCode;
        Assert.Contains("Fill=\"{DynamicResource CardBrush}\"", xaml);
        Assert.Contains("Foreground=\"{DynamicResource TextSecondaryBrush}\"", xaml);
        Assert.DoesNotContain("Fill=\"White\"", xaml);
        Assert.DoesNotContain("Foreground = Brushes.Black", code);
        Assert.Contains("SetResourceReference(TextBlock.ForegroundProperty, \"TextBrush\")", code);
    }

    private static IEnumerable<string> EnumerateApplicationXaml(string uiRoot)
        => Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(uiRoot, file)
                .StartsWith($"Theme{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static bool DefinesOwnTemplate(XElement style)
        => style.Elements().Any(element =>
            element.Name.LocalName == "Setter" && Attribute(element, "Property") == "Template");

    private static bool TargetTypeMatches(string? targetType, string expected)
        => string.Equals(targetType, expected, StringComparison.Ordinal)
           || string.Equals(targetType, $"{{x:Type {expected}}}", StringComparison.Ordinal);

    private static bool IsThemeAwareBrush(string value)
        => value.Equals("Transparent", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("{DynamicResource ", StringComparison.Ordinal)
           || value.StartsWith("{StaticResource ", StringComparison.Ordinal)
           || value.StartsWith("{TemplateBinding ", StringComparison.Ordinal);

    private static void AssertSetter(XElement style, string property, string expectedValue)
    {
        var setter = Assert.Single(style.Elements().Where(element =>
            element.Name.LocalName == "Setter" && Attribute(element, "Property") == property));
        Assert.Equal(expectedValue, Attribute(setter, "Value"));
    }

    private static string? Attribute(XElement element, string localName)
        => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static string Describe(string uiRoot, string file, XElement element, string detail)
    {
        var line = (element as IXmlLineInfo)?.LineNumber ?? 0;
        return $"{Path.GetRelativePath(uiRoot, file)}:{line}: {detail}";
    }
}
