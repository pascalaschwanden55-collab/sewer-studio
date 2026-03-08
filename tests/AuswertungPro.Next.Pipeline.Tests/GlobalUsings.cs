global using Xunit;

// Alle Tests die Env-Vars manipulieren müssen sequenziell laufen.
[CollectionDefinition("EnvironmentVars", DisableParallelization = true)]
public class EnvironmentVarsCollection;
