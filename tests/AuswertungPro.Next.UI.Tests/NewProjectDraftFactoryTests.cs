using AuswertungPro.Next.Application.Projects;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class NewProjectDraftFactoryTests
{
    [Fact]
    public void Create_setzt_auftraggeber_default_und_leeren_namen()
    {
        var project = NewProjectDraftFactory.Create();

        Assert.Equal("Abwasser Uri", project.Metadata["Auftraggeber"]);
        Assert.Equal(string.Empty, project.Name);
    }
}
