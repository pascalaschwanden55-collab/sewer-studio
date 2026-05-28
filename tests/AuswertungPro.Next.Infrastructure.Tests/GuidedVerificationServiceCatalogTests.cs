using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GuidedVerificationServiceCatalogTests
{
    [Fact]
    public void Code_description_is_resolved_from_catalog()
    {
        var catalog = new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition { Code = "BAH", Title = "Schadhafter Anschluss" },
            new CodeDefinition { Code = "BBA", Title = "Wurzeln" }
        });

        Assert.Equal("Schadhafter Anschluss", GuidedVerificationService.ResolveCodeDescription("BAH", catalog));
        Assert.Equal("Wurzeln", GuidedVerificationService.ResolveCodeDescription("BBA", catalog));
    }

    [Fact]
    public void Unknown_code_description_falls_back_to_code()
    {
        var catalog = new InMemoryCodeCatalogProvider(Array.Empty<CodeDefinition>());

        Assert.Equal("ZZZ", GuidedVerificationService.ResolveCodeDescription("ZZZ", catalog));
    }

    private sealed class InMemoryCodeCatalogProvider : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _codes;

        public InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
            => _codes = codes;

        public IReadOnlyList<CodeDefinition> GetAll() => _codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                  ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
