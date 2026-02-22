namespace AuswertungPro.Next.UI;

public interface IDialogService
{
    string? OpenFile(string title, string filter, string? initialDirectory = null);
    string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null);
    string[] OpenFiles(string title, string filter);
    string? SelectFolder(string title, string? initialPath = null);
}
