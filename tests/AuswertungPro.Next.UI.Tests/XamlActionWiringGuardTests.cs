using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Schuetzt vor sichtbaren Knoepfen und XAML-Ereignissen ohne Wirkung.</summary>
public sealed class XamlActionWiringGuardTests
{
    private static readonly Regex RelayCommandMethod = new(
        @"\[RelayCommand[^\]]*\]\s*(?:private|internal|public|protected)\s+" +
        @"(?:async\s+)?(?:Task(?:<[^>]+>)?|void|bool|string|[A-Za-z0-9_?.<>]+)\s+" +
        @"(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex PublicCommandProperty = new(
        @"public\s+(?:[A-Za-z0-9_.<>?, ]+)\s+(?<command>[A-Za-z_][A-Za-z0-9_]*Command)\s*\{",
        RegexOptions.Compiled);

    private static readonly HashSet<string> EventNames =
    [
        "Click", "Checked", "Unchecked", "SelectionChanged", "TextChanged",
        "ValueChanged", "MouseDoubleClick", "PreviewMouseDown",
        "PreviewMouseLeftButtonDown", "PreviewKeyDown", "KeyDown", "Drop",
        "DragOver", "Loaded", "Unloaded", "Closed", "Closing"
    ];

    [Fact]
    public void Alle_Xaml_Ereignisse_besitzen_einen_Handler_in_ihrer_partial_Klasse()
    {
        var findings = new List<string>();
        foreach (var xamlPath in XamlFiles())
        {
            var document = XDocument.Load(xamlPath, LoadOptions.PreserveWhitespace);
            var className = document.Root?.Attribute(
                XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
            if (string.IsNullOrWhiteSpace(className))
                continue;

            var shortName = className[(className.LastIndexOf('.') + 1)..];
            var code = CodeForPartialClass(shortName);
            foreach (var attribute in document.Root!.DescendantsAndSelf()
                         .Attributes()
                         .Where(attribute => EventNames.Contains(attribute.Name.LocalName))
                         .Where(attribute => Regex.IsMatch(
                             attribute.Value,
                             "^[A-Za-z_][A-Za-z0-9_]*$")))
            {
                if (!Regex.IsMatch(code, $@"\b{Regex.Escape(attribute.Value)}\s*\("))
                {
                    findings.Add(
                        $"{Path.GetRelativePath(FindRepositoryRoot(), xamlPath)}: "
                        + $"{attribute.Name.LocalName}=\"{attribute.Value}\"");
                }
            }
        }

        Assert.True(findings.Count == 0, string.Join(Environment.NewLine, findings));
    }

    [Fact]
    public void Sichtbare_Blatt_Knoepfe_haben_eine_Aktion_oder_nachweisbare_Codeverdrahtung()
    {
        var findings = new List<string>();
        foreach (var xamlPath in XamlFiles())
        {
            var document = XDocument.Load(xamlPath, LoadOptions.PreserveWhitespace);
            var className = document.Root?.Attribute(
                XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
            var shortName = className?[(className.LastIndexOf('.') + 1)..] ?? string.Empty;
            var code = CodeForPartialClass(shortName);

            foreach (var element in document.Descendants()
                         .Where(IsButtonOrMenuItem)
                         .Where(element => !element.Descendants().Any(IsButtonOrMenuItem)))
            {
                var label = LiteralAttribute(element, "Content")
                            ?? LiteralAttribute(element, "Header");
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var attributes = element.Attributes()
                    .Select(attribute => attribute.Name.LocalName)
                    .ToHashSet(StringComparer.Ordinal);
                if (attributes.Contains("Click")
                    || attributes.Contains("Command")
                    || attributes.Contains("IsCancel")
                    || attributes.Contains("IsDefault")
                    || string.Equals(LiteralAttribute(element, "IsCheckable"), "True", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = element.Attributes()
                    .FirstOrDefault(attribute => attribute.Name.LocalName == "Name")?.Value;
                var isWiredInCode = !string.IsNullOrWhiteSpace(name)
                                    && code.Contains(name, StringComparison.Ordinal)
                                    && code.Contains(".Click +=", StringComparison.Ordinal);
                if (!isWiredInCode)
                {
                    findings.Add(
                        $"{Path.GetRelativePath(FindRepositoryRoot(), xamlPath)}: {label}");
                }
            }
        }

        Assert.True(findings.Count == 0, string.Join(Environment.NewLine, findings));
    }

    [Fact]
    public void ViewModel_Befehle_der_zugeordneten_Ansicht_sind_erreichbar()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var viewModelRoot = Path.Combine(uiRoot, "ViewModels");
        var xamlFiles = XamlFiles().ToList();
        var productionCode = Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToList();
        var viewModels = Directory.EnumerateFiles(
                viewModelRoot,
                "*ViewModel.cs",
                SearchOption.AllDirectories)
            .Select(path => BuildViewModelCommandSource(path))
            .ToList();
        var declarationCounts = viewModels
            .SelectMany(viewModel => DeclaredCommands(viewModel.Text))
            .GroupBy(command => command, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var findings = new List<string>();
        foreach (var viewModel in viewModels)
        {
            var viewPath = FindAssociatedView(viewModel.Name, xamlFiles);
            if (viewPath is null)
                continue;

            var viewCode = File.ReadAllText(viewPath);
            var codeBehindPath = viewPath + ".cs";
            if (File.Exists(codeBehindPath))
                viewCode += Environment.NewLine + File.ReadAllText(codeBehindPath);

            foreach (var command in DeclaredCommands(viewModel.Text))
            {
                if (ContainsIdentifier(viewCode, command))
                    continue;

                // Ein eindeutiger Befehl darf bewusst von einem Controller oder Code-behind
                // aufgerufen werden. Bei gleichnamigen Befehlen ist ein globaler Treffer
                // dagegen kein Beweis fuer die richtige DataContext-Zuordnung.
                var isUnique = declarationCounts[command] == 1;
                var hasExternalCodeWiring = isUnique && productionCode.Any(file =>
                    !viewModel.Paths.Contains(file.Path)
                    && ContainsIdentifier(file.Text, command));
                if (hasExternalCodeWiring)
                    continue;

                findings.Add(
                    $"{Path.GetRelativePath(FindRepositoryRoot(), viewModel.MainPath)}: " +
                    $"{command} wird in {Path.GetFileName(viewPath)} nicht aufgerufen.");
            }
        }

        Assert.True(findings.Count == 0, string.Join(Environment.NewLine, findings));
    }

    private static IEnumerable<string> XamlFiles()
        => Directory.EnumerateFiles(
            RepoFile("src", "AuswertungPro.Next.UI"),
            "*.xaml",
            SearchOption.AllDirectories);

    private static (string MainPath, string Name, HashSet<string> Paths, string Text)
        BuildViewModelCommandSource(string mainPath)
    {
        var directory = Path.GetDirectoryName(mainPath)!;
        var name = Path.GetFileNameWithoutExtension(mainPath);
        var paths = Directory.EnumerateFiles(directory, name + "*.cs", SearchOption.TopDirectoryOnly)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var text = string.Join(Environment.NewLine, paths.Select(File.ReadAllText));
        return (mainPath, name, paths, text);
    }

    private static IReadOnlyList<string> DeclaredCommands(string source)
    {
        var commands = PublicCommandProperty.Matches(source)
            .Select(match => match.Groups["command"].Value)
            .ToList();
        commands.AddRange(RelayCommandMethod.Matches(source).Select(match =>
        {
            var method = match.Groups["method"].Value;
            var baseName = method.EndsWith("Async", StringComparison.Ordinal)
                ? method[..^"Async".Length]
                : method;
            return baseName + "Command";
        }));
        return commands.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string? FindAssociatedView(string viewModelName, IReadOnlyList<string> xamlFiles)
    {
        var baseName = viewModelName.EndsWith("ViewModel", StringComparison.Ordinal)
            ? viewModelName[..^"ViewModel".Length]
            : viewModelName;
        var candidates = new[]
        {
            baseName,
            baseName + "Window",
            baseName + "Dialog",
            baseName + "Page"
        };
        return candidates
            .Select(candidate => xamlFiles.FirstOrDefault(path => string.Equals(
                Path.GetFileNameWithoutExtension(path),
                candidate,
                StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(path => path is not null);
    }

    private static bool ContainsIdentifier(string source, string identifier)
        => Regex.IsMatch(source, $@"\b{Regex.Escape(identifier)}\b");

    private static string CodeForPartialClass(string shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName))
            return string.Empty;

        var marker = $"partial class {shortName}";
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    RepoFile("src", "AuswertungPro.Next.UI"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Select(path => (Path: path, Text: File.ReadAllText(path)))
                .Where(file => file.Text.Contains(marker, StringComparison.Ordinal))
                .Select(file => file.Text));
    }

    private static bool IsButtonOrMenuItem(XElement element)
        => element.Name.LocalName is "Button" or "MenuItem";

    private static string? LiteralAttribute(XElement element, string name)
    {
        var value = element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value;
        return value?.StartsWith('{') == true ? null : value;
    }
}
