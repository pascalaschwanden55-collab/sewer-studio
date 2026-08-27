global using AuswertungPro.Next.Application.Ai;
global using Xunit;

[CollectionDefinition("EnvironmentVars", DisableParallelization = true)]
public sealed class EnvironmentVarsCollection;

// Echte WPF-Fenster brauchen exklusiven Zugriff auf die prozessweiten
// Application- und Dispatcher-Ressourcen. Sonst wird der Rauchtest unter Last flatterig.
[CollectionDefinition("IsolatedWpf", DisableParallelization = true)]
public sealed class IsolatedWpfCollection;
