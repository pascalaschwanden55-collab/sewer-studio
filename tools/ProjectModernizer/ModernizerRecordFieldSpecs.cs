using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

internal sealed record ModernizerRecordFieldSpec(
    string Field,
    Func<string, bool> Predicate,
    bool IsList = false);

internal static class ModernizerRecordFieldSpecs
{
    public static IReadOnlyList<ModernizerRecordFieldSpec> HaltungPathFields { get; } = new[]
    {
        new ModernizerRecordFieldSpec(FieldKeys.Link, MediaFileTypes.HasVideoExtension),
        new ModernizerRecordFieldSpec(ModernizerProjectKeys.SecondaryVideoLink, MediaFileTypes.HasVideoExtension),
        new ModernizerRecordFieldSpec(FieldKeys.PdfPath, ModernizerStructureFiles.IsPdf),
        new ModernizerRecordFieldSpec(FieldKeys.PdfEigen, ModernizerStructureFiles.IsPdf),
        new ModernizerRecordFieldSpec(FieldKeys.PdfAll, ModernizerStructureFiles.IsPdf, IsList: true)
    };

    public static IReadOnlyList<ModernizerRecordFieldSpec> HaltungDateStampFields { get; } = new[]
    {
        new ModernizerRecordFieldSpec(FieldKeys.PdfPath, ModernizerStructureFiles.IsPdf),
        new ModernizerRecordFieldSpec(FieldKeys.PdfEigen, ModernizerStructureFiles.IsPdf),
        new ModernizerRecordFieldSpec(FieldKeys.Link, MediaFileTypes.HasVideoExtension),
        new ModernizerRecordFieldSpec(ModernizerProjectKeys.SecondaryVideoLink, MediaFileTypes.HasVideoExtension)
    };

    public static IReadOnlyList<ModernizerRecordFieldSpec> SchachtPathFields { get; } = new[]
    {
        new ModernizerRecordFieldSpec(FieldKeys.Link, ModernizerStructureFiles.IsPdf),
        new ModernizerRecordFieldSpec(FieldKeys.PdfPath, ModernizerStructureFiles.IsPdf)
    };
}
