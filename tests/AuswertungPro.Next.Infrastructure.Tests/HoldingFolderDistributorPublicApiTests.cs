using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>Sichert die vor der Baum-Erweiterung vorhandenen CLR-Signaturen.</summary>
public sealed class HoldingFolderDistributorPublicApiTests
{
    [Fact]
    public void Bestehende_acht_verteil_overloads_bleiben_binaer_erhalten()
    {
        var project = typeof(Project);
        var progress = typeof(IProgress<HoldingFolderDistributor.DistributionProgress>);
        var strings = typeof(IEnumerable<string>);
        var expectedReturnType = typeof(IReadOnlyList<HoldingFolderDistributor.DistributionResult>);
        var signatures = new (string Name, Type[] Parameters, int RequiredCount, object?[] Defaults)[]
        {
            ("Distribute", [typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(string), project, progress, typeof(string)], 3, [false, false, true, "__UNMATCHED", null, null, null]),
            ("DistributeFiles", [strings, typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(string), project, progress, typeof(string)], 3, [false, false, true, "__UNMATCHED", null, null, null]),
            ("DistributeTxt", [typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(string), project, progress], 3, [false, false, true, "__UNMATCHED", null, null]),
            ("DistributeTxtFiles", [strings, typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(string), project, progress], 3, [false, false, true, "__UNMATCHED", null, null]),
            ("DistributeShafts", [typeof(string), typeof(string), typeof(bool), typeof(bool), project, progress], 2, [false, false, null, null]),
            ("DistributeShaftFiles", [strings, typeof(string), typeof(bool), typeof(bool), project, progress], 2, [false, false, null, null]),
            ("DistributeDichtheit", [typeof(string), typeof(string), typeof(bool), typeof(bool), project, progress, typeof(IHaltungCadastreResolver)], 2, [false, false, null, null, null]),
            ("DistributeDichtheitFiles", [strings, typeof(string), typeof(bool), typeof(bool), project, progress, typeof(IHaltungCadastreResolver)], 2, [false, false, null, null, null]),
        };

        foreach (var signature in signatures)
        {
            var method = typeof(HoldingFolderDistributor).GetMethod(
                signature.Name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: signature.Parameters,
                modifiers: null);

            Assert.True(method is not null, $"Alte CLR-Signatur fehlt: {signature.Name}");
            Assert.Equal(expectedReturnType, method!.ReturnType);

            var parameters = method.GetParameters();
            Assert.All(
                parameters.Take(signature.RequiredCount),
                parameter => Assert.False(parameter.HasDefaultValue));

            var optionalParameters = parameters.Skip(signature.RequiredCount).ToArray();
            Assert.Equal(signature.Defaults.Length, optionalParameters.Length);
            for (var index = 0; index < optionalParameters.Length; index++)
            {
                Assert.True(
                    optionalParameters[index].HasDefaultValue,
                    $"Alter Standardwert fehlt: {signature.Name}.{optionalParameters[index].Name}");
                Assert.Equal(signature.Defaults[index], optionalParameters[index].DefaultValue);
            }
        }
    }

    [Fact]
    public void Bestehender_distribution_result_vertrag_bleibt_binaer_erhalten()
    {
        var resultType = typeof(HoldingFolderDistributor.DistributionResult);
        var videoStatus = typeof(HoldingFolderDistributor.VideoMatchStatus);
        var constructorTypes = new[]
        {
            typeof(bool),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            videoStatus,
            typeof(bool),
            typeof(string)
        };

        var constructor = resultType.GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: constructorTypes,
            modifiers: null);

        Assert.NotNull(constructor);
        var constructorParameters = constructor!.GetParameters();
        Assert.All(constructorParameters.Take(9), parameter => Assert.False(parameter.HasDefaultValue));
        Assert.True(constructorParameters[9].HasDefaultValue);
        Assert.Equal(false, constructorParameters[9].DefaultValue);
        Assert.True(constructorParameters[10].HasDefaultValue);
        Assert.Null(constructorParameters[10].DefaultValue);

        var expectedProperties = new (string Name, Type Type)[]
        {
            ("Success", typeof(bool)),
            ("Message", typeof(string)),
            ("SourcePdfPath", typeof(string)),
            ("SourceVideoPath", typeof(string)),
            ("DestPdfPath", typeof(string)),
            ("DestVideoPath", typeof(string)),
            ("InfoPath", typeof(string)),
            ("HoldingFolder", typeof(string)),
            ("VideoStatus", videoStatus),
            ("PdfCorrected", typeof(bool)),
            ("PdfCorrectionMessage", typeof(string))
        };

        foreach (var expected in expectedProperties)
        {
            var property = resultType.GetProperty(
                expected.Name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            Assert.Equal(expected.Type, property!.PropertyType);
            Assert.True(property.CanRead);
        }

        var deconstructTypes = constructorTypes.Select(type => type.MakeByRefType()).ToArray();
        var deconstruct = resultType.GetMethod(
            "Deconstruct",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: deconstructTypes,
            modifiers: null);
        Assert.NotNull(deconstruct);
        Assert.Equal(typeof(void), deconstruct!.ReturnType);
    }
}
