using AuswertungPro.Next.Application.Common;
using IVsaMediaPathResolver = AuswertungPro.Next.Application.Import.IVsaMediaPathResolver;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

public sealed partial class LegacyXtfImportService
{
    private readonly string? _archiveRoot;
    private readonly string? _legacyArchiveRoot;
    private readonly IVsaMediaPathResolver _mediaPaths;
    private readonly LegacyXtfSourceReader _sourceReader = new(new SafeXmlDocumentLoader());
    private readonly IM150SourceFileReader _m150SourceFiles;
    private readonly IM150MdbRowReader _m150MdbRows;

    public LegacyXtfImportService()
    {
        // Direkte Parser-Nutzung schreibt keine Dateien neben das Programm.
        _mediaPaths = VsaMediaPathResolver.Current;
        _m150SourceFiles = M150SourceFileReader.Current;
        _m150MdbRows = M150MdbRowReader.Current;
    }

    public LegacyXtfImportService(string archiveRoot, string? legacyArchiveRoot = null)
        : this(
            archiveRoot,
            legacyArchiveRoot,
            VsaMediaPathResolver.Current,
            M150SourceFileReader.Current,
            M150MdbRowReader.Current)
    {
    }

    internal LegacyXtfImportService(
        string archiveRoot,
        string? legacyArchiveRoot,
        IVsaMediaPathResolver mediaPaths,
        IM150SourceFileReader m150SourceFiles,
        IM150MdbRowReader m150MdbRows,
        ISafeXmlDocumentLoader? xmlLoader = null)
    {
        _archiveRoot = string.IsNullOrWhiteSpace(archiveRoot)
            ? throw new ArgumentException("Der XTF-Archivordner fehlt.", nameof(archiveRoot))
            : Path.GetFullPath(archiveRoot);
        _legacyArchiveRoot = string.IsNullOrWhiteSpace(legacyArchiveRoot)
            ? null
            : Path.GetFullPath(legacyArchiveRoot);
        _mediaPaths = mediaPaths ?? throw new ArgumentNullException(nameof(mediaPaths));
        _m150SourceFiles = m150SourceFiles ?? throw new ArgumentNullException(nameof(m150SourceFiles));
        _m150MdbRows = m150MdbRows ?? throw new ArgumentNullException(nameof(m150MdbRows));
        _sourceReader = new LegacyXtfSourceReader(xmlLoader ?? new SafeXmlDocumentLoader());
    }

    public static LegacyXtfImportService CreateForApplication()
        => CreateForApplication(VsaMediaPathResolver.Current);

    internal static LegacyXtfImportService CreateForApplication(IVsaMediaPathResolver mediaPaths) =>
        CreateForApplication(
            mediaPaths,
            M150SourceFileReader.Current,
            M150MdbRowReader.Current);

    internal static LegacyXtfImportService CreateForApplication(
        IVsaMediaPathResolver mediaPaths,
        IM150SourceFileReader m150SourceFiles,
        IM150MdbRowReader m150MdbRows) => new(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SewerStudio",
                "Rohdaten",
                "xtf_imports"),
            Path.Combine(AppContext.BaseDirectory, "Rohdaten", "xtf_imports"),
            mediaPaths,
            m150SourceFiles,
            m150MdbRows);
}
