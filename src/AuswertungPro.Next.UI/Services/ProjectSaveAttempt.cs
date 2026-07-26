using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Gemeinsame UI-Grenze fuer Speicherdelegates: sowohl ein <c>false</c> als auch
/// eine Ausnahme bedeuten sichtbar "nicht gespeichert".
/// </summary>
internal static class ProjectSaveAttempt
{
    internal static bool Try(
        Func<bool> saveProject,
        string operation,
        out string? userError)
    {
        ArgumentNullException.ThrowIfNull(saveProject);

        try
        {
            userError = null;
            return saveProject();
        }
        catch (Exception ex)
        {
            userError = UserError.DescribeAndReport(ex, operation);
            return false;
        }
    }

    internal static string ErrorDetails(string? userError)
        => string.IsNullOrWhiteSpace(userError)
            ? string.Empty
            : $"\nSpeicherfehler: {userError}";
}
