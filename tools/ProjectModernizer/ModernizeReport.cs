internal sealed class ModernizeReport
{
    public int FoldersCreated { get; set; }
    public int ImportCopied { get; set; }
    public int ImportSkipped { get; set; }
    public int HaltungFilesCopied { get; set; }
    public int SchachtFilesCopied { get; set; }
    public int PlanFilesCopied { get; set; }
    public int PhotoFilesCopied { get; set; }
    public int FlattenedFiles { get; set; }
    public int FoldersRemoved { get; set; }
    public int CentralPhotos { get; set; }
    public int ProtocolPhotosRepaired { get; set; }
    public int ReusedFiles { get; set; }
    public int RelinkedPaths { get; set; }
    public int MetadataUpdated { get; set; }
    public int ExternalLinksRemoved { get; set; }
    public int SnapshotLinksRemoved { get; set; }
    public int UnresolvedPaths { get; set; }
    public int CopyErrors { get; set; }
    public List<string> Messages { get; } = new();
}
