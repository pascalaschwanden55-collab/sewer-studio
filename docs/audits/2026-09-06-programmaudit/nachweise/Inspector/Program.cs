using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args[0] == "--repros") { AuditRepros.Run(args[1], args[2]); return; }

var root = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var records = new List<object>();
var methods = new List<object>();
var observations = new List<object>();
var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
foreach (var area in new[] { "src", "tests", "tools" })
foreach (var path in Directory.EnumerateFiles(Path.Combine(root, area), "*.cs", options))
{
    var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
    if (relative.Split('/').Any(x => x is "bin" or "obj" or ".tmp" or ".venv")) continue;
    var bytes = File.ReadAllBytes(path);
    var source = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
    var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), relative);
    var syntax = tree.GetRoot();
    var members = syntax.DescendantNodes().Where(n => n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax || n is PropertyDeclarationSyntax { ExpressionBody: not null }).ToList();
    records.Add(new { file = relative, lines = tree.GetText().Lines.Count, sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), callables = members.Count,
        usings = syntax.DescendantNodes().OfType<UsingDirectiveSyntax>().Select(u => u.Name?.ToString()).ToArray(),
        types = syntax.DescendantNodes().OfType<TypeDeclarationSyntax>().Select(t => new { name = t.Identifier.Text, partial = t.Modifiers.Any(SyntaxKind.PartialKeyword), kind = t.Keyword.Text, bases = t.BaseList?.Types.Select(b => b.Type.ToString()).ToArray() }).ToArray() });
    foreach (var member in members)
    {
        var span = tree.GetLineSpan(member.Span);
        string name = member switch {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            DestructorDeclarationSyntax d => "~" + d.Identifier.Text,
            OperatorDeclarationSyntax o => "operator " + o.OperatorToken.Text,
            ConversionOperatorDeclarationSyntax c => "operator " + c.Type,
            LocalFunctionStatementSyntax l => l.Identifier.Text,
            AccessorDeclarationSyntax a => (a.Parent?.Parent as MemberDeclarationSyntax)?.ToString().Split('\n')[0].Trim() + ":" + a.Keyword.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            _ => member.Kind().ToString()
        };
        var type = string.Join(".", member.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.Text));
        var body = member switch { BaseMethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody, LocalFunctionStatementSyntax l => (SyntaxNode?)l.Body ?? l.ExpressionBody, AccessorDeclarationSyntax a => (SyntaxNode?)a.Body ?? a.ExpressionBody, PropertyDeclarationSyntax p => p.ExpressionBody, _ => null };
        methods.Add(new { file = relative, type, name, kind = member.Kind().ToString(), line = span.StartLinePosition.Line + 1, lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1, hasBody = body != null,
            branches = body?.DescendantNodes().Count(n => n is IfStatementSyntax or SwitchSectionSyntax or ConditionalExpressionSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or CatchClauseSyntax) ?? 0 });
    }
    if (area == "tests") continue;
    foreach (var c in syntax.DescendantNodes().OfType<CatchClauseSyntax>())
        if (!c.Block.Statements.Any()) observations.Add(new { file = relative, line = tree.GetLineSpan(c.Span).StartLinePosition.Line + 1, kind = "empty_catch", text = c.ToString() });
    foreach (var a in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>())
        if (a.Modifiers.Any(SyntaxKind.AsyncKeyword) && a.ReturnType.ToString() == "void") observations.Add(new { file = relative, line = tree.GetLineSpan(a.Span).StartLinePosition.Line + 1, kind = "async_void", text = a.Identifier.Text });
}
Directory.CreateDirectory(output);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, "csharp-files.json"), JsonSerializer.Serialize(records, jsonOptions));
File.WriteAllText(Path.Combine(output, "csharp-functions.json"), JsonSerializer.Serialize(methods, jsonOptions));
File.WriteAllText(Path.Combine(output, "static-candidates.json"), JsonSerializer.Serialize(observations, jsonOptions));
Console.WriteLine($"C#-Dateien: {records.Count}; Deklarationen: {methods.Count}; Pruefkandidaten: {observations.Count}. Kein Kandidat ist ohne Codepruefung ein Fehler.");
