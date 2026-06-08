using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoLabelToolCodeBrowserTests
{
    [Fact]
    public void AppHtml_bietet_klickbaren_code_browser_an()
    {
        var html = File.ReadAllText(FindRepoFile("tools", "VideoLabelTool", "app.html"));

        Assert.Contains("id=\"codesearch\"", html);
        Assert.Contains("id=\"codegroups\"", html);
        Assert.Contains("id=\"codechoices\"", html);
        Assert.Contains("function renderCodeBrowser", html);
        Assert.Contains("function selectCode(code)", html);
    }

    [Fact]
    public void CodeBrowser_setzt_hauptcode_und_annotationscode()
    {
        var html = File.ReadAllText(FindRepoFile("tools", "VideoLabelTool", "app.html"));

        Assert.Contains("el('codein').value=code;", html);
        Assert.Contains("if(el('anncode'))el('anncode').value=code;", html);
        Assert.Contains("onclick=\"selectCode('", html);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }
}
