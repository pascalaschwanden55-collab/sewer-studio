using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDropdownCommandFactoryTests
{
    [Fact]
    public void Create_wires_each_dropdown_command_to_its_action_group()
    {
        var calls = new List<string>();

        var commands = DataPageDropdownCommandFactory.Create(
            Actions("sanieren", calls),
            Actions("eigentuemer", calls),
            Actions("pruefungsresultat", calls),
            Actions("referenzpruefung", calls),
            Actions("massnahmen", calls));

        ExecuteAll(commands.Sanieren, "sanieren-value");
        ExecuteAll(commands.Eigentuemer, "eigentuemer-value");
        ExecuteAll(commands.Pruefungsresultat, "pruefungsresultat-value");
        ExecuteAll(commands.Referenzpruefung, "referenzpruefung-value");
        ExecuteAll(commands.EmpfohleneSanierungsmassnahmen, "massnahmen-value");

        Assert.Equal(
            new[]
            {
                "sanieren:edit",
                "sanieren:preview",
                "sanieren:reset",
                "sanieren:add:sanieren-value",
                "sanieren:remove:sanieren-value",
                "eigentuemer:edit",
                "eigentuemer:preview",
                "eigentuemer:reset",
                "eigentuemer:add:eigentuemer-value",
                "eigentuemer:remove:eigentuemer-value",
                "pruefungsresultat:edit",
                "pruefungsresultat:preview",
                "pruefungsresultat:reset",
                "pruefungsresultat:add:pruefungsresultat-value",
                "pruefungsresultat:remove:pruefungsresultat-value",
                "referenzpruefung:edit",
                "referenzpruefung:preview",
                "referenzpruefung:reset",
                "referenzpruefung:add:referenzpruefung-value",
                "referenzpruefung:remove:referenzpruefung-value",
                "massnahmen:edit",
                "massnahmen:preview",
                "massnahmen:reset",
                "massnahmen:add:massnahmen-value",
                "massnahmen:remove:massnahmen-value"
            },
            calls);
    }

    private static DropdownCommandActions Actions(string name, List<string> calls)
        => new(
            Edit: () => calls.Add($"{name}:edit"),
            Preview: () => calls.Add($"{name}:preview"),
            Reset: () => calls.Add($"{name}:reset"),
            Add: value => calls.Add($"{name}:add:{value}"),
            Remove: value => calls.Add($"{name}:remove:{value}"));

    private static void ExecuteAll(DropdownCommandGroup group, string value)
    {
        group.Edit.Execute(null);
        group.Preview.Execute(null);
        group.Reset.Execute(null);
        group.Add.Execute(value);
        group.Remove.Execute(value);
    }
}
