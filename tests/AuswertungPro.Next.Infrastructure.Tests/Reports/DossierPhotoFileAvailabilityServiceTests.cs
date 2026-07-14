using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Reports;

namespace AuswertungPro.Next.Infrastructure.Tests.Reports;

public sealed class DossierPhotoFileAvailabilityServiceTests
{
    [Fact]
    public void Dienst_findet_existierendes_relatives_Protokollfoto()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "DossierPhotoFileAvailabilityServiceTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Fotos"));
            File.WriteAllText(Path.Combine(root, "Fotos", "vorhanden.jpg"), "foto");
            var entry = new ProtocolEntry();
            entry.FotoPaths.Add("Fotos/fehlt.jpg");
            entry.FotoPaths.Add("Fotos/vorhanden.jpg");
            var record = new HaltungRecord
            {
                Protocol = new ProtocolDocument
                {
                    Current = new ProtocolRevision { Entries = { entry } }
                }
            };
            IDossierPhotoAvailabilityService service = new DossierPhotoFileAvailabilityService();

            var available = service.HasPrintablePhotos(record, root);

            Assert.True(available);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
