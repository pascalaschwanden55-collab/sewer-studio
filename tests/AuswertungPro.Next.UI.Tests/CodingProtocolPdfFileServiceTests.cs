using System.Collections.Generic;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPdfFileServiceTests
{
    [Fact]
    public void SaveAndOpen_writes_pdf_and_opens_file()
    {
        var writes = new List<(string Path, byte[] Bytes)>();
        var opened = new List<string>();
        var service = new CodingProtocolPdfFileService(
            (path, bytes) =>
            {
                writes.Add((path, bytes));
            },
            path =>
            {
                opened.Add(path);
                return true;
            });

        service.SaveAndOpen("out.pdf", new byte[] { 1, 2, 3 });

        var write = Assert.Single(writes);
        Assert.Equal("out.pdf", write.Path);
        Assert.Equal(new byte[] { 1, 2, 3 }, write.Bytes);
        Assert.Equal(new[] { "out.pdf" }, opened);
    }
}
