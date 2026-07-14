using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerDetectionConfirmationOwnershipContractTests
{
    [Fact]
    public void Bestaetigungszustand_Liegt_Im_LiveDetectionController_Nicht_Im_PlayerWindow()
    {
        var windowFields = typeof(PlayerWindow).GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);
        var controllerFields = typeof(LiveDetectionController).GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);

        Assert.Contains(
            controllerFields,
            field => field.FieldType == typeof(DetectionConfirmationBuffer));
        Assert.DoesNotContain(
            windowFields,
            field => field.FieldType == typeof(DetectionConfirmationBuffer));
        Assert.DoesNotContain(windowFields, field => field.FieldType == typeof(byte[]));
        Assert.DoesNotContain(
            windowFields,
            field => typeof(IReadOnlyList<LiveFrameFinding>).IsAssignableFrom(field.FieldType));
    }
}
