namespace AuswertungPro.Next.UI.Ai;

public static class VsaCodeExplorerDispatchWorkflow
{
    public static void DispatchPropertyChanged(
        bool isOnUiThread,
        Action apply,
        Action<Action> postToUi)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(postToUi);

        if (isOnUiThread)
        {
            apply();
            return;
        }

        postToUi(apply);
    }

    public static void ScheduleColumnRender(
        Action render,
        Action<Action> postToUi)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(postToUi);

        postToUi(render);
    }
}
