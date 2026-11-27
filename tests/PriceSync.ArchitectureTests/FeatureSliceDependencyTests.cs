using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace PriceSync.ArchitectureTests;

public sealed class FeatureSliceDependencyTests
{
    [Fact]
    public void Feature_slices_do_not_depend_on_other_feature_slices()
    {
        var assemblies = Directory.EnumerateFiles(AppContext.BaseDirectory, "PriceSync.Modules.*.dll")
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .ToArray();
        var slices = assemblies.SelectMany(assembly => assembly.GetTypes())
            .Select(type => type.Namespace)
            .OfType<string>()
            .Where(name => name.Contains(".Features.", StringComparison.Ordinal))
            .Select(name =>
            {
                var featureStart = name.IndexOf(".Features.", StringComparison.Ordinal) + ".Features.".Length;
                var nestedNamespace = name.IndexOf('.', featureStart);
                return nestedNamespace < 0 ? name : name[..nestedNamespace];
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        slices.Should().NotBeEmpty();
        var types = Types.InAssemblies(assemblies);
        var violations = new List<string>();
        foreach (var slice in slices)
        {
            var otherSlices = slices.Where(other => other != slice).ToArray();
            var result = types.That().ResideInNamespaceMatching($@"^{System.Text.RegularExpressions.Regex.Escape(slice)}(\.|$)")
                .ShouldNot().HaveDependencyOnAny(otherSlices).GetResult();
            if (!result.IsSuccessful)
                violations.AddRange(result.FailingTypeNames);
        }

        violations.Should().BeEmpty("feature slices must only depend on code outside Features or in their own slice");
    }
}
