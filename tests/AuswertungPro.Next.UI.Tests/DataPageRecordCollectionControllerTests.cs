using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRecordCollectionControllerTests
{
    [Fact]
    public void Add_erstellt_datensatz_waehlt_ihn_aus_und_plant_autosave()
    {
        var project = new Project();
        HaltungRecord? selected = null;
        var autosaves = 0;
        var controller = CreateController(
            project,
            getSelected: () => selected,
            setSelected: value => selected = value,
            scheduleAutoSave: () => autosaves++);

        controller.Add();

        var record = Assert.Single(project.Data);
        Assert.Same(record, selected);
        Assert.Equal("1", record.GetFieldValue("NR"));
        Assert.True(project.Dirty);
        Assert.Equal(1, autosaves);
    }

    [Fact]
    public void Remove_ignoriert_fehlende_auswahl_ohne_rueckfrage()
    {
        var project = new Project();
        var confirmCalls = 0;
        var autosaves = 0;
        var controller = CreateController(
            project,
            getSelected: () => null,
            confirmDelete: (_, _) =>
            {
                confirmCalls++;
                return true;
            },
            scheduleAutoSave: () => autosaves++);

        controller.Remove();

        Assert.Equal(0, confirmCalls);
        Assert.Equal(0, autosaves);
    }

    [Fact]
    public void Remove_bricht_ab_wenn_bestaetigung_verneint()
    {
        var project = ProjectWithRecords("A", "B");
        var selected = project.Data[0];
        var controller = CreateController(
            project,
            getSelected: () => selected,
            confirmDelete: (_, _) => false);

        controller.Remove();

        Assert.Equal(new[] { "A", "B" }, project.Data.Select(r => r.GetFieldValue("Haltungsname")).ToArray());
        Assert.Same(selected, project.Data[0]);
    }

    [Fact]
    public void Remove_loescht_auswahl_und_waehlt_naechsten_datensatz()
    {
        var project = ProjectWithRecords("A", "B", "C");
        HaltungRecord? selected = project.Data[1];
        var autosaves = 0;
        var controller = CreateController(
            project,
            getSelected: () => selected,
            setSelected: value => selected = value,
            scheduleAutoSave: () => autosaves++);

        controller.Remove();

        Assert.Equal(new[] { "A", "C" }, project.Data.Select(r => r.GetFieldValue("Haltungsname")).ToArray());
        Assert.Equal("C", selected?.GetFieldValue("Haltungsname"));
        Assert.True(project.Dirty);
        Assert.Equal(1, autosaves);
    }

    [Fact]
    public void Remove_letzten_datensatz_setzt_auswahl_auf_null()
    {
        var project = ProjectWithRecords("A");
        HaltungRecord? selected = project.Data[0];
        var controller = CreateController(
            project,
            getSelected: () => selected,
            setSelected: value => selected = value);

        controller.Remove();

        Assert.Empty(project.Data);
        Assert.Null(selected);
    }

    [Fact]
    public void MoveToPosition_markiert_dirty_und_meldet_reihenfolge_nur_bei_bewegung()
    {
        var project = ProjectWithRecords("A", "B", "C");
        var selected = project.Data[1];
        var orderChanged = 0;
        var autosaves = 0;
        var controller = CreateController(
            project,
            getSelected: () => selected,
            notifyRecordsOrderChanged: () => orderChanged++,
            scheduleAutoSave: () => autosaves++);

        var moved = controller.MoveToPosition(3);
        var unchanged = controller.MoveToPosition(3);

        Assert.True(moved);
        Assert.False(unchanged);
        Assert.Equal(new[] { "A", "C", "B" }, project.Data.Select(r => r.GetFieldValue("Haltungsname")).ToArray());
        Assert.True(project.Dirty);
        Assert.Equal(1, orderChanged);
        Assert.Equal(1, autosaves);
    }

    [Fact]
    public void CanMoveUp_und_CanMoveDown_delegieren_auf_auswahl()
    {
        var project = ProjectWithRecords("A", "B");
        var selected = project.Data[0];
        var controller = CreateController(project, getSelected: () => selected);

        Assert.False(controller.CanMoveUp());
        Assert.True(controller.CanMoveDown());
    }

    private static DataPageRecordCollectionController CreateController(
        Project project,
        Func<HaltungRecord?>? getSelected = null,
        Action<HaltungRecord?>? setSelected = null,
        Func<string, string, bool>? confirmDelete = null,
        Action? notifyRecordsOrderChanged = null,
        Action? scheduleAutoSave = null)
        => new(
            getProject: () => project,
            getSelected ?? (() => null),
            setSelected ?? (_ => { }),
            confirmDelete ?? ((_, _) => true),
            notifyRecordsOrderChanged ?? (() => { }),
            scheduleAutoSave ?? (() => { }));

    private static Project ProjectWithRecords(params string[] names)
    {
        var project = new Project();
        foreach (var name in names)
        {
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
            project.Data.Add(record);
        }

        project.Dirty = false;
        return project;
    }
}
