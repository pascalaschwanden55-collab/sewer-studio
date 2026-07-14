using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Tests.Media;

public sealed class InspectionProtocolFileLocatorTests
{
    [Fact]
    public void Dienst_bevorzugt_Inspektionsprotokoll_vor_Lageplan()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "InspectionProtocolFileLocatorTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var holdingDirectory = Directory.CreateDirectory(Path.Combine(root, "06-001")).FullName;
            var protocol = Path.Combine(holdingDirectory, "A_Protokoll_06-001.pdf");
            var plan = Path.Combine(holdingDirectory, "Z_Plan_06-001.pdf");
            File.WriteAllText(protocol, "Haltungsinspektion 06-001 Leitungsbericht");
            File.WriteAllText(plan, "Leitungsende 06-001 Dachwasser angeschlossen");
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "06-001", FieldSource.Manual, userEdited: true);
            IInspectionProtocolFileLocator locator = new InspectionProtocolFileLocator();

            var found = locator.FindProtocolPath(
                record,
                resolvedLink: null,
                initialFolder: root,
                projectPath: null,
                storedFilesRaw: null);

            Assert.Equal(protocol, found, ignoreCase: true);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
