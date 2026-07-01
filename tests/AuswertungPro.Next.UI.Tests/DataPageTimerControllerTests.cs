using System.Threading;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageTimerControllerTests
{
    [Fact]
    public void ScheduleAutoSave_on_each_change_marks_dirty_and_saves()
    {
        RunOnStaThread(() =>
        {
            var calls = new List<string>();
            var controller = new DataPageTimerController(
                setSaveStatus: value => calls.Add($"status:{value}"),
                setSaveVisible: value => calls.Add($"visible:{value}"));

            controller.ScheduleAutoSave(
                AutoSaveMode.OnEachChange,
                markDirty: () => calls.Add("dirty"),
                save: () => calls.Add("save"));

            Assert.Equal(new[] { "dirty", "save" }, calls);
        });
    }

    [Fact]
    public void ShowSaveStatus_sets_text_and_visibility()
    {
        RunOnStaThread(() =>
        {
            var status = "";
            var visible = false;
            var controller = new DataPageTimerController(
                setSaveStatus: value => status = value,
                setSaveVisible: value => visible = value);

            controller.ShowSaveStatus("Automatisch gespeichert");

            Assert.Equal("Automatisch gespeichert", status);
            Assert.True(visible);
        });
    }

    [Fact]
    public void Stop_can_be_called_repeatedly()
    {
        RunOnStaThread(() =>
        {
            var controller = new DataPageTimerController(_ => { }, _ => { });

            controller.Stop();
            controller.Stop();
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
