using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class VsaCodeResolverTestCollection
{
    public const string Name = "VSA-CodeResolver static catalog";
}
