using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoLabelToolCodeBrowserTests
{
    [Fact]
    public void AppHtml_bietet_klickbaren_code_browser_an()
    {
        var html = File.ReadAllText(RepoFile("tools", "VideoLabelTool", "app.html"));

        Assert.Contains("id=\"codesearch\"", html);
        Assert.Contains("id=\"codegroups\"", html);
        Assert.Contains("id=\"codechoices\"", html);
        Assert.Contains("function renderCodeBrowser", html);
        Assert.Contains("function selectCode(code)", html);
    }

    [Fact]
    public void CodeBrowser_setzt_hauptcode_und_annotationscode()
    {
        var html = File.ReadAllText(RepoFile("tools", "VideoLabelTool", "app.html"));

        Assert.Contains("el('codein').value=code;", html);
        Assert.Contains("if(el('anncode'))el('anncode').value=code;", html);
        Assert.Contains("onclick=\"selectCode('", html);
    }

}
