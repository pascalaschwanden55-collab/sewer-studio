using System;
using System.IO;

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

    private static string ExtractMethod(string source, string marker)
    {
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidOperationException($"Method marker not found: {marker}");

        var openBrace = source.IndexOf('{', markerIndex);
        if (openBrace < 0)
            throw new InvalidOperationException($"Method has no body: {marker}");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
                continue;
            }

            if (source[i] != '}')
                continue;

            depth--;
            if (depth == 0)
                return source.Substring(markerIndex, i - markerIndex + 1);
        }

        throw new InvalidOperationException($"Method body is incomplete: {marker}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
