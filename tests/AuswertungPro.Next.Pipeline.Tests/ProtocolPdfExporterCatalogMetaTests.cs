using System.Reflection;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPdfExporterCatalogMetaTests
{
    [Fact]
    public void Photo_caption_keeps_original_code_and_hides_catalog_metadata()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAGA",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BAGA",
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["catalog.source"] = "IKAS-ILI",
                    ["catalog.canonicalCode"] = "BAG",
                    ["catalog.standardAnnotation"] = "A",
                    ["vsa.q1"] = "12 mm"
                }
            }
        };

        var caption = InvokePrivateString("BuildPhotoCaptionLine2", entry);

        Assert.StartsWith("BAGA", caption, StringComparison.Ordinal);
        Assert.Contains("Q1=12 mm", caption, StringComparison.Ordinal);
        Assert.DoesNotContain("catalog.", caption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IKAS-ILI", caption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canonicalCode", caption, StringComparison.OrdinalIgnoreCase);
    }

    private static string InvokePrivateString(string methodName, ProtocolEntry entry)
    {
        var method = typeof(ProtocolPdfExporter).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<string>(method!.Invoke(null, [entry]));
    }
}
