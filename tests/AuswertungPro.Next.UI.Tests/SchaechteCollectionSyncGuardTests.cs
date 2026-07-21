using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteCollectionSyncGuardTests
{
    [Fact]
    public void Controller_fuehrt_Add_Remove_und_Move_unter_gemeinsamem_Lock_aus()
    {
        var collectionLock = new object();
        var records = new LockCheckingCollection(collectionLock)
        {
            new SchachtRecord(),
            new SchachtRecord()
        };
        var controller = new SchaechteRecordCollectionController(
            () => records,
            () => new[] { "Nr." },
            collectionLock);
        records.StartChecking();

        var added = controller.Add();
        Assert.True(controller.TryMoveUp(added));
        Assert.True(controller.TryMoveDown(added));
        Assert.True(controller.TryMoveToPosition(added, 1));

        var renumberedRecords = 0;
        foreach (var record in records)
        {
            record.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(SchachtRecord.ModifiedAtUtc))
                    return;

                Assert.True(
                    Monitor.IsEntered(collectionLock),
                    "Schacht-Nummerierung wurde ohne CollectionLock ausgefuehrt.");
                renumberedRecords++;
            };
        }

        controller.Renumber();
        Assert.True(controller.TryRemove(added, out _));

        Assert.Equal(1, records.CheckedInserts);
        Assert.Equal(3, records.CheckedMoves);
        Assert.Equal(1, records.CheckedRemovals);
        Assert.Equal(3, renumberedRecords);
    }

    [Fact]
    public void Direkte_Mutationen_in_allen_ViewModel_Partials_sind_gesperrt()
    {
        var pagesPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages");
        var mutation = new Regex(
            @"(?:Records|[A-Za-z_][A-Za-z0-9_]*\.SchaechteData)\." +
            @"(Add|Insert|RemoveAt|Remove|Move|Clear)\(");
        var violations = new List<string>();
        var mutationCount = 0;

        foreach (var path in Directory.GetFiles(pagesPath, "SchaechtePageViewModel*.cs"))
        {
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!mutation.IsMatch(lines[index]))
                    continue;

                mutationCount++;
                var windowStart = Math.Max(0, index - 8);
                var isLocked = Enumerable.Range(windowStart, index - windowStart)
                    .Any(lineIndex => lines[lineIndex].Contains("lock (_shell.CollectionLock)"));
                if (!isLocked)
                    violations.Add($"{Path.GetFileName(path)}:{index + 1}: {lines[index].Trim()}");
            }
        }

        Assert.True(mutationCount > 0, "Der ViewModel-Lock-Guard darf nicht leer-gruen werden.");
        Assert.True(
            violations.Count == 0,
            "Direkte Schacht-Mutationen ohne CollectionLock gefunden:\n" + string.Join("\n", violations));
    }

    private sealed class LockCheckingCollection(object collectionLock)
        : ObservableCollection<SchachtRecord>
    {
        private bool _check;

        public int CheckedInserts { get; private set; }
        public int CheckedMoves { get; private set; }
        public int CheckedRemovals { get; private set; }

        public void StartChecking() => _check = true;

        protected override void InsertItem(int index, SchachtRecord item)
        {
            VerifyLock();
            if (_check)
                CheckedInserts++;
            base.InsertItem(index, item);
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            VerifyLock();
            if (_check)
                CheckedMoves++;
            base.MoveItem(oldIndex, newIndex);
        }

        protected override void RemoveItem(int index)
        {
            VerifyLock();
            if (_check)
                CheckedRemovals++;
            base.RemoveItem(index);
        }

        private void VerifyLock()
        {
            if (_check)
                Assert.True(Monitor.IsEntered(collectionLock), "Schacht-Sammlung wurde ohne CollectionLock mutiert.");
        }
    }
}
