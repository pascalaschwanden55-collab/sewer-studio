namespace AuswertungPro.Next.UI.Ai;

public static class VisionModelSelectionPolicy
{
    public static string Select(string configuredModel, IEnumerable<string> availableModels)
    {
        ArgumentNullException.ThrowIfNull(configuredModel);
        ArgumentNullException.ThrowIfNull(availableModels);

        var configuredExists = false;
        string? fallbackVision = null;

        foreach (var model in availableModels)
        {
            if (model.StartsWith(configuredModel, StringComparison.OrdinalIgnoreCase) ||
                model.Equals(configuredModel, StringComparison.OrdinalIgnoreCase))
                configuredExists = true;

            if (fallbackVision == null && model.Contains("vl", StringComparison.OrdinalIgnoreCase))
                fallbackVision = model;
        }

        return !configuredExists && fallbackVision != null
            ? fallbackVision
            : configuredModel;
    }
}
