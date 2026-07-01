using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDetailItemFactoryArchitectureTests
{
    [Fact]
    public void DataPage_delegiert_haltungs_detail_items_an_factory()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var dataPage = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var factory = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPageDetailItemFactory.cs"));

        Assert.Contains("private readonly DataPageDetailItemFactory _haltungDetailItemFactory;", dataPage, StringComparison.Ordinal);
        Assert.Contains("new DataPageDetailItemFactory(", dataPage, StringComparison.Ordinal);
        Assert.Contains("FieldCatalog.Get(fieldName)", factory, StringComparison.Ordinal);
        Assert.Contains("new RecordDetailItem(", factory, StringComparison.Ordinal);

        var method = SourceTextTestHelpers.ExtractMethodBody(
            dataPage,
            "private RecordDetailItem CreateHaltungDetailItem(string fieldName, HaltungRecord record)");

        Assert.Contains("_haltungDetailItemFactory.Create(fieldName, record);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("FieldCatalog.Get", method, StringComparison.Ordinal);
        Assert.DoesNotContain("FieldCatalog.GetComboItems", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new RecordDetailItem", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveManagedComboSpec", method, StringComparison.Ordinal);
    }
}
