using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class PlanPdfImporterTests
{
    [Fact]
    public void Service_verwendet_injizierte_Dokumenterkennung()
    {
        var root = CreateTempRoot();
        try
        {
            var projectDir = Path.Combine(root, "projekt");
            var archivedPdfDir = Path.Combine(projectDir, ProjectStructure.Importdateien, ProjectStructure.PdfDir);
            Directory.CreateDirectory(archivedPdfDir);
            var plan = Path.Combine(archivedPdfDir, "erzwingen.pdf");
            var skip = Path.Combine(archivedPdfDir, "ignorieren.pdf");
            File.WriteAllBytes(plan, [1, 2, 3]);
            File.WriteAllBytes(skip, [4, 5, 6]);
            var checkedFiles = new List<string>();
            var service = new PlanPdfImportService(path =>
            {
                checkedFiles.Add(Path.GetFileName(path));
                return path.EndsWith("erzwingen.pdf", StringComparison.OrdinalIgnoreCase);
            });

            var result = service.ImportFromArchivedPdfFolder(archivedPdfDir, projectDir);

            Assert.Equal(1, result.Copied);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(["erzwingen.pdf", "ignorieren.pdf"], checkedFiles.OrderBy(name => name));
            Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(projectDir, "Pläne", "erzwingen.pdf")));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void ImportFromArchivedPdfFolder_KopiertPlanUndIgnoriertProtokoll()
    {
        var root = CreateTempRoot();
        try
        {
            var projectDir = Path.Combine(root, "projekt");
            var archivedPdfDir = Path.Combine(projectDir, ProjectStructure.Importdateien, ProjectStructure.PdfDir);
            Directory.CreateDirectory(archivedPdfDir);
            File.WriteAllText(Path.Combine(archivedPdfDir, "AWU_Altdorf_Vorstadt_Plan.pdf"),
                "DW\nLeitungsende Veschlossen\nDachwasser angeschlossen");
            File.WriteAllText(Path.Combine(archivedPdfDir, "Altdorf_Vorstadt_0626.pdf"),
                "Leitungs-Stammdaten\nLeitung 1633.01-79226\nLeitungsbericht");
            File.WriteAllText(Path.Combine(archivedPdfDir, "Altdorf_DP.pdf"),
                "Dichtheitspruefung nach SIA190:2017 / VSA RL Dicht:2023\nvon Schacht: 1\nnach Schacht: 2");

            var result = PlanPdfImporter.ImportFromArchivedPdfFolder(archivedPdfDir, projectDir);

            Assert.Equal(1, result.Copied);
            Assert.Equal(2, result.Skipped);
            Assert.True(File.Exists(Path.Combine(projectDir, "Pläne", "AWU_Altdorf_Vorstadt_Plan.pdf")));
            Assert.False(File.Exists(Path.Combine(projectDir, "Pläne", "Altdorf_Vorstadt_0626.pdf")));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void ImportFromArchivedPdfFolder_IstIdempotentBeiGleicherDatei()
    {
        var root = CreateTempRoot();
        try
        {
            var projectDir = Path.Combine(root, "projekt");
            var archivedPdfDir = Path.Combine(projectDir, ProjectStructure.Importdateien, ProjectStructure.PdfDir);
            Directory.CreateDirectory(archivedPdfDir);
            File.WriteAllText(Path.Combine(archivedPdfDir, "Netzplan.pdf"), "Plan\nLeitungsende Veschlossen");

            var first = PlanPdfImporter.ImportFromArchivedPdfFolder(archivedPdfDir, projectDir);
            var second = PlanPdfImporter.ImportFromArchivedPdfFolder(archivedPdfDir, projectDir);

            Assert.Equal(1, first.Copied);
            Assert.Equal(0, second.Copied);
            Assert.Equal(1, second.Reused);
            Assert.Single(Directory.GetFiles(Path.Combine(projectDir, "Pläne"), "*.pdf"));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void ImportFromArchivedPdfFolder_KopiertKollidierendenPlanMitSicheremNamen()
    {
        var root = CreateTempRoot();
        try
        {
            var projectDir = Path.Combine(root, "projekt");
            var archivedPdfDir = Path.Combine(projectDir, ProjectStructure.Importdateien, ProjectStructure.PdfDir);
            var plaeneDir = Path.Combine(projectDir, "Pläne");
            Directory.CreateDirectory(archivedPdfDir);
            Directory.CreateDirectory(plaeneDir);
            File.WriteAllText(Path.Combine(archivedPdfDir, "Plan.pdf"), "Plan\nLeitungsende Veschlossen\nneu");
            File.WriteAllText(Path.Combine(plaeneDir, "Plan.pdf"), "alt");

            var result = PlanPdfImporter.ImportFromArchivedPdfFolder(archivedPdfDir, projectDir);

            Assert.Equal(1, result.Copied);
            Assert.True(File.Exists(Path.Combine(plaeneDir, "Plan_1.pdf")));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"plan-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}
