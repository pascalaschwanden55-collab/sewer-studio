using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

internal static class VsaResolverTestCatalog
{
    public static void ConfigureDefault()
    {
        VsaCodeResolver.ConfigureCatalog(CreateDefault());
    }

    public static ICodeCatalogProvider CreateDefault()
        => new InMemoryCodeCatalogProvider(new[]
        {
            Code("BAB", "Risse"),
            Code("BAC", "Leitungsbruch/Einsturz"),
            Code("BAF", "Deformation"),
            Code("BAH", "Versatz"),
            Code("BAI", "Einragung"),
            Code("BAJ", "Oberflaechenschaden"),
            Code("BBA", "Wurzeln", requiresRange: true),
            Code("BBB", "Anhaftende Stoffe"),
            Code("BBC", "Ablagerungen"),
            Code("BCA", "Seitlicher Anschluss"),
            Code("BCC", "Bogen"),
            Code("BCD", "Rohranfang"),
            Code("BCE", "Rohrende"),
            Code("BDDC", "Wasserstand")
        });

    private static CodeDefinition Code(string code, string title, bool requiresRange = false)
        => new()
        {
            Code = code,
            Title = title,
            CanonicalCode = code,
            Source = VsaKekCatalogSources.Ili,
            RequiresRange = requiresRange,
            IsSelectable = true
        };

    private sealed class InMemoryCodeCatalogProvider : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _codes;

        public InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
        {
            _codes = codes;
        }

        public IReadOnlyList<CodeDefinition> GetAll() => _codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new NotSupportedException();

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
