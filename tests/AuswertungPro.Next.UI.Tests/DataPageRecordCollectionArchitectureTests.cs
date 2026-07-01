using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRecordCollectionArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_zeilenverwaltung_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        Assert.Contains("private readonly DataPageRecordCollectionController _recordCollectionController;", source, StringComparison.Ordinal);

        AssertDelegates(source, "private bool CanMoveUp()", "_recordCollectionController.CanMoveUp();");
        AssertDelegates(source, "private bool CanMoveDown()", "_recordCollectionController.CanMoveDown();");
        AssertDelegates(source, "private void Add()", "_recordCollectionController.Add();");
        AssertDelegates(source, "private void Remove()", "_recordCollectionController.Remove();");
        AssertDelegates(source, "private void MoveUp()", "_recordCollectionController.MoveUp();");
        AssertDelegates(source, "private void MoveDown()", "_recordCollectionController.MoveDown();");
        AssertDelegates(source, "public bool MoveToPosition(int targetPosition)", "_recordCollectionController.MoveToPosition(targetPosition);");

        var viewModelMethods = string.Concat(
            ExtractMethod(source, "private void Add()"),
            ExtractMethod(source, "private void Remove()"),
            ExtractMethod(source, "private void MoveUp()"),
            ExtractMethod(source, "private void MoveDown()"),
            ExtractMethod(source, "public bool MoveToPosition(int targetPosition)"));

        Assert.DoesNotContain("CreateNewRecord", viewModelMethods, StringComparison.Ordinal);
        Assert.DoesNotContain("AddRecord", viewModelMethods, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveRecord", viewModelMethods, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.Confirm", viewModelMethods, StringComparison.Ordinal);
        Assert.DoesNotContain("DataPageRecordOrderController.TryMove", viewModelMethods, StringComparison.Ordinal);
    }

    private static void AssertDelegates(string source, string marker, string call)
    {
        var method = ExtractMethod(source, marker);
        Assert.Contains(call, method, StringComparison.Ordinal);
    }

}
