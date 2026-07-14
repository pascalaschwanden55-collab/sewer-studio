using System.IO;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.Tests;

public sealed class UserErrorArchitectureTests
{
    private static readonly Regex RawExceptionInDialog = new(
        @"(?s)(?:Dialogs?|_dialogs?|MessageBox)\.(?:Error|Warn|Info|Show)\s*\((?:(?!;).)*\b(?:ex|exception)\.Message",
        RegexOptions.CultureInvariant);

    private static readonly Regex RawExceptionInViewModelStatus = new(
        @"(?m)(?:\b(?:StatusText|Summary|LastResult|KiStatus)\s*=|\bSetStatus\s*\()\s*[^;\r\n]*\b(?:ex|exception)\.Message",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Ui_Dialoge_zeigen_keine_rohen_Exception_Meldungen()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var separator = Path.DirectorySeparatorChar;

        var offenders = Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase))
            .Where(file => RawExceptionInDialog.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Technische Exception-Texte duerfen nicht direkt im Dialog erscheinen. "
            + "UserError verwenden und die volle Ursache nur protokollieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void ViewModel_Status_zeigt_keine_rohen_Exception_Meldungen()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var viewModelRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels");

        var offenders = Directory.EnumerateFiles(viewModelRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => RawExceptionInViewModelStatus.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Technische Exception-Texte duerfen nicht direkt im sichtbaren ViewModel-Status erscheinen. "
            + "UserError verwenden und die volle Ursache nur protokollieren:\n"
            + string.Join("\n", offenders));
    }
}
