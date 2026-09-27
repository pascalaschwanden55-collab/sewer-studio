using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Hydraulik;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;

var engine = new CostOptimizationEngine();
var costInput = new CostCalcInput { Measure = "Kurzliner", DiameterMm = 300, LengthMeter = 10, DepthM = 3, Access = AccessDifficulty.Easy };
var exact = engine.Calculate(costInput);
var descriptive = engine.Calculate(costInput with { Measure = "Kurzliner DN 300" });
var shorter = engine.Calculate(costInput with { Measure = "Kurzliner DN 300", LengthMeter = 1 });
var h = new HaltungRecord();
h.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Manual, true);
h.SetFieldValue(FieldKeys.SlopePromille, "5", FieldSource.Manual, true);
h.SetFieldValue(FieldKeys.PipeMaterial, "Normalbeton", FieldSource.Manual, true);
var previousPlastic = DataPageHydraulikReportCalculator.BuildReportCalculation(h, new HydraulikPanelSettings { MaterialKey = "PVC/PE" });
var previousConcrete = DataPageHydraulikReportCalculator.BuildReportCalculation(h, new HydraulikPanelSettings { MaterialKey = "Beton" });
var alias = new SchachtRecord();
alias.SetFieldValue("STATUS", "in_Betrieb", FieldSource.Xtf, false);
var result = new {
    cost = new { exactMeasure = costInput.Measure, exactCHF = exact.Expected, descriptiveMeasure = "Kurzliner DN 300", descriptiveCHF = descriptive.Expected, descriptiveOneMeterCHF = shorter.Expected },
    hydraulics = new { storedMaterial = h.GetFieldValue(FieldKeys.PipeMaterial), previousPlastic = new { previousPlastic?.Material, previousPlastic?.Kb, previousPlastic?.Q_V }, previousConcrete = new { previousConcrete?.Material, previousConcrete?.Kb, previousConcrete?.Q_V } },
    shaftAlias = new { field = SchachtFeldnamen.Feld(alias, FieldKeys.OperatingStatus), storedValue = alias.GetFieldValue("STATUS") }
};
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
