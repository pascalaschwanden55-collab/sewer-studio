using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

/// <summary>Dateizugriff fuer projektbezogene Kosten.</summary>
public interface IProjectCostStoreRepository
{
    ProjectCostStore Load(string? projectPath);
    ProjectCostStore Load(string? projectPath, out string? loadError);
    bool Save(string? projectPath, ProjectCostStore store, out string? error);
    string GetStorePath(string projectDirectory);
}

/// <summary>Dateizugriff fuer den Kostenkatalog.</summary>
public interface ICostCatalogStore
{
    string? LastUserOverrideLoadError { get; }

    CostCatalog LoadMerged(string? projectPath);
    CostCatalog LoadDefault(string? projectPath);
    CostCatalog LoadUserOverrides();
    bool SaveUserOverrides(CostCatalog catalog, out string error);
    bool SaveUserOverrides(CostCatalog catalog, string? projectPath, out string error);
    bool ResetUserOverrides(out string error);
    CostCatalogItem? FindByPosition(CostCatalog catalog, string position);
    bool UpsertByPosition(
        CostCatalog catalog,
        string position,
        decimal? unitPrice,
        string? unit,
        bool active,
        IEnumerable<string>? aliases);
}

/// <summary>Dateizugriff fuer Massnahmenvorlagen.</summary>
public interface IMeasureTemplateStore
{
    string? LastUserOverrideLoadError { get; }

    MeasureTemplateCatalog LoadMerged(string? projectPath);
    MeasureTemplateCatalog LoadDefault(string? projectPath);
    MeasureTemplateCatalog LoadUserOverrides();
    bool SaveUserOverrides(MeasureTemplateCatalog catalog, out string error);
    bool ResetUserOverrides(out string error);
    bool UpsertUserTemplate(MeasureTemplate template, out string error);
    bool DisableUserTemplate(string idOrName, out string error);
    bool DeleteUserTemplate(string idOrName, out string error);
}

/// <summary>Dateizugriff fuer Positionsvorlagen.</summary>
public interface IPositionTemplateStore
{
    string? LastUserOverrideLoadError { get; }

    PositionTemplateCatalog Load(string? projectPath);
    PositionTemplateCatalog LoadMerged(string? projectPath);
    bool SaveUserOverride(PositionTemplateCatalog catalog, out string? error);
}

/// <summary>
/// Zusammengehoerende Speicher fuer Kostenrechner und Sanierungsmatrizen.
/// Das Paket verhindert, dass Katalog, Vorlagen und Projektkosten getrennt verdrahtet werden.
/// </summary>
public sealed class CostCalculationStores
{
    public CostCalculationStores(
        ICostCatalogStore catalog,
        IMeasureTemplateStore templates,
        IProjectCostStoreRepository projectCosts)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Templates = templates ?? throw new ArgumentNullException(nameof(templates));
        ProjectCosts = projectCosts ?? throw new ArgumentNullException(nameof(projectCosts));
    }

    public ICostCatalogStore Catalog { get; }
    public IMeasureTemplateStore Templates { get; }
    public IProjectCostStoreRepository ProjectCosts { get; }
}

/// <summary>
/// Erzeugt frische Speicherinstanzen am zentralen Zusammensetzungspunkt.
/// Katalog-Speicher bleiben absichtlich nicht global, weil sie Ladefehler pro Fenster merken.
/// </summary>
public interface ICostStoreFactory
{
    IProjectCostStoreRepository CreateProjectCostStore(string fileName = "costs.json");
    ICostCatalogStore CreateCostCatalogStore(string? userOverridePath = null);
    IMeasureTemplateStore CreateMeasureTemplateStore(string? userOverridePath = null);
    IPositionTemplateStore CreatePositionTemplateStore(string? userOverridePath = null);
    CostCalculationStores CreateCalculationStores(string projectCostFileName = "costs.json");
}
