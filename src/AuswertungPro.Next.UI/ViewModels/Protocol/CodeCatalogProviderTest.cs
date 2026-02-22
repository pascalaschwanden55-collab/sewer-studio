using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public static class CodeCatalogProviderTest
{
    public static void RunTest(AppProtocol.ICodeCatalogProvider provider)
    {
        var allowed = provider.AllowedCodes();
        Console.WriteLine($"Codes: {allowed.Count}");
        if (provider.TryGet(allowed.FirstOrDefault() ?? string.Empty, out var def))
            Console.WriteLine($"Definition: {def.Code} - {def.Title}");
    }
}
