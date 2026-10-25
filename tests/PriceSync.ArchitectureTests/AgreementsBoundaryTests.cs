using FluentAssertions;
using PriceSync.Modules.Agreements;
using PriceSync.Modules.Agreements.Contracts;
using Xunit;
using static PriceSync.ArchitectureTests.Support.RepositoryProjects;

namespace PriceSync.ArchitectureTests;

public sealed class AgreementsBoundaryTests
{
    [Fact]
    public void Agreements_exports_only_composition_surfaces_and_its_owned_identifier()
    {
        typeof(DependencyInjection).Assembly.ExportedTypes
            .Select(type => type.FullName).Should().BeEquivalentTo([
                "PriceSync.Modules.Agreements.DependencyInjection",
                "PriceSync.Modules.Agreements.Endpoints",
            ]);
        typeof(AgreementId).Assembly.ExportedTypes.Select(type => type.FullName)
            .Should().BeEquivalentTo(["PriceSync.Modules.Agreements.Contracts.AgreementId"]);
    }

    [Fact]
    public void Agreements_persistence_is_only_referenced_inside_its_own_module()
    {
        var root = FindRepositoryRoot(AppContext.BaseDirectory);
        var ownRoot = Path.Combine(root, "src", "PriceSync.Modules.Agreements");
        var forbidden = new[] { "AgreementsDbContext", "PriceSync.Modules.Agreements.Persistence", "agreements.price_agreements" };
        var violations = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(ownRoot, path).StartsWith("..", StringComparison.Ordinal))
            .Where(path => forbidden.Any(File.ReadAllText(path).Contains)).ToArray();
        violations.Should().BeEmpty();
    }
}
