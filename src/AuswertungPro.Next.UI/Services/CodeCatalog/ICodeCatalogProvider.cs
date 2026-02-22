using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Services.CodeCatalog;

public interface ILocalCodeCatalogProvider
{
    IReadOnlyList<string> AllowedCodes { get; }
    CodeDef? Get(string code);
    IReadOnlyList<CodeDef> Search(string query, int max = 50);
    CodeCatalogTreeNode GetTree();
    DateTimeOffset LoadedAt { get; }
}
