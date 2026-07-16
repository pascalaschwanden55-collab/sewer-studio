using AuswertungPro.Next.Application.Costs;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class CostStoreFactory : ICostStoreFactory
{
    public IProjectCostStoreRepository CreateProjectCostStore(string fileName = "costs.json")
        => new ProjectCostStoreRepository(fileName);

    public ICostCatalogStore CreateCostCatalogStore(string? userOverridePath = null)
        => new CostCatalogStore(userOverridePath);

    public IMeasureTemplateStore CreateMeasureTemplateStore(string? userOverridePath = null)
        => new MeasureTemplateStore(userOverridePath);

    public IPositionTemplateStore CreatePositionTemplateStore(string? userOverridePath = null)
        => new PositionTemplateStore(userOverridePath);

    public CostCalculationStores CreateCalculationStores(string projectCostFileName = "costs.json")
        => new(
            CreateCostCatalogStore(),
            CreateMeasureTemplateStore(),
            CreateProjectCostStore(projectCostFileName));
}
