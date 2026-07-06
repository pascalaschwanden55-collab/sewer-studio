internal sealed class TempProject : IDisposable
{
    private TempProject(string root)
    {
        Root = root;
        ProjectFolder = Path.Combine(root, "project");
        Directory.CreateDirectory(ProjectFolder);
    }

    public string Root { get; }

    public string ProjectFolder { get; }

    public static TempProject Create()
        => new(Path.Combine(Path.GetTempPath(), "ProjectModernizer.Tests", Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        if (!Directory.Exists(Root))
            return;

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Test cleanup should not hide assertion failures.
        }
    }
}
