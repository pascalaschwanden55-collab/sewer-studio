using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class KanalImportDistributorTests
{
    [Fact]
    public void SelectPrimaryProtocolPdf_BevorzugtBasisPdf_GegenDuplikatUndPlan()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"primarypdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            // Basis-Protokoll (groß), Zweit-Export (_1, groß, Duplikat-Variant), Plan (klein), split_ (ausgeschlossen)
            var basis = Path.Combine(dir, "Meien_Fuerlauwi_40671_0626.pdf");
            var dup   = Path.Combine(dir, "Meien_Fuerlauwi_40671_0626_1.pdf");
            var plan  = Path.Combine(dir, "Meien_Plan.pdf");
            var split = Path.Combine(dir, "split_egal.pdf");
            File.WriteAllBytes(basis, new byte[3000]);
            File.WriteAllBytes(dup,   new byte[3200]);   // sogar etwas größer, darf aber NICHT gewinnen (Duplikat-Variant)
            File.WriteAllBytes(plan,  new byte[500]);
            File.WriteAllBytes(split, new byte[9000]);

            var primary = KanalImportDistributor.SelectPrimaryProtocolPdf(dir);

            Assert.Equal(basis, primary, ignoreCase: true);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void SelectPrimaryProtocolPdf_LiefertNull_OhnePdfs()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"primarypdf-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(KanalImportDistributor.SelectPrimaryProtocolPdf(dir));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void SelectPrimaryProtocolPdf_BevorzugtTvProtokollVorPlanUndDichtheit()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"primarypdf-typ-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var plan = Path.Combine(dir, "AWU_Plan.pdf");
            var dp = Path.Combine(dir, "048473_DP.pdf");
            var tv = Path.Combine(dir, "Gesamtprotokoll.pdf");
            File.WriteAllText(plan, "DW\nLeitungsende Veschlossen\nDachwasser angeschlossen");
            File.WriteAllText(dp, "Dichtheitspruefung nach SIA190:2017 / VSA RL Dicht:2023\nvon Schacht: 1\nnach Schacht: 2");
            File.WriteAllText(tv, "Haltungsinspektion - 22.06.2026 - 10081-8993\nLeitungsbericht\n0.00 BCD Rohranfang");

            var primary = KanalImportDistributor.SelectPrimaryProtocolPdf(dir);

            Assert.Equal(tv, primary, ignoreCase: true);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }
}
